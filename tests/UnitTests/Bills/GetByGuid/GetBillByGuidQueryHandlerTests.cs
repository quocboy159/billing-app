using Application.Bills.GetByGuid;
using Domain.Bills;
using Infrastructure.Database;
using SharedKernel;
using UnitTests.Infrastructure;

namespace UnitTests.Bills.GetByGuid;

public sealed class GetBillByGuidQueryHandlerTests
{
    [Fact]
    public async Task Returns_header_lines_taxes_and_vendor_with_DueAmount_minus_payments()
    {
        // Arrange
        ApplicationDbContext db = TestDbContext.Create();
        await BillsTestData.SeedAsync(db);

        Bill bill = BillsTestData.BuildBill(id: 1, price: 200m, invoiceNo: "INV-001");
        await BillsTestData.AddBillWithLineAsync(
            db, bill, lineQty: 2m, linePrice: 50m, taxRate: 10m, paidAmount: 30m);

        var handler = new GetBillByGuidQueryHandler(db);
        var query = new GetBillByGuidQuery { Guid = bill.Guid, CompanyId = BillsTestData.CompanyId };

        // Act
        Result<BillDetailResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        BillDetailResponse detail = result.Value;
        detail.Header.Id.ShouldBe(1);
        detail.Header.InvoiceNo.ShouldBe("INV-001");
        detail.Header.Price.ShouldBe(200m);
        detail.Header.DueAmount.ShouldBe(170m);
        detail.Header.IsAutoCreated.ShouldBeFalse();

        detail.Lines.Count.ShouldBe(1);
        detail.Lines[0].ProductName.ShouldBe(BillsTestData.ProductName);
        detail.Lines[0].SubHeadName.ShouldBe(BillsTestData.SubHeadName);
        detail.Lines[0].TransactionHeadName.ShouldBe(BillsTestData.ProductTransactionHeadName);
        detail.Lines[0].Quantity.ShouldBe(2m);

        detail.Taxes.Count.ShouldBe(1);
        detail.Taxes[0].Rate.ShouldBe(10m);

        detail.Vendor.ShouldNotBeNull();
        detail.Vendor.VendorName.ShouldBe(BillsTestData.VendorName);
    }

    [Fact]
    public async Task Sets_IsAutoCreated_true_when_bill_is_mapped_to_RecurringBill()
    {
        // Arrange
        ApplicationDbContext db = TestDbContext.Create();
        await BillsTestData.SeedAsync(db);

        Bill bill = BillsTestData.BuildBill(id: 2);
        await BillsTestData.AddBillWithLineAsync(db, bill);

        db.RecurringBillMaps.Add(new RecurringBillMap { BillId = bill.Id });
        await db.SaveChangesAsync();

        var handler = new GetBillByGuidQueryHandler(db);
        var query = new GetBillByGuidQuery { Guid = bill.Guid, CompanyId = BillsTestData.CompanyId };

        // Act
        Result<BillDetailResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Header.IsAutoCreated.ShouldBeTrue();
    }

    [Fact]
    public async Task Returns_DueAmount_equal_to_Price_when_no_payments()
    {
        // Arrange
        ApplicationDbContext db = TestDbContext.Create();
        await BillsTestData.SeedAsync(db);

        Bill bill = BillsTestData.BuildBill(id: 3, price: 75m);
        await BillsTestData.AddBillWithLineAsync(db, bill);

        var handler = new GetBillByGuidQueryHandler(db);
        var query = new GetBillByGuidQuery { Guid = bill.Guid, CompanyId = BillsTestData.CompanyId };

        // Act
        Result<BillDetailResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Header.DueAmount.ShouldBe(75m);
    }

    [Fact]
    public async Task Returns_NotFound_when_guid_does_not_match()
    {
        // Arrange
        ApplicationDbContext db = TestDbContext.Create();
        await BillsTestData.SeedAsync(db);

        var handler = new GetBillByGuidQueryHandler(db);
        var query = new GetBillByGuidQuery { Guid = Guid.NewGuid(), CompanyId = BillsTestData.CompanyId };

        // Act
        Result<BillDetailResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Bills.NotFound");
    }

    [Fact]
    public async Task Returns_NotFound_when_companyId_does_not_match()
    {
        // Arrange
        ApplicationDbContext db = TestDbContext.Create();
        await BillsTestData.SeedAsync(db);

        Bill bill = BillsTestData.BuildBill(id: 4);
        await BillsTestData.AddBillWithLineAsync(db, bill);

        var handler = new GetBillByGuidQueryHandler(db);
        var query = new GetBillByGuidQuery { Guid = bill.Guid, CompanyId = 999 };

        // Act
        Result<BillDetailResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}
