using FluentValidation;

namespace Application.Reports.ProfitLoss;

public sealed class GetProfitLossReportQueryValidator : AbstractValidator<GetProfitLossReportQuery>
{
    public GetProfitLossReportQueryValidator()
    {
        RuleFor(q => q.CompanyId).GreaterThan(0);
        RuleFor(q => q).Must(q => q.StartDate1 <= q.EndDate1)
            .WithErrorCode("Reports.InvalidPeriod1")
            .WithMessage("StartDate1 must be less than or equal to EndDate1.");
        When(q => q.StartDate2.HasValue || q.EndDate2.HasValue, () =>
        {
            RuleFor(q => q).Must(q => q.StartDate2.HasValue && q.EndDate2.HasValue && q.StartDate2.Value <= q.EndDate2.Value)
                .WithErrorCode("Reports.InvalidPeriod2")
                .WithMessage("StartDate2 and EndDate2 must both be supplied and StartDate2 <= EndDate2.");
        });
    }
}
