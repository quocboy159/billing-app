using System.Linq.Expressions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Services;
using Domain.Bills;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Bills.GetForExport;

/// <summary>
/// Port of <c>dbo.BillsGetForExport</c>. Step numbers below match the markers in that SP.
///
///   1. Resolve business "today" + the deleted-window cutoff.
///   2. Build the status predicate ONCE  -- the SP repeats the same 5-branch OR three times.
///   3. Join Bills + Vendors LEFT once, apply every optional filter on the tuple
///      (including the keyword filter so we don't duplicate the join).
///   4. Project to BillExportRowResponse with correlated subqueries for the three measures.
///      EF emits ONE SQL statement (verified) -- the subqueries become OUTER APPLYs / scalar
///      subqueries, not N+1 round-trips. The pre-aggregate alternative (one GroupBy per
///      measure JOINed in) is theoretically faster for very large unpaginated exports but
///      adds materialisation quirks under the EF InMemory provider used in tests; stick with
///      correlated subqueries here because the query is paginated (Skip/Take) and the
///      supporting indexes (BillId on lines/taxes/payments) make each subquery O(log n).
///   5. Skip/Take pagination.
/// </summary>
internal sealed class GetBillsForExportQueryHandler(
    IApplicationDbContext context,
    IBusinessClock businessClock)
    : IQueryHandler<GetBillsForExportQuery, List<BillExportRowResponse>>
{
    public async Task<Result<List<BillExportRowResponse>>> Handle(
        GetBillsForExportQuery query,
        CancellationToken cancellationToken)
    {
        // Step 1: anchors for the overdue and deleted-window branches of the status predicate.
        DateTime businessToday = await businessClock.CurrentDateAsync(query.CompanyId, cancellationToken);
        DateTime deletedCutoff = DateTime.UtcNow.AddDays(-query.DeletedInvoiceDisplayFor);

        // Step 2: status predicate (5 branches: default, Overdue, Deleted, Archived, explicit).
        Expression<Func<Bill, bool>> statusPredicate = BuildStatusPredicate(
            query.Status, businessToday, deletedCutoff);

        // Step 3: bills + vendor LEFT JOIN done ONCE. All optional filters operate on the tuple,
        // including the keyword filter -- the previous version joined to Vendors a second time
        // for the keyword filter, producing a redundant join.
        var filtered =
            from b in context.Bills.AsNoTracking().Where(b => b.CompanyId == query.CompanyId).Where(statusPredicate)
            join v in context.Vendors on b.HeadTransactionVendorId equals v.HeadTransactionId into vendorJoin
            from v in vendorJoin.DefaultIfEmpty()
            select new { Bill = b, Vendor = v };

        if (query.VendorId.HasValue)
        {
            int vendorId = query.VendorId.Value;
            filtered = filtered.Where(x => x.Bill.HeadTransactionVendorId == vendorId);
        }

        if (query.From.HasValue)
        {
            DateTime from = query.From.Value;
            filtered = filtered.Where(x => x.Bill.Created >= from);
        }

        if (query.To.HasValue)
        {
            DateTime to = query.To.Value;
            filtered = filtered.Where(x => x.Bill.Created <= to);
        }

        if (!string.IsNullOrEmpty(query.InvoiceNo))
        {
            string invoiceNo = query.InvoiceNo;
            filtered = filtered.Where(x => x.Bill.InvoiceNo == invoiceNo);
        }

        if (!string.IsNullOrEmpty(query.FilterKeyword))
        {
            string keyword = $"%{query.FilterKeyword}%";
            filtered = filtered.Where(x =>
                EF.Functions.Like(x.Bill.InvoiceNo, keyword)
                || (x.Vendor != null && EF.Functions.Like(x.Vendor.VendorName, keyword)));
        }

        // Step 4: project to the response. The three correlated subqueries replace
        // @tblTaxes / @tblDiscounts / the GROUP BY on BillPaymentDetails in the SP.
        // EF translates this as a single SQL statement (verified) with OUTER APPLYs;
        // not N+1, despite the visual look of "DbContext access inside a Select".
        IQueryable<BillExportRowResponse> projected =
            from x in filtered
            orderby x.Bill.Id descending
            select new BillExportRowResponse
            {
                Id = x.Bill.Id,
                Guid = x.Bill.Guid,
                HeadTransactionVendorId = x.Bill.HeadTransactionVendorId,
                VendorName = x.Vendor != null ? x.Vendor.VendorName : null,
                Status = x.Bill.Status,
                InvoiceNo = x.Bill.InvoiceNo,
                BillAt = x.Bill.BillAt,
                DueAt = x.Bill.DueAt,
                Price = x.Bill.Price,
                Currency = x.Bill.Currency,
                Notes = x.Bill.Notes,
                // DueAmount = Price - SUM(payments). Nullable Sum + ?? 0m handles bills with no payments.
                DueAmount = x.Bill.Price - context.BillPaymentDetails
                    .Where(p => p.BillId == x.Bill.Id)
                    .Sum(p => p.Amount) ?? 0m,
                // SubTotal = SUM(Quantity * Price) per line (= @tblDiscounts in the SP).
                SubTotal = context.BillTransactions
                    .Where(t => t.BillId == x.Bill.Id)
                    .Sum(t => (decimal?)(t.Quantity * t.Price)) ?? 0m,
                // TotalTax = SUM(Quantity * Price * Rate / 100) over lines x taxes (= @tblTaxes).
                TotalTax = context.BillTransactions
                    .Where(t => t.BillId == x.Bill.Id)
                    .SelectMany(t => context.BillTransactionTaxes
                        .Where(tt => tt.BillTransactionId == t.Id)
                        .Select(tt => (decimal?)(t.Quantity * t.Price * tt.Rate / 100m)))
                    .Sum() ?? 0m
            };

        // Step 5: pagination. The SP declares @PageNumber/@RecordsPerPage but never uses them; we do.
        List<BillExportRowResponse> rows = await projected
            .Skip((query.PageNumber - 1) * query.RecordsPerPage)
            .Take(query.RecordsPerPage)
            .ToListAsync(cancellationToken);

        return rows;
    }

    /// <summary>
    /// Builds the single status predicate the SP duplicates 3x. 5 branches:
    ///   - null            -> exclude Deleted + Archived  (default).
    ///   - Overdue (1001)  -> Approved/Partial with DueAt <= today, not Deleted, not Archived.
    ///   - Deleted (2000)  -> Deleted bills with Deleted >= cutoff (= last N days).
    ///   - Archived (3000) -> only archived bills.
    ///   - explicit        -> exact BillStatus match, excluding Deleted + Archived.
    /// </summary>
    private static Expression<Func<Bill, bool>> BuildStatusPredicate(
        BillExportStatusFilter? status,
        DateTime businessToday,
        DateTime deletedCutoff)
    {
        if (status is null)
        {
            return b => b.Status != BillStatus.Deleted && !b.IsArchive;
        }

        if (status.FilterStatus == BillFilterStatus.Overdue)
        {
            return b => (b.Status == BillStatus.Approved || b.Status == BillStatus.PartiallySettled)
                        && b.DueAt <= businessToday
                        && b.Status != BillStatus.Deleted
                        && !b.IsArchive;
        }

        if (status.FilterStatus == BillFilterStatus.Deleted)
        {
            return b => b.Status == BillStatus.Deleted
                        && b.Deleted != null
                        && b.Deleted >= deletedCutoff;
        }

        if (status.FilterStatus == BillFilterStatus.Archived)
        {
            return b => b.IsArchive;
        }

        BillStatus explicitStatus = status.Status!.Value;
        return b => b.Status == explicitStatus && b.Status != BillStatus.Deleted && !b.IsArchive;
    }
}
