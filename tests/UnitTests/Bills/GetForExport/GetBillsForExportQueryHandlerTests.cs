using Application.Bills.GetForExport;
using Domain.Bills;
using Infrastructure.Database;
using SharedKernel;
using UnitTests.Infrastructure;

namespace UnitTests.Bills.GetForExport;

public sealed class GetBillsForExportQueryHandlerTests
{
    private static readonly DateTime BusinessToday = new(2026, 5, 22);

    [Fact]
    public async Task Default_filter_excludes_deleted_and_archived_bills()
    {
        // Arrange
        ApplicationDbContext db = await SeedScenarioAsync();
        GetBillsForExportQueryHandler handler = BuildHandler(db);
        var query = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            RecordsPerPage = 50
        };

        // Act
        Result<List<BillExportRowResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(r => r.Id).ShouldBe([5, 2, 1], ignoreOrder: false);
    }

    [Fact]
    public async Task Computes_SubTotal_TotalTax_and_DueAmount_per_row()
    {
        // Arrange
        ApplicationDbContext db = await SeedScenarioAsync();
        GetBillsForExportQueryHandler handler = BuildHandler(db);
        var query = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            Status = BillExportStatusFilter.FromInt((int)BillStatus.Approved),
            RecordsPerPage = 50
        };

        // Act
        Result<List<BillExportRowResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        BillExportRowResponse row = result.Value.Single(r => r.Id == 1);
        row.SubTotal.ShouldBe(100m);
        row.TotalTax.ShouldBe(10m);
        row.DueAmount.ShouldBe(60m);
        row.VendorName.ShouldBe(BillsTestData.VendorName);
    }

    [Fact]
    public async Task Overdue_filter_returns_only_approved_or_partial_with_DueAt_before_business_today()
    {
        // Arrange
        ApplicationDbContext db = await SeedScenarioAsync();
        GetBillsForExportQueryHandler handler = BuildHandler(db);
        var query = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            Status = BillExportStatusFilter.FromInt((int)BillFilterStatus.Overdue),
            RecordsPerPage = 50
        };

        // Act
        Result<List<BillExportRowResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(r => r.Id).ShouldBe([2]);
    }

    [Fact]
    public async Task Archived_filter_returns_only_archived_bills()
    {
        // Arrange
        ApplicationDbContext db = await SeedScenarioAsync();
        GetBillsForExportQueryHandler handler = BuildHandler(db);
        var query = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            Status = BillExportStatusFilter.FromInt((int)BillFilterStatus.Archived),
            RecordsPerPage = 50
        };

        // Act
        Result<List<BillExportRowResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(r => r.Id).ShouldBe([4]);
    }

    [Fact]
    public async Task Deleted_filter_uses_DeletedInvoiceDisplayFor_cutoff()
    {
        // Arrange
        ApplicationDbContext db = await SeedScenarioAsync();
        GetBillsForExportQueryHandler handler = BuildHandler(db);
        var withinWindowQuery = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            Status = BillExportStatusFilter.FromInt((int)BillFilterStatus.Deleted),
            DeletedInvoiceDisplayFor = 30,
            RecordsPerPage = 50
        };
        var outsideWindowQuery = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            Status = BillExportStatusFilter.FromInt((int)BillFilterStatus.Deleted),
            DeletedInvoiceDisplayFor = 0,
            RecordsPerPage = 50
        };

        // Act
        Result<List<BillExportRowResponse>> withinWindow = await handler.Handle(withinWindowQuery, CancellationToken.None);
        Result<List<BillExportRowResponse>> outsideWindow = await handler.Handle(outsideWindowQuery, CancellationToken.None);

        // Assert
        withinWindow.Value.Select(r => r.Id).ShouldBe([3]);
        outsideWindow.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Keyword_filter_matches_invoice_no_or_vendor_name()
    {
        // Arrange
        ApplicationDbContext db = await SeedScenarioAsync();
        GetBillsForExportQueryHandler handler = BuildHandler(db);
        var byInvoiceQuery = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            Status = BillExportStatusFilter.FromInt((int)BillStatus.Settled),
            FilterKeyword = "SETTLED",
            RecordsPerPage = 50
        };
        var byVendorQuery = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            FilterKeyword = "Acme",
            RecordsPerPage = 50
        };

        // Act
        Result<List<BillExportRowResponse>> byInvoice = await handler.Handle(byInvoiceQuery, CancellationToken.None);
        Result<List<BillExportRowResponse>> byVendor = await handler.Handle(byVendorQuery, CancellationToken.None);

        // Assert
        byInvoice.Value.Select(r => r.Id).ShouldBe([5]);
        byVendor.Value.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Pagination_returns_a_single_page_in_descending_id_order()
    {
        // Arrange
        ApplicationDbContext db = await SeedScenarioAsync();
        GetBillsForExportQueryHandler handler = BuildHandler(db);
        var page1Query = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            PageNumber = 1,
            RecordsPerPage = 2
        };
        var page2Query = new GetBillsForExportQuery
        {
            CompanyId = BillsTestData.CompanyId,
            PageNumber = 2,
            RecordsPerPage = 2
        };

        // Act
        Result<List<BillExportRowResponse>> page1 = await handler.Handle(page1Query, CancellationToken.None);
        Result<List<BillExportRowResponse>> page2 = await handler.Handle(page2Query, CancellationToken.None);

        // Assert
        page1.Value.Select(r => r.Id).ShouldBe([5, 2]);
        page2.Value.Select(r => r.Id).ShouldBe([1]);
    }

    // ---------------------------------------------------------------------------------------
    // Shared Arrange helpers
    // ---------------------------------------------------------------------------------------
    private static async Task<ApplicationDbContext> SeedScenarioAsync()
    {
        ApplicationDbContext db = TestDbContext.Create();
        await BillsTestData.SeedAsync(db);

        // Approved, not overdue (DueAt > today), with one $100 line, 10% tax, $40 paid
        // -> DueAmount=60, SubTotal=100, Tax=10
        Bill approved = BillsTestData.BuildBill(
            id: 1,
            status: BillStatus.Approved,
            price: 100m,
            dueAt: BusinessToday.AddDays(7),
            created: new DateTime(2026, 5, 10));
        await BillsTestData.AddBillWithLineAsync(db, approved, lineQty: 1m, linePrice: 100m, taxRate: 10m, paidAmount: 40m);

        // Approved + overdue (DueAt <= today)
        Bill overdue = BillsTestData.BuildBill(
            id: 2,
            status: BillStatus.Approved,
            price: 250m,
            dueAt: BusinessToday.AddDays(-3),
            created: new DateTime(2026, 4, 1));
        await BillsTestData.AddBillWithLineAsync(db, overdue, lineQty: 5m, linePrice: 50m, taxRate: 0m);

        // Deleted bill (deleted today)
        Bill deleted = BillsTestData.BuildBill(
            id: 3,
            status: BillStatus.Deleted,
            price: 80m,
            created: new DateTime(2026, 5, 1),
            deleted: BusinessToday);
        await BillsTestData.AddBillWithLineAsync(db, deleted, lineQty: 1m, linePrice: 80m, taxRate: 0m);

        // Archived bill
        Bill archived = BillsTestData.BuildBill(
            id: 4,
            status: BillStatus.Approved,
            price: 999m,
            created: new DateTime(2025, 12, 1),
            isArchive: true);
        await BillsTestData.AddBillWithLineAsync(db, archived, lineQty: 1m, linePrice: 999m, taxRate: 0m);

        // Settled bill
        Bill settled = BillsTestData.BuildBill(
            id: 5,
            status: BillStatus.Settled,
            price: 60m,
            created: new DateTime(2026, 5, 15),
            invoiceNo: "INV-SETTLED");
        await BillsTestData.AddBillWithLineAsync(db, settled, lineQty: 2m, linePrice: 30m, taxRate: 0m, paidAmount: 60m);

        return db;
    }

    private static GetBillsForExportQueryHandler BuildHandler(ApplicationDbContext db) =>
        new(db, new FakeBusinessClock(BusinessToday));
}
