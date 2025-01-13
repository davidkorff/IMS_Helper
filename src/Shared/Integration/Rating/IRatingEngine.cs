public interface IRatingEngine
{
    Task<RatingResponse> RatePolicy(RatingRequest request);
    Task<List<RatingFactor>> GetRatingFactors(string lineOfBusiness, string state);
    Task<RatingWorksheet> GetRatingWorksheet(string quoteId);
    Task<List<RatingRule>> GetActiveRatingRules(string lineOfBusiness, string state);
    Task<decimal> CalculatePremium(string quoteId, PremiumCalculationRequest request);
    Task<List<Discount>> GetAvailableDiscounts(string quoteId);
    Task<RatingValidationResult> ValidateRatingData(string quoteId, Dictionary<string, object> data);
    Task<ComparisonResult> CompareRates(string quoteId, string comparisonQuoteId);
}

public class RatingRequest
{
    public string QuoteId { get; set; }
    public string LineOfBusiness { get; set; }
    public string State { get; set; }
    public DateTime EffectiveDate { get; set; }
    public Dictionary<string, object> RatingData { get; set; }
    public List<string> RequestedDiscounts { get; set; }
    public string RatedBy { get; set; }
    public RatingOptions Options { get; set; }
}

public class RatingResponse
{
    public string RatingId { get; set; }
    public string QuoteId { get; set; }
    public decimal BasePremium { get; set; }
    public decimal TotalPremium { get; set; }
    public List<PremiumComponent> PremiumComponents { get; set; }
    public List<AppliedDiscount> AppliedDiscounts { get; set; }
    public List<RatingWarning> Warnings { get; set; }
    public List<RatingError> Errors { get; set; }
    public RatingWorksheet Worksheet { get; set; }
    public DateTime RatingDate { get; set; }
    public string RatedBy { get; set; }
}

public class RatingFactor
{
    public string FactorId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public List<FactorValue> Values { get; set; }
    public string DataType { get; set; }
    public bool IsRequired { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class FactorValue
{
    public string Code { get; set; }
    public string Description { get; set; }
    public decimal Value { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<string> ApplicableStates { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class RatingWorksheet
{
    public string WorksheetId { get; set; }
    public string QuoteId { get; set; }
    public List<RatingStep> Steps { get; set; }
    public List<PremiumComponent> Components { get; set; }
    public List<AppliedFactor> AppliedFactors { get; set; }
    public List<string> Messages { get; set; }
}

public class RatingStep
{
    public string StepId { get; set; }
    public string Description { get; set; }
    public decimal InputValue { get; set; }
    public decimal OutputValue { get; set; }
    public string Operation { get; set; }
    public List<AppliedFactor> Factors { get; set; }
    public Dictionary<string, object> StepData { get; set; }
}

public class RatingRule
{
    public string RuleId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Expression { get; set; }
    public string Category { get; set; }
    public decimal? AdjustmentFactor { get; set; }
    public string ErrorMessage { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public class PremiumCalculationRequest
{
    public Dictionary<string, object> RatingData { get; set; }
    public List<string> RequestedDiscounts { get; set; }
    public bool IncludeWorksheet { get; set; }
    public RatingOptions Options { get; set; }
}

public class Discount
{
    public string DiscountId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal MaximumPercentage { get; set; }
    public List<string> EligibilityRules { get; set; }
    public bool RequiresDocumentation { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class RatingOptions
{
    public bool IncludeWorksheet { get; set; } = false;
    public bool ValidateOnly { get; set; } = false;
    public bool ApplyAllEligibleDiscounts { get; set; } = false;
    public string RateVersion { get; set; }
    public Dictionary<string, object> OverrideValues { get; set; }
}

public class PremiumComponent
{
    public string ComponentId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; }
    public bool IsTaxable { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class AppliedDiscount
{
    public string DiscountId { get; set; }
    public string Name { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public string AppliedTo { get; set; }
    public Dictionary<string, object> Details { get; set; }
}

public class AppliedFactor
{
    public string FactorId { get; set; }
    public string Name { get; set; }
    public decimal Value { get; set; }
    public string Category { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class RatingValidationResult
{
    public bool IsValid { get; set; }
    public List<RatingError> Errors { get; set; }
    public List<RatingWarning> Warnings { get; set; }
    public Dictionary<string, List<string>> ValidationMessages { get; set; }
}

public class RatingError
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string Field { get; set; }
    public string Category { get; set; }
    public Dictionary<string, object> Details { get; set; }
}

public class RatingWarning
{
    public string WarningCode { get; set; }
    public string Message { get; set; }
    public string Field { get; set; }
    public string Category { get; set; }
    public Dictionary<string, object> Details { get; set; }
}

public class ComparisonResult
{
    public string BaseQuoteId { get; set; }
    public string ComparisonQuoteId { get; set; }
    public decimal PremiumDifference { get; set; }
    public decimal PercentageDifference { get; set; }
    public List<ComponentComparison> ComponentComparisons { get; set; }
    public List<FactorComparison> FactorComparisons { get; set; }
    public List<DiscountComparison> DiscountComparisons { get; set; }
}

public class ComponentComparison
{
    public string ComponentId { get; set; }
    public string Name { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal ComparisonAmount { get; set; }
    public decimal Difference { get; set; }
    public decimal PercentageDifference { get; set; }
}

public class FactorComparison
{
    public string FactorId { get; set; }
    public string Name { get; set; }
    public decimal BaseValue { get; set; }
    public decimal ComparisonValue { get; set; }
    public decimal Difference { get; set; }
}

public class DiscountComparison
{
    public string DiscountId { get; set; }
    public string Name { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal ComparisonAmount { get; set; }
    public decimal Difference { get; set; }
} 