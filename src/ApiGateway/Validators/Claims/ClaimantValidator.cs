using FluentValidation;

public class ClaimantValidator : AbstractValidator<ClaimantInfo>
{
    public ClaimantValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-zA-Z\\s\\-']+$")
            .WithMessage("First name can only contain letters, spaces, hyphens and apostrophes");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-zA-Z\\s\\-']+$")
            .WithMessage("Last name can only contain letters, spaces, hyphens and apostrophes");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .Matches("^\\+?[1-9]\\d{1,14}$")
            .WithMessage("Phone number must be in E.164 format");

        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Address)
            .SetValidator(new AddressValidator());
    }
} 