using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Bills;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Bills.GetByGuid;

/// <summary>
/// Port of <c>dbo.BillsGet</c>. Step numbers below match the markers in that SP.
///
///   1. Resolve header + PaidAmount + IsAutoCreated in one projection.
///   2. NotFound short-circuit when (Guid, CompanyId) doesn't match anything.
///   3. Load line items (ProductService + HeadTransaction + HeadSub joins).
///   4. Load taxes for those lines.
///   5. Load vendor summary.
///   6. Fold everything into a single BillDetailResponse.
///
/// The SP returns 4 result sets; we collapse them into one DTO so the client gets one JSON body.
/// Queries 3-5 could be parallelised with Task.WhenAll if profiling shows the sequential cost matters.
/// </summary>
internal sealed class GetBillByGuidQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetBillByGuidQuery, BillDetailResponse>
{
    public async Task<Result<BillDetailResponse>> Handle(GetBillByGuidQuery query, CancellationToken cancellationToken)
    {
        // Step 1: header + correlated SUM(payments) + EXISTS(RecurringBillMaps) in a single round-trip.
        // The nullable Sum + ?? 0m replaces the SP's explicit EXISTS-then-SUM pattern.
        var header = await (
            from b in context.Bills.AsNoTracking()
            where b.Guid == query.Guid && b.CompanyId == query.CompanyId
            select new
            {
                Bill = b,
                PaidAmount = context.BillPaymentDetails
                    .Where(p => p.BillId == b.Id)
                    .Sum(p => p.Amount) ?? 0m,
                IsAutoCreated = context.RecurringBillMaps.Any(m => m.BillId == b.Id)
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Step 2: bail with HTTP 404 if nothing matched.
        if (header is null)
        {
            return Result.Failure<BillDetailResponse>(BillErrors.NotFound(query.Guid));
        }

        int billId = header.Bill.Id;

        // Step 3: line items joined to ProductService + HeadTransaction + HeadSub.
        // Same shape as the SP's 2nd result set, projected straight into the response DTO.
        List<BillDetailResponse.BillLine> lines = await (
            from t in context.BillTransactions.AsNoTracking()
            join p in context.ProductServices on t.ProductId equals p.Id
            join th in context.HeadTransactions on t.ProductTransactionHeadId equals th.Id
            join sh in context.HeadSubs on th.SubHeadsId equals sh.Id
            where t.BillId == billId && t.CompanyId == query.CompanyId
            select new BillDetailResponse.BillLine
            {
                Id = t.Id,
                ProductId = t.ProductId,
                ProductName = p.Name,
                Description = t.Description,
                SubHeadId = sh.Id,
                SubHeadName = sh.Name,
                ProductTransactionHeadId = t.ProductTransactionHeadId,
                TransactionHeadName = th.Name,
                Quantity = t.Quantity,
                Price = t.Price
            })
            .ToListAsync(cancellationToken);

        // Step 4: taxes for the lines we just loaded. We feed the materialised line Ids back into the
        // query instead of re-running the BillTransactions sub-select the SP uses.
        int[] lineIds = lines.Select(l => l.Id).ToArray();

        List<BillDetailResponse.BillLineTax> taxes = lineIds.Length == 0
            ? []
            : await context.BillTransactionTaxes.AsNoTracking()
                .Where(t => t.CompanyId == query.CompanyId && lineIds.Contains(t.BillTransactionId))
                .Select(t => new BillDetailResponse.BillLineTax
                {
                    Id = t.Id,
                    BillTransactionId = t.BillTransactionId,
                    TransactionHeadTaxId = t.TransactionHeadTaxId,
                    Name = t.Name,
                    Rate = t.Rate
                })
                .ToListAsync(cancellationToken);

        // Step 5: vendor summary keyed by Bills.HeadTransactionVendorId (1:1 lookup).
        BillDetailResponse.VendorSummary? vendor = await context.Vendors.AsNoTracking()
            .Where(v => v.HeadTransactionId == header.Bill.HeadTransactionVendorId && v.CompanyId == query.CompanyId)
            .Select(v => new BillDetailResponse.VendorSummary
            {
                Id = v.Id,
                HeadTransactionId = v.HeadTransactionId,
                CompanyId = v.CompanyId,
                FirstName = v.FirstName,
                LastName = v.LastName,
                VendorName = v.VendorName,
                AccountNumber = v.AccountNumber,
                Email = v.Email,
                Currency = v.Currency,
                BillCountry = v.BillCountry,
                BillState = v.BillState,
                BillCity = v.BillCity,
                BillAddress1 = v.BillAddress1,
                BillAddress2 = v.BillAddress2,
                BillPostCode = v.BillPostCode,
                Phone = v.Phone,
                Mobile = v.Mobile,
                Fax = v.Fax,
                TollFree = v.TollFree,
                Portal = v.Portal,
                CorporateIdNumber = v.CorporateIdNumber,
                VatIdNumber = v.VatIdNumber
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Step 6: assemble the response (DueAmount = Price - sum of payments).
        return new BillDetailResponse
        {
            Header = new BillDetailResponse.BillHeader
            {
                Id = header.Bill.Id,
                Guid = header.Bill.Guid,
                CompanyId = header.Bill.CompanyId,
                HeadTransactionVendorId = header.Bill.HeadTransactionVendorId,
                Status = header.Bill.Status,
                InvoiceNo = header.Bill.InvoiceNo,
                PoNumber = header.Bill.PoNumber,
                ExchangeRate = header.Bill.ExchangeRate,
                Price = header.Bill.Price,
                DueAmount = header.Bill.Price - header.PaidAmount,
                BillAt = header.Bill.BillAt,
                DueAt = header.Bill.DueAt,
                Currency = header.Bill.Currency,
                Notes = header.Bill.Notes,
                Created = header.Bill.Created,
                PaymentDueDays = 0,
                IsAutoCreated = header.IsAutoCreated
            },
            Lines = lines,
            Taxes = taxes,
            Vendor = vendor
        };
    }
}
