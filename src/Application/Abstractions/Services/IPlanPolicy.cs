namespace Application.Abstractions.Services;

/// <summary>
/// Port of the plan-policy SQL functions:
///   - <see cref="CheckTransactionAllowedAsync"/>      = dbo.IsPlanAllowTransactionV2
///   - <see cref="CheckBulkTransactionAllowedAsync"/>  = dbo.IsPlanAllowBulkTransactionsV2
///
/// Both return <c>null</c> when the action is allowed, otherwise the alert message that
/// should bubble back to the user.
/// </summary>
public interface IPlanPolicy
{
    /// <summary>
    /// Single-item plan check. Equivalent to <see cref="CheckBulkTransactionAllowedAsync"/>
    /// with <c>bulkItemsCount = 0</c>.
    /// </summary>
    Task<string?> CheckTransactionAllowedAsync(
        int companyId,
        int transactionType,
        int extra = 0,
        string context = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk plan check. Resolves the company's limit for <paramref name="featureId"/> and
    /// counts current usage in the matching table; returns the alert message when the
    /// projected total (<c>existing + bulkItemsCount</c>) would exceed the limit.
    /// </summary>
    /// <param name="featureId">PlanFeature value (1..24).</param>
    /// <param name="moduleId">Sub-discriminator used by features 2 (Estimate vs Recurring) and 3 (Income vs Expense product).</param>
    /// <param name="userId">Owner check input for feature 18; ignored otherwise.</param>
    /// <param name="bulkItemsCount">How many items the caller wants to add in one shot.</param>
    Task<string?> CheckBulkTransactionAllowedAsync(
        int companyId,
        int featureId,
        int moduleId,
        string? userId,
        int bulkItemsCount,
        CancellationToken cancellationToken = default);
}
