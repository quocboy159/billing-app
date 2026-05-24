using FluentValidation;

namespace Application.Bills.GetForExport;

public sealed class GetBillsForExportQueryValidator : AbstractValidator<GetBillsForExportQuery>
{
    public GetBillsForExportQueryValidator()
    {
        RuleFor(q => q.CompanyId).GreaterThan(0);
        RuleFor(q => q.PageNumber).GreaterThan(0);
        RuleFor(q => q.RecordsPerPage).InclusiveBetween(1, 500);
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithErrorCode("Bills.InvalidDateRange")
            .WithMessage("From must be less than or equal to To.");
    }
}
