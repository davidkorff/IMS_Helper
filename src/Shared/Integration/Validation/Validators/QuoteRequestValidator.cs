using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

public class QuoteRequestValidator : BaseRequestValidator<QuoteRequest>
{
    private readonly ILogger<QuoteRequestValidator> _logger;
    private readonly IReferenceDataService _referenceData;

    public QuoteRequestValidator(
        ILogger<QuoteRequestValidator> logger,
        IReferenceDataService referenceData,
        IOptions<IMSValidationSettings> settings)
        : base(logger, settings)
    {
        _logger = logger;
        _referenceData = referenceData;
    }

    protected override async Task ValidatePropertiesAsync(
        QuoteRequest request,
        ValidationResult result,
        ValidationContext context)
    {
        // Required fields
        ValidateRequired("LineOfBusiness", request.LineOfBusiness, result);
        ValidateRequired("State", request.State, result);
        ValidateRequired("EffectiveDate", request.EffectiveDate, result);
        ValidateRequired("Insured", request.Insured, result);

        if (request.Insured != null)
        {
            ValidateRequired("Insured.Name", request.Insured.Name, result);
            ValidateRequired("Insured.Address", request.Insured.Address, result);
            ValidateRequired("Insured.State", request.Insured.State, result);
            ValidateRequired("Insured.ZipCode", request.Insured.ZipCode, result);
        }

        // Field lengths
        ValidateLength("LineOfBusiness", request.LineOfBusiness, 10, result);
        ValidateLength("State", request.State, 2, result);

        if (request.Insured != null)
        {
            ValidateLength("Insured.Name", request.Insured.Name, 100, result);
            ValidateLength("Insured.Address", request.Insured.Address, 200, result);
            ValidateLength("Insured.State", request.Insured.State, 2, result);
            ValidateLength("Insured.ZipCode", request.Insured.ZipCode, 10, result);
        }

        // Patterns
        ValidatePattern("State", request.State,
            @"^[A-Z]{2}$",
            "2 letter state code",
            result);

        if (request.Insured != null)
        {
            ValidatePattern("Insured.ZipCode", request.Insured.ZipCode,
                @"^\d{5}(-\d{4})?$",
                "5-digit ZIP code or ZIP+4",
                result);

            ValidatePattern("Insured.Email", request.Insured.Email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                "valid email address",
                result);
        }

        // Date validations
        var today = DateTime.Today;
        ValidateDate("EffectiveDate", request.EffectiveDate, result,
            minDate: today,
            maxDate: today.AddYears(1));
    }

    protected override async Task ValidateBusinessRulesAsync(
        QuoteRequest request,
        ValidationResult result,
        ValidationContext context)
    {
        // Validate line of business is active
        var activeLines = await _referenceData.GetActiveLinesOfBusinessAsync();
        if (!activeLines.Contains(request.LineOfBusiness))
        {
            result.AddError("LineOfBusiness", "BUS001", 
                $"Line of business {request.LineOfBusiness} is not active");
        }

        // Validate state is supported for line of business
        var supportedStates = await _referenceData
            .GetSupportedStatesAsync(request.LineOfBusiness);
        if (!supportedStates.Contains(request.State))
        {
            result.AddError("State", "BUS002", 
                $"State {request.State} is not supported for {request.LineOfBusiness}");
        }

        // Validate coverage limits
        if (request.Coverages != null)
        {
            foreach (var coverage in request.Coverages)
            {
                var limits = await _referenceData
                    .GetCoverageLimitsAsync(
                        request.LineOfBusiness,
                        coverage.Code);

                if (coverage.Limit < limits.MinLimit || 
                    coverage.Limit > limits.MaxLimit)
                {
                    result.AddError($"Coverages[{coverage.Code}].Limit", "BUS003",
                        $"Coverage limit must be between {limits.MinLimit:C} and {limits.MaxLimit:C}");
                }
            }
        }
    }
} 