using FluentValidation;
using System;

public class ClaimDocumentMetadataValidator : AbstractValidator<ClaimDocumentMetadata>
{
    public ClaimDocumentMetadataValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.DocumentDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Document date cannot be in the future");

        When(x => x.Type == ClaimDocumentType.PoliceReport, () =>
        {
            RuleFor(x => x.Description)
                .MinimumLength(20)
                .WithMessage("Police report requires detailed description");
        });

        When(x => x.Type == ClaimDocumentType.Estimate, () =>
        {
            RuleFor(x => x.DocumentDate)
                .Must(date => date >= DateTime.UtcNow.AddDays(-30))
                .WithMessage("Estimates must be less than 30 days old");
        });
    }
} 