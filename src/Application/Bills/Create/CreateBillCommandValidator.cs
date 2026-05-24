using FluentValidation;

namespace Application.Bills.Create;

public sealed class CreateBillCommandValidator : AbstractValidator<CreateBillCommand>
{
    public CreateBillCommandValidator()
    {
        RuleFor(c => c.Bill).NotNull();
        RuleFor(c => c.Bill.CompanyId).GreaterThan(0);
        RuleFor(c => c.Bill.Price).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Transactions).NotEmpty();
        RuleForEach(c => c.Transactions).ChildRules(t =>
        {
            t.RuleFor(x => x.ProductId).GreaterThan(0);
            t.RuleFor(x => x.ProductTransactionHeadId).GreaterThan(0);
            t.RuleFor(x => x.Quantity).GreaterThan(0);
            t.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        });
        RuleFor(c => c)
            .Must(c => c.Bill.HeadTransactionVendorId > 0 || c.Vendor is not null)
            .WithErrorCode("Bills.VendorRequired")
            .WithMessage("Either HeadTransactionVendorId or a new Vendor must be supplied.");
        When(c => c.Vendor is not null, () =>
        {
            RuleFor(c => c.Vendor!.VendorName).NotEmpty().MaximumLength(100);
        });
    }
}
