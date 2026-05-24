using Domain.Banks;
using Domain.Bills;
using Domain.Companies;
using Domain.Expenses;
using Domain.Invoices;
using Domain.Products;
using Domain.Reports;
using Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database;

public static class DatabaseSeeder
{
    public const string SeedCompanyCurrency = "USD";
    public const string SeedVendorName = "Acme Supplies";
    public const string SeedExpenseSubHeadName = "Operating Expense";
    public const string SeedIncomeSubHeadName = "Sales Revenue";
    public const string SeedExpenseHeadName = "Office Supplies";
    public const string SeedIncomeHeadName = "Service Income";
    public const string SeedProductName = "Widget";
    public const string SeedBillInvoiceNo = "BILL-DEMO-001";

    public static async Task ApplyMigrationsAndSeedAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        logger.LogInformation("Applying EF Core migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Seeding baseline data (idempotent)...");
        SeededIds seeded = await SeedAsync(db, cancellationToken);
        logger.LogInformation(
            "Baseline data ready. Use these IDs in POST /bills payloads: CompanyId={CompanyId}, HeadTransactionVendorId={VendorHead}, ProductId={ProductId}, ProductTransactionHeadId={ProductHead}",
            seeded.CompanyId, seeded.VendorHeadTransactionId, seeded.ProductId, seeded.ProductTransactionHeadId);
        logger.LogInformation(
            "P&L scenario seeded for May 2026: 1 Settled Invoice ($500), 1 Approved Bill ($200), 1 Bank income ($150), 1 Expense ($75). Expected net for May 2026 = +$375.");
    }

    private sealed record SeededIds(
        int CompanyId,
        int VendorHeadTransactionId,
        int ProductId,
        int ProductTransactionHeadId);

    private static async Task<SeededIds> SeedAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        Company company = await db.Companies
            .FirstOrDefaultAsync(c => c.BusinessCurrency == SeedCompanyCurrency, cancellationToken)
            ?? AddAndReturn(db.Companies, new Company { BusinessCurrency = SeedCompanyCurrency });

        HeadSub expenseSubHead = await UpsertSubHeadAsync(db, SeedExpenseSubHeadName, ProfitLossHead.Expense, cancellationToken);
        HeadSub incomeSubHead = await UpsertSubHeadAsync(db, SeedIncomeSubHeadName, ProfitLossHead.Income, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        HeadTransaction vendorHead = await db.HeadTransactions
            .FirstOrDefaultAsync(h => h.CompanyId == company.Id && h.Name == SeedVendorName, cancellationToken)
            ?? AddAndReturn(db.HeadTransactions, new HeadTransaction
            {
                CompanyId = company.Id,
                SubHeadsId = VendorConstants.VendorSubHeadId,
                Name = SeedVendorName,
                Currency = SeedCompanyCurrency
            });

        HeadTransaction expenseHead = await db.HeadTransactions
            .FirstOrDefaultAsync(h => h.CompanyId == company.Id && h.Name == SeedExpenseHeadName, cancellationToken)
            ?? AddAndReturn(db.HeadTransactions, new HeadTransaction
            {
                CompanyId = company.Id,
                SubHeadsId = expenseSubHead.Id,
                Name = SeedExpenseHeadName,
                Currency = SeedCompanyCurrency
            });

        HeadTransaction incomeHead = await db.HeadTransactions
            .FirstOrDefaultAsync(h => h.CompanyId == company.Id && h.Name == SeedIncomeHeadName, cancellationToken)
            ?? AddAndReturn(db.HeadTransactions, new HeadTransaction
            {
                CompanyId = company.Id,
                SubHeadsId = incomeSubHead.Id,
                Name = SeedIncomeHeadName,
                Currency = SeedCompanyCurrency
            });

        await db.SaveChangesAsync(cancellationToken);

        if (!await db.Vendors.AnyAsync(v => v.HeadTransactionId == vendorHead.Id, cancellationToken))
        {
            db.Vendors.Add(new Vendor
            {
                HeadTransactionId = vendorHead.Id,
                CompanyId = company.Id,
                VendorName = SeedVendorName,
                Currency = SeedCompanyCurrency,
                IsActive = true,
                Created = DateTime.UtcNow
            });
        }

        ProductService product = await db.ProductServices
            .FirstOrDefaultAsync(p => p.Name == SeedProductName, cancellationToken)
            ?? AddAndReturn(db.ProductServices, new ProductService { Name = SeedProductName });

        await db.SaveChangesAsync(cancellationToken);

        await SeedProfitLossCategoriesAsync(db, incomeSubHead.Id, expenseSubHead.Id, cancellationToken);
        await SeedProfitLossScenarioAsync(db, company.Id, vendorHead.Id, expenseHead.Id, incomeHead.Id, product.Id, cancellationToken);

        return new SeededIds(company.Id, vendorHead.Id, product.Id, expenseHead.Id);
    }

    private static async Task<HeadSub> UpsertSubHeadAsync(
        ApplicationDbContext db, string name, int headId, CancellationToken cancellationToken)
    {
        HeadSub? existing = await db.HeadSubs.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (existing is null)
        {
            existing = new HeadSub { Name = name, HeadId = headId };
            db.HeadSubs.Add(existing);
        }
        else if (existing.HeadId != headId)
        {
            existing.HeadId = headId;
        }
        return existing;
    }

    private static async Task SeedProfitLossCategoriesAsync(
        ApplicationDbContext db, int incomeSubHeadId, int expenseSubHeadId, CancellationToken cancellationToken)
    {
        if (!await db.ProfitNLossCategories.AnyAsync(p => p.SubHeadId == incomeSubHeadId, cancellationToken))
        {
            db.ProfitNLossCategories.Add(new ProfitNLossCategory { SubHeadId = incomeSubHeadId, OrderId = 10 });
        }

        if (!await db.ProfitNLossCategories.AnyAsync(p => p.SubHeadId == expenseSubHeadId, cancellationToken))
        {
            db.ProfitNLossCategories.Add(new ProfitNLossCategory { SubHeadId = expenseSubHeadId, OrderId = 50 });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Seeds a small May-2026 scenario so POST /reports/profit-loss returns non-empty rows:
    //   - Invoice $500 (Settled)          -> revenue +500
    //   - Bill   $200 (Approved)          -> cost   +200
    //   - BankTransaction $150 (Debit/Income head 4000) -> revenue +150
    //   - Expense $75 (HeadId 5000)       -> cost    +75
    // Net for May 2026 = +$375.
    private static async Task SeedProfitLossScenarioAsync(
        ApplicationDbContext db,
        int companyId,
        int vendorHeadId,
        int expenseHeadId,
        int incomeHeadId,
        int productId,
        CancellationToken cancellationToken)
    {
        if (!await db.Invoices.AnyAsync(i => i.CompanyId == companyId, cancellationToken))
        {
            var invoice = new Invoice
            {
                CompanyId = companyId,
                Status = InvoiceStatus.Settled,
                InvoiceDate = new DateTime(2026, 5, 10),
                Amount = 500m
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);

            db.InvoiceTransactions.Add(new InvoiceTransaction
            {
                InvoiceId = invoice.Id,
                ProductId = productId,
                ProductTransactionHeadId = incomeHeadId,
                Quantity = 5m,
                Price = 100m
            });
            db.InvoicePaymentDetails.Add(new InvoicePaymentDetail
            {
                InvoiceId = invoice.Id,
                Amount = 500m
            });
        }

        if (!await db.Bills.AnyAsync(b => b.CompanyId == companyId && b.InvoiceNo == SeedBillInvoiceNo, cancellationToken))
        {
            var bill = new Bill
            {
                Guid = Guid.NewGuid(),
                CompanyId = companyId,
                HeadTransactionVendorId = vendorHeadId,
                Status = BillStatus.Approved,
                InvoiceNo = SeedBillInvoiceNo,
                Price = 200m,
                ExchangeRate = 1m,
                BillAt = new DateTime(2026, 5, 12),
                DueAt = new DateTime(2026, 6, 12),
                Currency = SeedCompanyCurrency,
                Created = new DateTime(2026, 5, 12)
            };
            db.Bills.Add(bill);
            await db.SaveChangesAsync(cancellationToken);

            db.BillTransactions.Add(new BillTransaction
            {
                CompanyId = companyId,
                BillId = bill.Id,
                ProductId = productId,
                ProductTransactionHeadId = expenseHeadId,
                Quantity = 2m,
                Price = 100m
            });
        }

        if (!await db.BankTransactions.AnyAsync(b => b.CompanyId == companyId, cancellationToken))
        {
            var bankTx = new BankTransaction
            {
                CompanyId = companyId,
                TransactionDate = new DateTime(2026, 5, 15),
                TransactionType = BankTransactionType.Debit
            };
            db.BankTransactions.Add(bankTx);
            await db.SaveChangesAsync(cancellationToken);

            db.BankSubTransactions.Add(new BankSubTransaction
            {
                BankTransactionId = bankTx.Id,
                TransactionHeadId = incomeHeadId,
                Amount = 150m,
                TransactionType = BankTransactionType.Debit
            });
        }

        if (!await db.Expenses.AnyAsync(e => e.CompanyId == companyId, cancellationToken))
        {
            var expense = new Expense
            {
                CompanyId = companyId,
                ExpenseDate = new DateTime(2026, 5, 18)
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync(cancellationToken);

            db.ExpenseTransactions.Add(new ExpenseTransaction
            {
                ExpenseId = expense.Id,
                CompanyId = companyId,
                TransactionHeadId = expenseHeadId,
                Amount = 75m,
                InvoiceAmount = 0m
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static T AddAndReturn<T>(DbSet<T> set, T entity) where T : class
    {
        set.Add(entity);
        return entity;
    }
}
