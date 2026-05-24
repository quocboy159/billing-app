using Domain.Activities;

namespace Application.Abstractions.Services;

public interface IActivityLogger
{
    Task LogAsync(
        int companyId,
        int tableTransactionId,
        string? tableTransactionValue,
        ActivityTableType tableType,
        ActivityActionType actionType,
        int transactionHeadId,
        string? remarks,
        string? userName,
        CancellationToken cancellationToken = default);
}
