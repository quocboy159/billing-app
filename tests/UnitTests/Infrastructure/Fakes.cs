using Application.Abstractions.Data;
using Application.Abstractions.Services;
using Domain.Activities;
using Domain.Companies;
using SharedKernel;

namespace UnitTests.Infrastructure;

internal sealed class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
}

internal sealed class FakePlanPolicy : IPlanPolicy
{
    public Func<int, int, string?> Resolver { get; set; } = (_, _) => null;

    public Task<string?> CheckTransactionAllowedAsync(
        int companyId,
        int transactionType,
        int extra = 0,
        string context = "",
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Resolver(companyId, transactionType));

    public Task<string?> CheckBulkTransactionAllowedAsync(
        int companyId,
        int featureId,
        int moduleId,
        string? userId,
        int bulkItemsCount,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Resolver(companyId, featureId));
}

internal sealed class FakeBusinessClock(DateTime today) : IBusinessClock
{
    public DateTime Today { get; set; } = today;

    public Task<DateTime> CurrentDateAsync(int companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Today);
}

internal sealed class FakeBillNumberGenerator(IApplicationDbContext context) : IBillNumberGenerator
{
    public string Prefix { get; set; } = "BILL-";

    public async Task<BillNumber> NextAsync(int companyId, CancellationToken cancellationToken = default)
    {
        PurchaseCounter? counter = context.PurchaseCounters
            .SingleOrDefault(c => c.CompanyId == companyId);

        int next = (counter?.BillNo ?? 0) + 1;

        if (counter is null)
        {
            context.PurchaseCounters.Add(new PurchaseCounter { CompanyId = companyId, BillNo = next });
        }
        else
        {
            counter.BillNo = next;
        }

        await Task.CompletedTask;
        return new BillNumber(next, Prefix + next);
    }
}

internal sealed class FakeActivityLogger : IActivityLogger
{
    public List<(ActivityTableType TableType, ActivityActionType Action, int CompanyId, int TableTransactionId, string? UserName)> Calls { get; } = [];

    public Task LogAsync(
        int companyId,
        int tableTransactionId,
        string? tableTransactionValue,
        ActivityTableType tableType,
        ActivityActionType actionType,
        int transactionHeadId,
        string? remarks,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((tableType, actionType, companyId, tableTransactionId, userName));
        return Task.CompletedTask;
    }
}
