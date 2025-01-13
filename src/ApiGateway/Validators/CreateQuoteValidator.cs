using FluentValidation;
using System;

public class CreateQuoteValidator : AbstractValidator<CreateQuoteRequest>
{
    public CreateQuoteValidator()
    {
        RuleFor(x => x.ProgramCode)
            .NotEmpty()
            .MaximumLength(10);

        RuleFor(x => x.Insured)
            .NotNull()
            .SetValidator(new InsuredInfoValidator());

        RuleFor(x => x.Coverage)
            .NotNull()
            .SetValidator(new CoverageInfoValidator());

        RuleFor(x => x.Locations)
            .NotEmpty()
            .ForEach(location => location.SetValidator(new LocationInfoValidator()));

        RuleFor(x => x.Premium)
            .NotNull()
            .SetValidator(new PremiumInfoValidator());
    }
}

public class InsuredInfoValidator : AbstractValidator<InsuredInfo>
{
    public InsuredInfoValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\d{10}$");
        RuleFor(x => x.Address).SetValidator(new AddressInfoValidator());
    }
}

public class CoverageInfoValidator : AbstractValidator<CoverageInfo>
{
    public CoverageInfoValidator()
    {
        RuleFor(x => x.LineOfBusiness).NotEmpty();
        RuleFor(x => x.EffectiveDate).NotEmpty().GreaterThan(DateTime.UtcNow);
        RuleFor(x => x.ExpirationDate).NotEmpty().GreaterThan(x => x.EffectiveDate);
        RuleFor(x => x.Coverages).NotEmpty();
    }
} 