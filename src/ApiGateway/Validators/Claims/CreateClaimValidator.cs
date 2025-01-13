using FluentValidation;
using System;
using System.Collections.Generic;

public class CreateClaimValidator : AbstractValidator<CreateClaimRequest>
{
    public CreateClaimValidator()
    {
        RuleFor(x => x.PolicyId)
            .NotEmpty()
            .Matches("^P[0-9]{6}$")
            .WithMessage("Invalid policy ID format");

        RuleFor(x => x.DateOfLoss)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Date of loss cannot be in the future");

        RuleFor(x => x.LossDescription)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(2000);

        RuleFor(x => x.Type)
            .IsInEnum()
            .NotEqual(ClaimType.Other)
            .When(x => x.LossDescription.Length < 50)
            .WithMessage("Detailed description required for 'Other' claim types");

        RuleFor(x => x.LossLocations)
            .NotEmpty()
            .Must(x => x.Count <= 5)
            .WithMessage("Maximum 5 loss locations allowed");

        RuleFor(x => x.Claimants)
            .NotEmpty()
            .Must(x => x.Count <= 10)
            .WithMessage("Maximum 10 claimants allowed");

        RuleForEach(x => x.Claimants).SetValidator(new ClaimantValidator());

        RuleFor(x => x.EstimatedLoss)
            .GreaterThan(0)
            .LessThan(10000000)
            .WithMessage("Estimated loss must be between $0 and $10,000,000");

        RuleForEach(x => x.Documents)
            .SetValidator(new ClaimDocumentMetadataValidator());
    }
} 