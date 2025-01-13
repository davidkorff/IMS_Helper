public interface IUnderwritingEngine
{
    Task<UnderwritingResponse> EvaluateRules(string quoteId, UnderwritingRequest request);
    Task<List<UnderwritingRule>> GetActiveRules(string lineOfBusiness, string state);
    Task<UnderwritingRuleResult> EvaluateRule(string ruleId, Dictionary<string, object> data);
    Task<UnderwritingResponse> RequestRuleOverride(string quoteId, RuleOverrideRequest request);
    Task<List<UnderwritingAction>> GetAvailableActions(string quoteId);
}

public class UnderwritingRequest
{
    public string QuoteId { get; set; }
    public string LineOfBusiness { get; set; }
    public string State { get; set; }
    public string UnderwriterGuid { get; set; }
    public Dictionary<string, object> RiskData { get; set; }
    public List<string> RuleCategories { get; set; }
    public bool IncludeRecommendations { get; set; }
}

public class UnderwritingResponse
{
    public string QuoteId { get; set; }
    public string EvaluationId { get; set; }
    public DateTime EvaluationDate { get; set; }
    public UnderwritingDecision Decision { get; set; }
    public List<UnderwritingRuleResult> RuleResults { get; set; }
    public List<UnderwritingAction> RequiredActions { get; set; }
    public List<UnderwritingRecommendation> Recommendations { get; set; }
    public string EvaluatedBy { get; set; }
}

public class UnderwritingRule
{
    public string RuleId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public int Severity { get; set; }
    public bool IsActive { get; set; }
    public string Expression { get; set; }
    public UnderwritingAction Action { get; set; }
    public bool AllowOverride { get; set; }
    public List<string> RequiredRoles { get; set; }
    public ValidationMetadata ValidationMetadata { get; set; }
}

public class UnderwritingRuleResult
{
    public string RuleId { get; set; }
    public bool Passed { get; set; }
    public string Message { get; set; }
    public Dictionary<string, object> EvaluatedData { get; set; }
    public UnderwritingAction RequiredAction { get; set; }
    public bool CanOverride { get; set; }
    public List<string> AllowedOverrideRoles { get; set; }
}

public class RuleOverrideRequest
{
    public string QuoteId { get; set; }
    public string RuleId { get; set; }
    public string Reason { get; set; }
    public string UnderwriterGuid { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
}

public class UnderwritingAction
{
    public string ActionId { get; set; }
    public string Name { get; set; }
    public ActionType Type { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public DateTime? DueDate { get; set; }
    public string AssignedTo { get; set; }
    public ActionStatus Status { get; set; }
}

public class UnderwritingRecommendation
{
    public string RecommendationId { get; set; }
    public string Description { get; set; }
    public int Priority { get; set; }
    public string Category { get; set; }
    public Dictionary<string, object> SupportingData { get; set; }
}

public class ValidationMetadata
{
    public List<string> RequiredFields { get; set; }
    public Dictionary<string, string> FieldTypes { get; set; }
    public Dictionary<string, object> DefaultValues { get; set; }
    public Dictionary<string, List<string>> AllowedValues { get; set; }
}

public enum UnderwritingDecision
{
    Approved,
    Declined,
    ReferToUnderwriter,
    PendingAction,
    MoreInformationRequired
}

public enum ActionType
{
    DocumentCollection,
    RiskInspection,
    UnderwriterReview,
    AdditionalInformation,
    RiskMitigation,
    Approval
}

public enum ActionStatus
{
    Pending,
    InProgress,
    Completed,
    Waived,
    Expired
} 