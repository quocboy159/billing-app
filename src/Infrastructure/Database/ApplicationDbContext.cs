using Application.Abstractions.Data;
using Domain.Activities;
using Domain.Banks;
using Domain.Bills;
using Domain.Companies;
using Domain.CreditNotes;
using Domain.Expenses;
using Domain.Invoices;
using Domain.Products;
using Domain.Reports;
using Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillTransaction> BillTransactions => Set<BillTransaction>();
    public DbSet<BillTransactionTax> BillTransactionTaxes => Set<BillTransactionTax>();
    public DbSet<BillPaymentDetail> BillPaymentDetails => Set<BillPaymentDetail>();
    public DbSet<RecurringBillMap> RecurringBillMaps => Set<RecurringBillMap>();

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<HeadTransaction> HeadTransactions => Set<HeadTransaction>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<PurchaseModuleCompanySetting> PurchaseModuleCompanySettings => Set<PurchaseModuleCompanySetting>();
    public DbSet<PurchaseCounter> PurchaseCounters => Set<PurchaseCounter>();

    public DbSet<ProductService> ProductServices => Set<ProductService>();
    public DbSet<HeadSub> HeadSubs => Set<HeadSub>();

    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceTransaction> InvoiceTransactions => Set<InvoiceTransaction>();
    public DbSet<InvoicePaymentDetail> InvoicePaymentDetails => Set<InvoicePaymentDetail>();

    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<CreditNoteTransaction> CreditNoteTransactions => Set<CreditNoteTransaction>();

    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<BankSubTransaction> BankSubTransactions => Set<BankSubTransaction>();

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseTransaction> ExpenseTransactions => Set<ExpenseTransaction>();

    public DbSet<ProfitNLossCategory> ProfitNLossCategories => Set<ProfitNLossCategory>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
