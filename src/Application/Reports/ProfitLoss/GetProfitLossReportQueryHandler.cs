using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Services;
using Domain.Banks;
using Domain.Bills;
using Domain.Companies;
using Domain.Invoices;
using Domain.Reports;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.ProfitLoss;

/// <summary>
/// EF Core port of <c>dbo.ReportProfitLossGet</c>.
///
/// The SP unions 6 transaction sources per period, optionally repeating for a 2nd comparison
/// period. We mirror that with 6 IQueryables concatenated via <c>Concat</c> (EF translates to
/// UNION ALL), running as a single SQL round-trip per period.
///
/// The 6 buckets:
///   1. Invoices                              -- revenue (+)
///   2. CreditNotes                           -- revenue offset (-) [accrual only]
///   3. Bills                                 -- cost (+)
///   4. Direct BankTransactions (HeadId 4000/5000) -- sign depends on Type x Head
///   5. ExpenseTransactions on HeadId=5000    -- expense (+)
///   6. ExpenseTransactions.InvoiceAmount     -- reimbursement (-)
///
/// Cash vs Accrual:
///   - Accrual (AccountType=2): full amount; date filter on InvoiceDate / BillAt.
///   - Cash    (AccountType=1): amount scaled by (paid/total); date filter on BankTransactions.TransactionDate.
/// IsRetain only applies to Period 1 (used for retained-earnings calculation).
///
/// Discount calc is inlined: EF cannot translate a private static helper, so the formula
///     base = Qty * Price
///     tax  = (TxDiscType=1 ? TxDisc : TxDiscType=2 ? base*TxDisc/100 : 0)
///     inv  = (InvDiscType=1 ? InvDisc : 0)   -- percentage invoice-disc is a SP bug; we mirror it
///     amt  = base - tax - inv
/// appears inline at each call site.
/// </summary>
internal sealed class GetProfitLossReportQueryHandler(
    IApplicationDbContext context,
    IAccountingTypeProvider accountingTypes)
    : IQueryHandler<GetProfitLossReportQuery, ProfitLossReportResponse>
{
    public async Task<Result<ProfitLossReportResponse>> Handle(
        GetProfitLossReportQuery query,
        CancellationToken cancellationToken)
    {
        // Step 1: resolve AccountType for the company (= dbo.AccountingTypeGet).
        // Stub returns Accrual by default; swap in a real DB lookup when the source table is available.
        AccountingType accountType = await accountingTypes.GetAsync(query.CompanyId, cancellationToken);

        // Step 2: build Period 1. IsRetain only applies here (retained-earnings calculation).
        List<ProfitLossRow> period1 = await BuildPeriodAsync(
            query.CompanyId, accountType, query.StartDate1, query.EndDate1, query.IsRetain, cancellationToken);

        // Step 3: build Period 2 if a second date range was supplied; isRetain is forced false
        // (the SP applies @IsRetain only to its Period 1 block).
        List<ProfitLossRow> period2 = query.StartDate2.HasValue && query.EndDate2.HasValue
            ? await BuildPeriodAsync(
                query.CompanyId, accountType, query.StartDate2.Value, query.EndDate2.Value, false, cancellationToken)
            : [];

        return new ProfitLossReportResponse { Period1 = period1, Period2 = period2 };
    }

    // Concats the 6 bucket queries (UNION ALL) and runs them in a single SQL round-trip.
    // Each bucket below returns IQueryable<ProfitLossRow> so EF can compose them server-side.
    private async Task<List<ProfitLossRow>> BuildPeriodAsync(
        int companyId,
        AccountingType accountType,
        DateTime start,
        DateTime end,
        bool isRetain,
        CancellationToken cancellationToken)
    {
        IQueryable<ProfitLossRow> rows = Invoices(companyId, accountType, start, end, isRetain)          // Bucket 1
            .Concat(CreditNotes(companyId, accountType, start, end, isRetain))                          // Bucket 2
            .Concat(Bills(companyId, accountType, start, end, isRetain))                                // Bucket 3
            .Concat(BankDirect(companyId, start, end, isRetain))                                        // Bucket 4
            .Concat(ExpensesPositive(companyId, start, end, isRetain))                                  // Bucket 5
            .Concat(ExpensesNegative(companyId, start, end, isRetain));                                 // Bucket 6

        return await rows.ToListAsync(cancellationToken);
    }

    // -----------------------------------------------------------------------------------------
    // Bucket 1: Invoices (revenue, positive)
    // -----------------------------------------------------------------------------------------
    private IQueryable<ProfitLossRow> Invoices(
        int companyId, AccountingType accountType, DateTime start, DateTime end, bool isRetain)
    {
        if (accountType == AccountingType.Accrual)
        {
            return
                from inv in context.Invoices.AsNoTracking()
                join t in context.InvoiceTransactions on inv.Id equals t.InvoiceId
                join h in context.HeadTransactions on t.ProductTransactionHeadId equals h.Id
                join s in context.HeadSubs on h.SubHeadsId equals s.Id
                join pnl in context.ProfitNLossCategories on s.Id equals pnl.SubHeadId into pnls
                from pnl in pnls.DefaultIfEmpty()
                where inv.CompanyId == companyId
                   && (inv.Status == InvoiceStatus.Approved
                       || inv.Status == InvoiceStatus.PartiallySettled
                       || inv.Status == InvoiceStatus.Settled)
                   && (isRetain || inv.InvoiceDate >= start)
                   && inv.InvoiceDate <= end
                select new ProfitLossRow
                {
                    Type = ProfitLossRowType.Invoice,
                    HeadId = s.HeadId,
                    SubHeadId = h.SubHeadsId,
                    SubHeadName = s.Name,
                    TransactionId = h.Id,
                    TransactionName = h.Name,
                    ProductPrice = (t.Quantity * t.Price)
                                   - (t.DiscountType == 1
                                       ? t.Discount
                                       : t.DiscountType == 2
                                           ? (t.Quantity * t.Price) * t.Discount / 100m
                                           : 0m)
                                   - (inv.DiscountType == 1 ? inv.Discount : 0m),
                    OrderId = pnl != null ? pnl.OrderId : 100
                };
        }

        // Cash basis: only count amounts actually paid. Date filter on BankTransactions.TransactionDate.
        return
            from inv in context.Invoices.AsNoTracking()
            join t in context.InvoiceTransactions on inv.Id equals t.InvoiceId
            join h in context.HeadTransactions on t.ProductTransactionHeadId equals h.Id
            join s in context.HeadSubs on h.SubHeadsId equals s.Id
            join ipd in context.InvoicePaymentDetails on inv.Id equals ipd.InvoiceId
            join bst in context.BankSubTransactions on ipd.BankSubTransactionId equals bst.Id
            join bt in context.BankTransactions on bst.BankTransactionId equals bt.Id
            join pnl in context.ProfitNLossCategories on s.Id equals pnl.SubHeadId into pnls
            from pnl in pnls.DefaultIfEmpty()
            where inv.CompanyId == companyId
               && (inv.Status == InvoiceStatus.Settled || inv.Status == InvoiceStatus.PartiallySettled)
               && bt.TransactionDate >= start && bt.TransactionDate <= end
            select new ProfitLossRow
            {
                Type = ProfitLossRowType.Invoice,
                HeadId = s.HeadId,
                SubHeadId = h.SubHeadsId,
                SubHeadName = s.Name,
                TransactionId = h.Id,
                TransactionName = h.Name,
                ProductPrice = ((t.Quantity * t.Price)
                                - (t.DiscountType == 1
                                    ? t.Discount
                                    : t.DiscountType == 2
                                        ? (t.Quantity * t.Price) * t.Discount / 100m
                                        : 0m)
                                - (inv.DiscountType == 1 ? inv.Discount : 0m))
                               * (ipd.Amount ?? 0m)
                               / (inv.Amount == 0m ? 1m : inv.Amount),
                OrderId = pnl != null ? pnl.OrderId : 100
            };
    }

    // -----------------------------------------------------------------------------------------
    // Bucket 2: CreditNotes (revenue offset, negative). SP guards with @AccountType=2.
    // -----------------------------------------------------------------------------------------
    private IQueryable<ProfitLossRow> CreditNotes(
        int companyId, AccountingType accountType, DateTime start, DateTime end, bool isRetain)
    {
        if (accountType != AccountingType.Accrual)
        {
            return Enumerable.Empty<ProfitLossRow>().AsQueryable();
        }

        return
            from cn in context.CreditNotes.AsNoTracking()
            join inv in context.Invoices on cn.InvoiceId equals inv.Id
            join t in context.CreditNoteTransactions on cn.Id equals t.CreditNoteId
            join p in context.ProductServices on t.ProductId equals p.Id
            join h in context.HeadTransactions on p.HeadTransactionId equals h.Id
            join s in context.HeadSubs on h.SubHeadsId equals s.Id
            join pnl in context.ProfitNLossCategories on s.Id equals pnl.SubHeadId into pnls
            from pnl in pnls.DefaultIfEmpty()
            where cn.CompanyId == companyId
               && cn.Status == (int)InvoiceStatus.Approved
               && (inv.Status == InvoiceStatus.Approved
                   || inv.Status == InvoiceStatus.PartiallySettled
                   || inv.Status == InvoiceStatus.Settled)
               && (isRetain || cn.CreditNoteDate >= start)
               && cn.CreditNoteDate <= end
            select new ProfitLossRow
            {
                Type = ProfitLossRowType.Invoice,
                HeadId = s.HeadId,
                SubHeadId = h.SubHeadsId,
                SubHeadName = s.Name,
                TransactionId = h.Id,
                TransactionName = h.Name,
                // Negative -- credit notes reduce revenue. SP passes invoice-discount type as 0
                // (no row-level invoice discount on a credit note).
                ProductPrice = -((t.Quantity * t.Price)
                                 - (t.DiscountType == 1
                                     ? t.Discount
                                     : t.DiscountType == 2
                                         ? (t.Quantity * t.Price) * t.Discount / 100m
                                         : 0m)),
                OrderId = pnl != null ? pnl.OrderId : 100
            };
    }

    // -----------------------------------------------------------------------------------------
    // Bucket 3: Bills (cost, positive). Same cash/accrual logic as Invoices but no discount math.
    // -----------------------------------------------------------------------------------------
    private IQueryable<ProfitLossRow> Bills(
        int companyId, AccountingType accountType, DateTime start, DateTime end, bool isRetain)
    {
        if (accountType == AccountingType.Accrual)
        {
            return
                from b in context.Bills.AsNoTracking()
                join t in context.BillTransactions on b.Id equals t.BillId
                join h in context.HeadTransactions on t.ProductTransactionHeadId equals h.Id
                join s in context.HeadSubs on h.SubHeadsId equals s.Id
                join pnl in context.ProfitNLossCategories on s.Id equals pnl.SubHeadId into pnls
                from pnl in pnls.DefaultIfEmpty()
                where b.CompanyId == companyId
                   && (b.Status == BillStatus.Approved
                       || b.Status == BillStatus.PartiallySettled
                       || b.Status == BillStatus.Settled)
                   && (isRetain || b.BillAt >= start)
                   && b.BillAt <= end
                select new ProfitLossRow
                {
                    Type = ProfitLossRowType.Bill,
                    HeadId = s.HeadId,
                    SubHeadId = h.SubHeadsId,
                    SubHeadName = s.Name,
                    TransactionId = h.Id,
                    TransactionName = h.Name,
                    ProductPrice = t.Quantity * t.Price,
                    OrderId = pnl != null ? pnl.OrderId : 100
                };
        }

        return
            from b in context.Bills.AsNoTracking()
            join t in context.BillTransactions on b.Id equals t.BillId
            join h in context.HeadTransactions on t.ProductTransactionHeadId equals h.Id
            join s in context.HeadSubs on h.SubHeadsId equals s.Id
            join bpd in context.BillPaymentDetails on b.Id equals bpd.BillId
            join bst in context.BankSubTransactions on bpd.Id equals bst.Id
            join bt in context.BankTransactions on bst.BankTransactionId equals bt.Id
            join pnl in context.ProfitNLossCategories on s.Id equals pnl.SubHeadId into pnls
            from pnl in pnls.DefaultIfEmpty()
            where b.CompanyId == companyId
               && (b.Status == BillStatus.Settled || b.Status == BillStatus.PartiallySettled)
               && bt.TransactionDate >= start && bt.TransactionDate <= end
            select new ProfitLossRow
            {
                Type = ProfitLossRowType.Bill,
                HeadId = s.HeadId,
                SubHeadId = h.SubHeadsId,
                SubHeadName = s.Name,
                TransactionId = h.Id,
                TransactionName = h.Name,
                ProductPrice = t.Quantity * t.Price
                               * (bpd.Amount ?? 0m)
                               / (b.Price == 0m ? 1m : b.Price),
                OrderId = pnl != null ? pnl.OrderId : 100
            };
    }

    // -----------------------------------------------------------------------------------------
    // Bucket 4: Direct BankTransactions on HeadId IN (4000, 5000). Sign depends on Type x HeadId.
    //   Expense head + Debit-like  -> -Amount   (refund / undo expense)
    //   Expense head + Credit-like -> +Amount   (paid expense)
    //   Income head  + Credit-like -> -Amount   (refunded revenue)
    //   Income head  + Debit-like  -> +Amount   (revenue received)
    // -----------------------------------------------------------------------------------------
    private IQueryable<ProfitLossRow> BankDirect(int companyId, DateTime start, DateTime end, bool isRetain) =>
        from bt in context.BankTransactions.AsNoTracking()
        join sub in context.BankSubTransactions on bt.Id equals sub.BankTransactionId
        join h in context.HeadTransactions on sub.TransactionHeadId equals h.Id
        join s in context.HeadSubs on h.SubHeadsId equals s.Id
        join pnl in context.ProfitNLossCategories on s.Id equals pnl.SubHeadId into pnls
        from pnl in pnls.DefaultIfEmpty()
        where bt.CompanyId == companyId
           && (s.HeadId == ProfitLossHead.Expense || s.HeadId == ProfitLossHead.Income)
           && (isRetain || bt.TransactionDate >= start)
           && bt.TransactionDate <= end
           && bt.ExpenseId == null
        select new ProfitLossRow
        {
            Type = s.HeadId == ProfitLossHead.Expense
                ? ProfitLossRowType.Bill
                : ProfitLossRowType.Invoice,
            HeadId = s.HeadId,
            SubHeadId = h.SubHeadsId,
            SubHeadName = s.Name,
            TransactionId = h.Id,
            TransactionName = h.Name,
            ProductPrice =
                s.HeadId == ProfitLossHead.Expense
                    ? (bt.TransactionType == BankTransactionType.Debit
                       || (bt.TransactionType == BankTransactionType.Transfer
                           && sub.TransactionType == BankTransactionType.Debit)
                          ? -sub.Amount
                          : sub.Amount)
                    : (bt.TransactionType == BankTransactionType.Credit
                       || (bt.TransactionType == BankTransactionType.Transfer
                           && sub.TransactionType == BankTransactionType.Credit)
                          ? -sub.Amount
                          : sub.Amount),
            OrderId = pnl != null ? pnl.OrderId : 100
        };

    // -----------------------------------------------------------------------------------------
    // Bucket 5: ExpenseTransactions on HeadId=5000 (expense, positive)
    // -----------------------------------------------------------------------------------------
    private IQueryable<ProfitLossRow> ExpensesPositive(int companyId, DateTime start, DateTime end, bool isRetain) =>
        from et in context.ExpenseTransactions.AsNoTracking()
        join e in context.Expenses on et.ExpenseId equals e.Id
        join h in context.HeadTransactions on et.TransactionHeadId equals h.Id
        join s in context.HeadSubs on h.SubHeadsId equals s.Id
        where et.CompanyId == companyId
           && s.HeadId == ProfitLossHead.Expense
           && (isRetain || e.ExpenseDate >= start)
           && e.ExpenseDate <= end
        select new ProfitLossRow
        {
            Type = ProfitLossRowType.Bill,
            HeadId = s.HeadId,
            SubHeadId = h.SubHeadsId,
            SubHeadName = s.Name,
            TransactionId = h.Id,
            TransactionName = h.Name,
            ProductPrice = et.Amount,
            OrderId = 100
        };

    // -----------------------------------------------------------------------------------------
    // Bucket 6: ExpenseTransactions.InvoiceAmount (reimbursement, negative)
    // -----------------------------------------------------------------------------------------
    private IQueryable<ProfitLossRow> ExpensesNegative(int companyId, DateTime start, DateTime end, bool isRetain) =>
        from et in context.ExpenseTransactions.AsNoTracking()
        join e in context.Expenses on et.ExpenseId equals e.Id
        join h in context.HeadTransactions on et.TransactionHeadId equals h.Id
        join s in context.HeadSubs on h.SubHeadsId equals s.Id
        where e.CompanyId == companyId
           && s.HeadId == ProfitLossHead.Expense
           && et.InvoiceAmount != 0m
           && (isRetain || e.ExpenseDate >= start)
           && e.ExpenseDate <= end
        select new ProfitLossRow
        {
            Type = ProfitLossRowType.Bill,
            HeadId = s.HeadId,
            SubHeadId = h.SubHeadsId,
            SubHeadName = s.Name,
            TransactionId = h.Id,
            TransactionName = h.Name,
            ProductPrice = -et.InvoiceAmount,
            OrderId = 100
        };
}
