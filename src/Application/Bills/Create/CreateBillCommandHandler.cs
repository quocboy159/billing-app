using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Services;
using Domain.Activities;
using Domain.Bills;
using Domain.Companies;
using Domain.Vendors;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Bills.Create;

/// <summary>
/// Port of <c>dbo.BillsAdd</c>. Application layer now orchestrates only:
///   - policy checks (plan, multi-currency),
///   - data access via <see cref="IBillRepository"/> / <see cref="IVendorRepository"/> /
///     <see cref="IHeadTransactionRepository"/> / <see cref="ICompanyRepository"/>,
///   - and the unit-of-work boundary (transaction + SaveChanges).
///
/// Entity construction lives on the aggregates themselves: <see cref="Bill.CreateDraft"/>,
/// <see cref="Bill.AddLine"/>, <see cref="BillTransaction.AddTax"/>,
/// <see cref="Vendor.CreateForCompany"/>, and <see cref="HeadTransaction.ForNewVendor"/>.
/// </summary>
internal sealed class CreateBillCommandHandler(
    IBillRepository bills,
    IVendorRepository vendors,
    IHeadTransactionRepository headTransactions,
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    IPlanPolicy planPolicy,
    IBillNumberGenerator numbers,
    IActivityLogger activityLogger,
    IDateTimeProvider clock)
    : ICommandHandler<CreateBillCommand, CreateBillResponse>
{
    public async Task<Result<CreateBillResponse>> Handle(CreateBillCommand command, CancellationToken cancellationToken)
    {
        BillHeaderInput header = command.Bill;

        // Step 1: plan check for Bill creation (SP @FeatureId = 4).
        string? planError = await planPolicy.CheckTransactionAllowedAsync(
            header.CompanyId, (int)PlanFeature.Bill, cancellationToken: cancellationToken);
        if (planError is not null)
        {
            return Result.Failure<CreateBillResponse>(BillErrors.PlanNotAllowed(planError));
        }

        // Step 2: read company's BusinessCurrency via the Company repo (= SP's @CompanyCurrency lookup).
        string? companyCurrency = await companies.GetBusinessCurrencyAsync(header.CompanyId, cancellationToken);

        // Step 3: multi-currency policy gate -- domain rule decides whether the gate applies.
        if (Bill.RequiresMultiCurrencyApproval(header.Currency, companyCurrency))
        {
            string? multiCurrencyError = await planPolicy.CheckTransactionAllowedAsync(
                header.CompanyId, (int)PlanFeature.MultiCurrencyBill, cancellationToken: cancellationToken);
            if (multiCurrencyError is not null)
            {
                return Result.Failure<CreateBillResponse>(BillErrors.PlanNotAllowed(multiCurrencyError));
            }
        }

        // Step 4: open the transaction. `await using` rolls back if we never reach Commit.
        await using IDbContextTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        int vendorHeadId = header.HeadTransactionVendorId;

        // Step 5: when the caller passes VendorId=0 + a Vendor DTO, create the vendor inline.
        if (vendorHeadId == 0 && command.Vendor is not null)
        {
            // 5a: vendor uniqueness check (per company) -> 409 Conflict.
            if (await vendors.ExistsByNameAsync(header.CompanyId, command.Vendor.VendorName, cancellationToken))
            {
                return Result.Failure<CreateBillResponse>(BillErrors.VendorAlreadyExists());
            }

            // 5b: build the anchor HeadTransaction via its domain factory and stage it.
            HeadTransaction newHead = HeadTransaction.ForNewVendor(
                header.CompanyId,
                command.Vendor.VendorName,
                command.Vendor.AccountNumber,
                command.Vendor.Currency);
            headTransactions.Add(newHead);
            await unitOfWork.SaveChangesAsync(cancellationToken); // populates newHead.Id

            vendorHeadId = newHead.Id;

            // 5c: build the Vendor via its factory, linked to the new HeadTransaction.
            Vendor newVendor = Vendor.CreateForCompany(
                header.CompanyId,
                vendorHeadId,
                ToVendorProfile(command.Vendor),
                clock.UtcNow);
            vendors.Add(newVendor);
        }

        // Step 6: validate the vendor HeadTransaction belongs to this company.
        if (!await headTransactions.BelongsToCompanyAsync(vendorHeadId, header.CompanyId, cancellationToken))
        {
            return Result.Failure<CreateBillResponse>(BillErrors.VendorInvalid());
        }

        // Step 7: generate the next InvoiceNo (bumps PurchaseCounters + applies Prefix/Suffix).
        BillNumber number = await numbers.NextAsync(header.CompanyId, cancellationToken);

        // Step 8: build the draft Bill via its factory. The factory raises BillCreatedDomainEvent.
        Bill bill = Bill.CreateDraft(
            companyId: header.CompanyId,
            vendorHeadId: vendorHeadId,
            invoiceNo: number.InvoiceNo,
            price: header.Price,
            exchangeRate: header.ExchangeRate,
            billAt: header.BillAt,
            dueAt: header.DueAt,
            utcNow: clock.UtcNow,
            currency: header.Currency,
            poNumber: header.PoNumber,
            notes: header.Notes);

        // Step 9: attach line items + their taxes through the aggregate's API. We map each input
        // Counter to the freshly created line, then route each tax row to the right line.
        Dictionary<int, BillTransaction> lineByCounter = command.Transactions.ToDictionary(
            t => t.Counter,
            t => bill.AddLine(
                productId: t.ProductId,
                productTransactionHeadId: t.ProductTransactionHeadId,
                quantity: t.Quantity,
                price: t.Price,
                description: t.Description));

        foreach (BillTransactionTaxInput taxInput in command.Taxes)
        {
            if (lineByCounter.TryGetValue(taxInput.CounterTranId, out BillTransaction? line))
            {
                line.AddTax(taxInput.TransactionHeadTaxId, taxInput.Name, taxInput.Rate);
            }
        }

        // Step 10: stage the bill. EF's relationship fixup persists the lines + taxes in one
        // SaveChanges (replaces the SP's CURSOR-driven insert loop and the previous AddRange dance).
        bills.Add(bill);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (bill.Id <= 0)
        {
            return Result.Failure<CreateBillResponse>(BillErrors.InsertFailed());
        }

        // Step 11: write the activity log entry (= EXEC ActivitiesAdd).
        await activityLogger.LogAsync(
            header.CompanyId,
            bill.Id,
            bill.InvoiceNo,
            ActivityTableType.Bill,
            ActivityActionType.BillCreated,
            vendorHeadId,
            remarks: null,
            userName: command.UserName,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Step 12: commit.
        await transaction.CommitAsync(cancellationToken);

        return new CreateBillResponse
        {
            Id = bill.Id,
            Guid = bill.Guid,
            InvoiceNo = bill.InvoiceNo
        };
    }

    private static VendorProfile ToVendorProfile(VendorInput input) => new(
        VendorName: input.VendorName,
        AccountNumber: input.AccountNumber,
        Currency: input.Currency,
        FirstName: input.FirstName,
        LastName: input.LastName,
        Email: input.Email,
        BillCountry: input.BillCountry,
        BillState: input.BillState,
        BillCity: input.BillCity,
        BillAddress1: input.BillAddress1,
        BillAddress2: input.BillAddress2,
        BillPostCode: input.BillPostCode,
        Phone: input.Phone,
        Mobile: input.Mobile,
        Fax: input.Fax,
        TollFree: input.TollFree,
        Portal: input.Portal,
        TaxId: input.TaxId,
        ContactPerson: input.ContactPerson,
        CorporateIdNumber: input.CorporateIdNumber,
        VatIdNumber: input.VatIdNumber);
}
