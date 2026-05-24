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

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<Bill> Bills { get; }
    DbSet<BillTransaction> BillTransactions { get; }
    DbSet<BillTransactionTax> BillTransactionTaxes { get; }
    DbSet<BillPaymentDetail> BillPaymentDetails { get; }
    DbSet<RecurringBillMap> RecurringBillMaps { get; }

    DbSet<Vendor> Vendors { get; }
    DbSet<HeadTransaction> HeadTransactions { get; }

    DbSet<Company> Companies { get; }
    DbSet<PurchaseModuleCompanySetting> PurchaseModuleCompanySettings { get; }
    DbSet<PurchaseCounter> PurchaseCounters { get; }

    DbSet<ProductService> ProductServices { get; }
    DbSet<HeadSub> HeadSubs { get; }

    DbSet<Activity> Activities { get; }

    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceTransaction> InvoiceTransactions { get; }
    DbSet<InvoicePaymentDetail> InvoicePaymentDetails { get; }

    DbSet<CreditNote> CreditNotes { get; }
    DbSet<CreditNoteTransaction> CreditNoteTransactions { get; }

    DbSet<BankTransaction> BankTransactions { get; }
    DbSet<BankSubTransaction> BankSubTransactions { get; }

    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseTransaction> ExpenseTransactions { get; }

    DbSet<ProfitNLossCategory> ProfitNLossCategories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
