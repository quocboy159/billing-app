using Application.Bills.Create;
using Domain.Activities;
using Domain.Bills;
using Domain.Companies;
using Infrastructure.Database;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using UnitTests.Infrastructure;

namespace UnitTests.Bills.Create;

public sealed class CreateBillCommandHandlerTests
{
    private const string CallingUser = "alice";
    private static readonly DateTime Now = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Creates_bill_with_existing_vendor_and_logs_activity()
    {
        // Arrange
        (CreateBillCommandHandler handler, ApplicationDbContext db, _, FakeActivityLogger activity) =
            await BuildHandlerAsync();
        CreateBillCommand command = BuildCommand();

        // Act
        Result<CreateBillResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBeGreaterThan(0);
        result.Value.InvoiceNo.ShouldBe("BILL-1");

        Bill stored = await db.Bills.SingleAsync();
        stored.CompanyId.ShouldBe(BillsTestData.CompanyId);
        stored.Status.ShouldBe(BillStatus.Draft);
        stored.HeadTransactionVendorId.ShouldBe(BillsTestData.VendorHeadTransactionId);

        (await db.BillTransactions.CountAsync()).ShouldBe(1);
        (await db.BillTransactionTaxes.CountAsync()).ShouldBe(1);

        activity.Calls.ShouldContain(c =>
            c.TableType == ActivityTableType.Bill &&
            c.Action == ActivityActionType.BillCreated &&
            c.UserName == CallingUser);
    }

    [Fact]
    public async Task Creates_new_vendor_when_supplied_and_no_HeadTransactionVendorId()
    {
        // Arrange
        (CreateBillCommandHandler handler, ApplicationDbContext db, _, _) = await BuildHandlerAsync();
        var newVendor = new VendorInput
        {
            VendorName = "New Vendor Co",
            Currency = "USD",
            Email = "ops@newvendor.example"
        };
        CreateBillCommand command = BuildCommand(vendorHeadId: 0, newVendor: newVendor);

        // Act
        Result<CreateBillResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await db.Vendors.CountAsync(v => v.VendorName == "New Vendor Co")).ShouldBe(1);
        (await db.HeadTransactions.CountAsync(h => h.Name == "New Vendor Co")).ShouldBe(1);
    }

    [Fact]
    public async Task Returns_conflict_when_vendor_name_already_exists_for_company()
    {
        // Arrange
        (CreateBillCommandHandler handler, ApplicationDbContext db, _, _) = await BuildHandlerAsync();
        CreateBillCommand command = BuildCommand(
            vendorHeadId: 0,
            newVendor: new VendorInput { VendorName = BillsTestData.VendorName });

        // Act
        Result<CreateBillResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Bills.VendorAlreadyExists");
        (await db.Bills.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Returns_problem_when_plan_disallows_bill_creation()
    {
        // Arrange
        (CreateBillCommandHandler handler, ApplicationDbContext db, FakePlanPolicy plan, _) =
            await BuildHandlerAsync();
        plan.Resolver = (_, type) =>
            type == (int)PlanFeature.Bill ? "Plan limit reached" : null;
        CreateBillCommand command = BuildCommand();

        // Act
        Result<CreateBillResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Bills.PlanNotAllowed");
        result.Error.Description.ShouldBe("Plan limit reached");
        (await db.Bills.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Returns_problem_when_multi_currency_disallowed_and_currencies_differ()
    {
        // Arrange
        (CreateBillCommandHandler handler, ApplicationDbContext db, FakePlanPolicy plan, _) =
            await BuildHandlerAsync();
        plan.Resolver = (_, type) =>
            type == (int)PlanFeature.MultiCurrencyBill ? "Upgrade to multi-currency" : null;
        CreateBillCommand command = BuildCommand(currency: "EUR");

        // Act
        Result<CreateBillResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Bills.PlanNotAllowed");
        result.Error.Description.ShouldBe("Upgrade to multi-currency");
        (await db.Bills.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Returns_problem_when_vendor_does_not_belong_to_company()
    {
        // Arrange
        (CreateBillCommandHandler handler, _, _, _) = await BuildHandlerAsync();
        CreateBillCommand command = BuildCommand(vendorHeadId: 99_999);

        // Act
        Result<CreateBillResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Bills.VendorInvalid");
    }

    // ---------------------------------------------------------------------------------------
    // Shared Arrange helpers
    // ---------------------------------------------------------------------------------------
    private static async Task<(CreateBillCommandHandler Handler, ApplicationDbContext Db, FakePlanPolicy Plan, FakeActivityLogger Activity)>
        BuildHandlerAsync()
    {
        ApplicationDbContext db = TestDbContext.Create();
        await BillsTestData.SeedAsync(db);

        var clock = new FakeDateTimeProvider(Now);
        var plan = new FakePlanPolicy();
        var numbers = new FakeBillNumberGenerator(db);
        var activity = new FakeActivityLogger();

        // Production repository implementations work fine against the InMemory DbContext;
        // the UnitOfWork wraps SaveChanges + BeginTransaction (no-op on InMemory).
        var bills = new BillRepository(db);
        var vendors = new VendorRepository(db);
        var headTransactions = new HeadTransactionRepository(db);
        var companies = new CompanyRepository(db);
        var unitOfWork = new UnitOfWork(db);

        var handler = new CreateBillCommandHandler(
            bills, vendors, headTransactions, companies, unitOfWork,
            plan, numbers, activity, clock);
        return (handler, db, plan, activity);
    }

    private static CreateBillCommand BuildCommand(
        int? vendorHeadId = BillsTestData.VendorHeadTransactionId,
        string? currency = BillsTestData.CompanyCurrency,
        VendorInput? newVendor = null,
        decimal price = 200m,
        bool withTax = true) => new()
        {
            UserName = CallingUser,
            Bill = new BillHeaderInput
            {
                CompanyId = BillsTestData.CompanyId,
                HeadTransactionVendorId = vendorHeadId ?? 0,
                Currency = currency,
                Price = price,
                ExchangeRate = 1m,
                BillAt = new DateTime(2026, 5, 22),
                DueAt = new DateTime(2026, 6, 22)
            },
            Transactions =
            [
                new BillTransactionInput
                {
                    Counter = 1,
                    ProductId = BillsTestData.ProductId,
                    ProductTransactionHeadId = BillsTestData.ProductTransactionHeadId,
                    Description = "widget x2",
                    Quantity = 2m,
                    Price = 100m
                }
            ],
            Taxes = withTax
                ?
                [
                    new BillTransactionTaxInput
                    {
                        CounterTranId = 1,
                        TransactionHeadTaxId = 1,
                        Name = "VAT",
                        Rate = 10m
                    }
                ]
                : [],
            Vendor = newVendor
        };
}
