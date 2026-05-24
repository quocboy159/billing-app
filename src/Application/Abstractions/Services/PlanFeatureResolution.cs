namespace Application.Abstractions.Services;

/// <summary>
/// The output of resolving a (company, feature) pair against the plan / subscription /
/// feature-master tables. Captures the three values the SP collects up front:
///   - <c>MaxLimit</c>      = SP's @MaxLimits
///   - <c>LimitCheckingDate</c> = SP's @LimitCheckingDate
///   - <c>AlertMessage</c>   = SP's @AlertMessage
/// </summary>
public sealed record PlanFeatureResolution(
    int MaxLimit,
    DateTime LimitCheckingDate,
    string? AlertMessage)
{
    /// <summary>
    /// Convenience for the "feature not enabled for this company" branch
    /// (no active subscription AND no matching free plan).
    /// </summary>
    public static PlanFeatureResolution Unavailable(string? alert) =>
        new(MaxLimit: -1, LimitCheckingDate: DateTime.MinValue, AlertMessage: alert);
}
