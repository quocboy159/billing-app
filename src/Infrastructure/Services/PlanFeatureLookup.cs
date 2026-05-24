using Application.Abstractions.Services;
using Domain.Companies;

namespace Infrastructure.Services;

/// <summary>
/// Stub implementation -- the original SP joins PlanFeatureCompanies / PlanSubscriptions /
/// PlanFeatureAlerts / PlanFeatureMasters / PlanFeatures / Plans / PlanFeatureLimits. Those
/// tables aren't in the Bills exercise scope, so we always resolve to "unlimited" (= every
/// feature is allowed without counting). Swap this for a real DB-backed lookup once the
/// subscription model is ported.
/// </summary>
internal sealed class PlanFeatureLookup : IPlanFeatureLookup
{
    public Task<PlanFeatureResolution> ResolveAsync(
        int companyId,
        int featureId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PlanFeatureResolution(
            MaxLimit: PlanFeatureLimits.Unlimited,
            LimitCheckingDate: DateTime.MinValue,
            AlertMessage: null));
}
