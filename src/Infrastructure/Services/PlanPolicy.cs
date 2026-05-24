using Application.Abstractions.Data;
using Application.Abstractions.Services;
using Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Port of <c>dbo.IsPlanAllowTransactionV2</c> and <c>dbo.IsPlanAllowBulkTransactionsV2</c>.
///
/// Flow (mirrors the SP):
///   1. Resolve (MaxLimit, LimitCheckingDate, AlertMessage) via <see cref="IPlanFeatureLookup"/>.
///      The SP does this with a 4-way join across PlanFeatureCompanies / PlanSubscriptions /
///      PlanFeatureAlerts / PlanFeatureMasters (and a fallback to PlanFeatures + PlanFeatureLimits
///      for free plans). Those tables aren't in our domain yet, so the default stub returns "unlimited".
///   2. If MaxLimit is Unlimited / 0 / -1, short-circuit: the SP's tail logic resolves these to
///      "allow" (Unlimited or 0) or "disallow" (-1) without counting.
///   3. Otherwise count current usage in the table that matches @FeatureId, compare
///      (existingCount + bulkItemsCount) against the limit (with the +1 quirk for features 18 and 20),
///      and return the AlertMessage when over.
/// </summary>
internal sealed class PlanPolicy(
    IApplicationDbContext context,
    IPlanFeatureLookup featureLookup) : IPlanPolicy
{
    public Task<string?> CheckTransactionAllowedAsync(
        int companyId,
        int transactionType,
        int extra = 0,
        string context = "",
        CancellationToken cancellationToken = default) =>
        CheckBulkTransactionAllowedAsync(
            companyId,
            transactionType,
            moduleId: extra,
            userId: null,
            bulkItemsCount: 0,
            cancellationToken);

    public async Task<string?> CheckBulkTransactionAllowedAsync(
        int companyId,
        int featureId,
        int moduleId,
        string? userId,
        int bulkItemsCount,
        CancellationToken cancellationToken = default)
    {
        // Step 1: resolve the plan limit for (company, feature).
        PlanFeatureResolution resolution = await featureLookup.ResolveAsync(companyId, featureId, cancellationToken);

        // Step 2: the SP's tail logic mapping limit -> allow/disallow for boundary values.
        switch (resolution.MaxLimit)
        {
            case PlanFeatureLimits.Unlimited:
            case PlanFeatureLimits.NoLimit:
                return null; // always allow
            case PlanFeatureLimits.Unavailable:
                // SP path: @IsAllow=0 + @AlertMessage already set by the lookup branch.
                return resolution.AlertMessage;
        }

        // Features that the SP unconditionally allows (SET @IsAllow='1') regardless of limit.
        if (featureId is 11 or 13 or 14 or 15 or 17)
        {
            return null;
        }

        // Step 3: count current usage and compare to the limit.
        int currentCount = await CountUsageAsync(
            featureId, moduleId, companyId, userId, resolution.LimitCheckingDate, cancellationToken);

        // Features 18 (owner roles) and 20 (distinct users) use (MaxLimit + 1) in the comparison.
        int effectiveLimit = featureId is 18 or 20
            ? resolution.MaxLimit + 1
            : resolution.MaxLimit;

        return effectiveLimit >= currentCount + bulkItemsCount
            ? null
            : resolution.AlertMessage;
    }

    /// <summary>
    /// Returns the SP's <c>COUNT(*)</c> for the table that matches <paramref name="featureId"/>.
    /// Features whose source table isn't in our domain return 0 (= "always allow") with a code
    /// comment marking the extension point.
    /// </summary>
    private async Task<int> CountUsageAsync(
        int featureId,
        int moduleId,
        int companyId,
        string? userId,
        DateTime since,
        CancellationToken cancellationToken)
    {
        return featureId switch
        {
            // 1, 10, 23: Invoices. We use InvoiceDate as the proxy for SP's Created column.
            1 or 10 => await context.Invoices.AsNoTracking()
                .CountAsync(i => i.CompanyId == companyId && i.InvoiceDate >= since, cancellationToken),

            23 => await CountMultiCurrencyInvoicesAsync(companyId, since, cancellationToken),

            // 2: EstimateInvoices (module=1) or RecurringInvoices (module=2). Not modelled yet.
            2 => 0, // TODO: add Estimate/Recurring invoice entities to enforce.

            // 3: ProductServices joined to HeadTransactions/HeadSubs by HeadId (4000 income, 5000 expense).
            //    ProductService is missing CompanyId + Created today, so we can't filter accurately.
            3 => 0, // TODO: add CompanyId + Created to ProductService to enforce.

            // 4: Bills.
            4 => await context.Bills.AsNoTracking()
                .CountAsync(b => b.CompanyId == companyId && b.Created >= since, cancellationToken),

            // 5: EstimateBills. Not modelled yet.
            5 => 0, // TODO: add EstimateBill entity to enforce.

            // 6: Vendors.
            6 => await context.Vendors.AsNoTracking()
                .CountAsync(v => v.CompanyId == companyId && v.Created >= since, cancellationToken),

            // 7: BankAccounts with IsPlaidAccount=1. Not modelled yet (only BankTransaction is).
            7 => 0, // TODO: add BankAccount entity to enforce.

            // 8: BankReconciles. Not modelled.
            8 => 0, // TODO: add BankReconcile entity to enforce.

            // 9: HeadTransactions. HeadTransaction has no Created column in our model.
            //    Counting without the date filter is the closest faithful behaviour.
            9 => await context.HeadTransactions.AsNoTracking()
                .CountAsync(h => h.CompanyId == companyId, cancellationToken),

            // 12: HeadTransactions with SubHeadsId=2004 (customer accounts).
            12 => await context.HeadTransactions.AsNoTracking()
                .CountAsync(h => h.CompanyId == companyId && h.SubHeadsId == 2004, cancellationToken),

            // 16: CreditNotes. Use CreditNoteDate as the proxy for Created.
            16 => await context.CreditNotes.AsNoTracking()
                .CountAsync(c => c.CompanyId == companyId && c.CreditNoteDate >= since, cancellationToken),

            // 18: Owner user roles. Not modelled (no AspNetUserRoles).
            18 => 0, // TODO: add identity / role entities to enforce.

            // 19: Customers. Not modelled (we have Vendors only).
            19 => 0, // TODO: add Customer entity to enforce.

            // 20: Distinct users. Not modelled.
            20 => 0, // TODO: add identity / role entities to enforce.

            // 21: Expenses. Use ExpenseDate as the proxy for Created.
            21 => await context.Expenses.AsNoTracking()
                .CountAsync(e => e.CompanyId == companyId && e.ExpenseDate >= since, cancellationToken),

            // 22: RecurringBills. Not modelled (we have RecurringBillMap but not the recurring template).
            22 => 0, // TODO: add RecurringBill entity to enforce.

            // 24: Bills whose Currency != company's BusinessCurrency.
            24 => await CountMultiCurrencyBillsAsync(companyId, since, cancellationToken),

            _ => 0
        };
    }

    private async Task<int> CountMultiCurrencyBillsAsync(int companyId, DateTime since, CancellationToken cancellationToken)
    {
        string? businessCurrency = await context.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.BusinessCurrency)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(businessCurrency))
        {
            // No business currency configured -> treat the comparison as "no multi-currency rows".
            return 0;
        }

        return await context.Bills.AsNoTracking()
            .CountAsync(b => b.CompanyId == companyId
                          && b.Currency != null
                          && b.Currency != businessCurrency
                          && b.Created >= since,
                cancellationToken);
    }

    private async Task<int> CountMultiCurrencyInvoicesAsync(int companyId, DateTime since, CancellationToken cancellationToken)
    {
        // The SP filters on Invoices.CustCurrency. Our Invoice entity doesn't model that column
        // (the P&L only needs InvoiceDate/Amount/Status). Return 0 until we add CustCurrency.
        _ = since;
        _ = companyId;
        await Task.CompletedTask;
        return 0; // TODO: add CustCurrency to Invoice entity to enforce.
    }
}
