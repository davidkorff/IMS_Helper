using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Integration.Validation.IMSValidationSettings;

public class PolicyRequestValidator : BaseRequestValidator<PolicyRequest>
{
    private readonly ILogger<PolicyRequestValidator> _logger;
    private readonly IStateService _stateService;

    public PolicyRequestValidator(
        ILogger<PolicyRequestValidator> logger,
        IStateService stateService,
        IOptions<IMSValidationSettings> settings)
        : base(logger, settings)
    {
        _logger = logger;
        _stateService = stateService;
    }

    protected override async Task ValidatePropertiesAsync(
        PolicyRequest request,
        ValidationResult result,
        ValidationContext context)
    {
        // Required fields
        ValidateRequired("PolicyNumber", request.PolicyNumber, result);
        ValidateRequired("EffectiveDate", request.EffectiveDate, result);
        ValidateRequired("State", request.State, result);
        ValidateRequired("LineOfBusiness", request.LineOfBusiness, result);

        // Field lengths
        ValidateLength("PolicyNumber", request.PolicyNumber, 20, result);
        ValidateLength("State", request.State, 2, result);
        ValidateLength("LineOfBusiness", request.LineOfBusiness, 10, result);

        // Patterns
        ValidatePattern("PolicyNumber", request.PolicyNumber, 
            @"^[A-Z0-9]{6,20}$",
            "6-20 alphanumeric characters",
            result);
        ValidatePattern("State", request.State,
            @"^[A-Z]{2}$",
            "2 letter state code",
            result);

        // Date validations
        var today = DateTime.Today;
        ValidateDate("EffectiveDate", request.EffectiveDate, result,
            minDate: today,
            maxDate: today.AddYears(1));

        if (request.ExpirationDate.HasValue)
        {
            ValidateDate("ExpirationDate", request.ExpirationDate, result,
                minDate: request.EffectiveDate?.AddDays(1),
                maxDate: request.EffectiveDate?.AddYears(1));
        }

        // Enum validations
        if (!string.IsNullOrEmpty(request.Status))
        {
            ValidateEnum<PolicyStatus>("Status", request.Status, result);
        }
    }

    protected override async Task ValidateBusinessRulesAsync(
        PolicyRequest request,
        ValidationResult result,
        ValidationContext context)
    {
        // Validate state is supported
        var supportedStates = await _stateService.GetSupportedStatesAsync(
            request.LineOfBusiness);
        
        if (!supportedStates.Contains(request.State))
        {
            result.AddError("State", "BUS001", 
                $"State {request.State} is not supported for {request.LineOfBusiness}");
        }

        // Validate policy term
        if (request.ExpirationDate.HasValue && 
            request.EffectiveDate.HasValue)
        {
            var termLength = (request.ExpirationDate.Value - 
                request.EffectiveDate.Value).TotalDays;

            if (termLength < 1 || termLength > 366)
            {
                result.AddError("ExpirationDate", "BUS002", 
                    "Policy term must be between 1 day and 1 year");
            }
        }

        // Additional business rules...
    }

    protected override async Task ValidateCustomRulesAsync(
        PolicyRequest request,
        ValidationResult result,
        ValidationContext context)
    {
        // Custom validation logic specific to the operation
        if (context.Operation == "Bind")
        {
            ValidateRequired("Premium", request.Premium, result);
            ValidateRequired("PaymentMethod", request.PaymentMethod, result);

            if (request.Premium.HasValue)
            {
                ValidateRange("Premium", request.Premium.Value, 
                    0m, 1000000m, result);
            }
        }
    }
} 