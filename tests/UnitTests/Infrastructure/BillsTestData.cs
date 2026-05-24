using Domain.Bills;
using Domain.Companies;
using Domain.Products;
using Domain.Vendors;
using Infrastructure.Database;

namespace UnitTests.Infrastructure;

internal static class BillsTestData
{
    public const int CompanyId = 100;
    public const string CompanyCurrency = "USD";

    public const int VendorHeadTransactionId = 5001;
    public const int VendorId = 7001;
    public const string VendorName = "Acme Supplies";

    public const int ProductId = 9001;
    public const string ProductName = "Widget";

    public const int SubHeadId = 4001;
    public const string SubHeadName = "Operating Expense";

    public const int ProductTransactionHeadId = 6001;
    public const string ProductTransactionHeadName = "Office Supplies";

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        db.Companies.Add(new Company
        {
            Id = CompanyId,
            BusinessCurrency = CompanyCurrency
        });

        db.HeadSubs.Add(new HeadSub { Id = SubHeadId, Name = SubHeadName });

        db.HeadTransactions.Add(new HeadTransaction
        {
            Id = VendorHeadTransactionId,
            CompanyId = CompanyId,
            SubHeadsId = VendorConstants.VendorSubHeadId,
            Name = VendorName,
            Currency = CompanyCurrency
        });

        db.HeadTransactions.Add(new HeadTransaction
        {
            Id = ProductTransactionHeadId,
            CompanyId = CompanyId,
            SubHeadsId = SubHeadId,
            Name = ProductTransactionHeadName,
            Currency = CompanyCurrency
        });

        db.Vendors.Add(new Vendor
        {
            Id = VendorId,
            HeadTransactionId = VendorHeadTransactionId,
            CompanyId = CompanyId,
            VendorName = VendorName,
            Currency = CompanyCurrency,
            IsActive = true,
            Created = DateTime.UtcNow
        });

        db.ProductServices.Add(new ProductService { Id = ProductId, Name = ProductName });

        await db.SaveChangesAsync();
    }

    public static Bill BuildBill(
        int id,
        BillStatus status = BillStatus.Approved,
        decimal price = 100m,
        DateTime? dueAt = null,
        DateTime? created = null,
        bool isArchive = false,
        DateTime? deleted = null,
        string? invoiceNo = null) => new()
        {
            Id = id,
            Guid = Guid.NewGuid(),
            CompanyId = CompanyId,
            HeadTransactionVendorId = VendorHeadTransactionId,
            Status = status,
            InvoiceNo = invoiceNo ?? $"INV-{id}",
            Price = price,
            ExchangeRate = 1m,
            BillAt = created ?? new DateTime(2026, 5, 1),
            DueAt = dueAt ?? new DateTime(2026, 6, 1),
            Currency = CompanyCurrency,
            Created = created ?? new DateTime(2026, 5, 1),
            IsArchive = isArchive,
            Deleted = deleted
        };

    public static async Task AddBillWithLineAsync(
        ApplicationDbContext db,
        Bill bill,
        decimal lineQty = 2m,
        decimal linePrice = 50m,
        decimal taxRate = 10m,
        decimal? paidAmount = null)
    {
        db.Bills.Add(bill);
        await db.SaveChangesAsync();

        var line = new BillTransaction
        {
            CompanyId = CompanyId,
            BillId = bill.Id,
            ProductId = ProductId,
            ProductTransactionHeadId = ProductTransactionHeadId,
            Description = "test line",
            Quantity = lineQty,
            Price = linePrice
        };
        db.BillTransactions.Add(line);
        await db.SaveChangesAsync();

        db.BillTransactionTaxes.Add(new BillTransactionTax
        {
            CompanyId = CompanyId,
            BillTransactionId = line.Id,
            TransactionHeadTaxId = 1,
            Name = "VAT",
            Rate = taxRate
        });

        if (paidAmount.HasValue)
        {
            db.BillPaymentDetails.Add(new BillPaymentDetail
            {
                BillId = bill.Id,
                Amount = paidAmount.Value
            });
        }

        await db.SaveChangesAsync();
    }
}
