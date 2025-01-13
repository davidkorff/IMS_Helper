using FluentValidation;

public class UpdateClaimStatusValidator : AbstractValidator<UpdateClaimStatusRequest>
{
    public UpdateClaimStatusValidator()
    {
        RuleFor(x => x.NewStatus)
            .IsInEnum()
            .NotEqual(ClaimStatus.New)
            .WithMessage("Cannot manually set status to New");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(500)
            .When(x => x.NewStatus == ClaimStatus.Denied || x.NewStatus == ClaimStatus.Closed)
            .WithMessage("Reason required for Denied or Closed status");

        RuleFor(x => x.ReserveAmount)
            .GreaterThan(0)
            .When(x => x.NewStatus == ClaimStatus.Approved)
            .WithMessage("Reserve amount required when approving claim");

        RuleFor(x => x.PaymentAmount)
            .GreaterThan(0)
            .LessThanOrEqualTo(x => x.ReserveAmount ?? 0)
            .When(x => x.NewStatus == ClaimStatus.InPayment)
            .WithMessage("Payment amount must be greater than 0 and not exceed reserve amount");
    }
} 