public interface IRatingService
{
    Task<RatingResponse> RateQuote(string quoteId, RatingRequest request);
    Task<List<RatingOption>> GetRatingOptions(string lineOfBusiness, string state);
    Task<RatingValidationResponse> ValidateRatingData(RatingRequest request);
    Task<byte[]> GenerateRatingWorksheet(string quoteId, string ratingOptionId);
    Task<List<RatingFactor>> GetRatingFactors(string ratingOptionId);
}

public class RatingRequest
{
    public string QuoteId { get; set; }
    public string RatingOptionId { get; set; }
    public Dictionary<string, object> RatingData { get; set; }
    public List<RatingOverride> Overrides { get; set; }
    public string UnderwriterGuid { get; set; }
    public DateTime EffectiveDate { get; set; }
}

public class RatingResponse
{
    public string QuoteId { get; set; }
    public string RatingId { get; set; }
    public decimal BasePremium { get; set; }
    public List<RatingModification> Modifications { get; set; }
    public List<Fee> Fees { get; set; }
    public decimal TotalPremium { get; set; }
    public List<RatingWarning> Warnings { get; set; }
    public DateTime RatedDate { get; set; }
    public string RatedBy { get; set; }
}

public class RatingOption
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<string> SupportedStates { get; set; }
    public List<RatingRequirement> Requirements { get; set; }
}

public class RatingFactor
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string DataType { get; set; }
    public bool Required { get; set; }
    public string DefaultValue { get; set; }
    public List<string> AllowedValues { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class ValidationRules
{
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string RegexPattern { get; set; }
    public List<string> Dependencies { get; set; }
}

public class RatingOverride
{
    public string FactorCode { get; set; }
    public object Value { get; set; }
    public string Reason { get; set; }
    public string ApprovedBy { get; set; }
}

public class RatingModification
{
    public string Type { get; set; }
    public string Description { get; set; }
    public decimal Factor { get; set; }
    public decimal Amount { get; set; }
}

public class Fee
{
    public string Type { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public bool IsOptional { get; set; }
}

public class RatingWarning
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string Severity { get; set; }
}

public class RatingValidationResponse
{
    public bool IsValid { get; set; }
    public List<RatingValidationError> Errors { get; set; }
    public List<RatingWarning> Warnings { get; set; }
}

public class RatingValidationError
{
    public string FactorCode { get; set; }
    public string Message { get; set; }
}

public class RatingRequirement
{
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsMandatory { get; set; }
    public string Description { get; set; }
} 