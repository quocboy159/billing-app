namespace Application.Abstractions.Services;

/// <summary>
/// Encapsulates the plan / subscription / feature-master lookup that
/// <c>dbo.IsPlanAllowBulkTransactionsV2</c> performs up front. Splitting this out lets
/// <see cref="IPlanPolicy"/> stay focused on the count-vs-limit decision and lets the
/// lookup be replaced (real DB query, in-memory test double, cache) independently.
/// </summary>
public interface IPlanFeatureLookup
{
    Task<PlanFeatureResolution> ResolveAsync(
        int companyId,
        int featureId,
        CancellationToken cancellationToken = default);
}
