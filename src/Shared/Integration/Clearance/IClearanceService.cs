public interface IClearanceService
{
    Task<ClearanceResponse> CheckClearance(ClearanceRequest request);
    Task<ClearanceResponse> GetClearanceStatus(string clearanceId);
    Task<List<ClearanceHistory>> GetClearanceHistory(string entityId, string entityType);
    Task<ClearanceResponse> UpdateClearance(string clearanceId, ClearanceUpdateRequest request);
    Task<bool> CancelClearance(string clearanceId, string reason);
    Task<List<ClearanceRule>> GetClearanceRules(string lineOfBusiness, string state);
    Task<ClearanceValidation> ValidateClearanceRequest(ClearanceRequest request);
    Task<List<ClearanceBlocker>> GetActiveBlockers(string entityId, string entityType);
}

public class ClearanceRequest
{
    public string EntityId { get; set; }
    public string EntityType { get; set; }
    public string LineOfBusiness { get; set; }
    public string State { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal Amount { get; set; }
    public string RequestedBy { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
    public ClearanceOptions Options { get; set; }
}

public class ClearanceResponse
{
    public string ClearanceId { get; set; }
    public string EntityId { get; set; }
    public string EntityType { get; set; }
    public ClearanceStatus Status { get; set; }
    public List<ClearanceBlocker> Blockers { get; set; }
    public List<ClearanceWarning> Warnings { get; set; }
    public DateTime RequestedDate { get; set; }
    public string RequestedBy { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string ProcessedBy { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public Dictionary<string, object> ResponseData { get; set; }
}

public class ClearanceBlocker
{
    public string BlockerId { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public string Severity { get; set; }
    public bool IsOverridable { get; set; }
    public string OverrideLevel { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public Dictionary<string, object> BlockerData { get; set; }
}

public class ClearanceWarning
{
    public string WarningId { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public Dictionary<string, object> WarningData { get; set; }
}

public class ClearanceHistory
{
    public string ClearanceId { get; set; }
    public DateTime RequestedDate { get; set; }
    public string RequestedBy { get; set; }
    public ClearanceStatus Status { get; set; }
    public List<ClearanceBlocker> Blockers { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public class ClearanceUpdateRequest
{
    public List<BlockerOverride> Overrides { get; set; }
    public List<DocumentReference> SupportingDocuments { get; set; }
    public string Comments { get; set; }
    public string UpdatedBy { get; set; }
}

public class BlockerOverride
{
    public string BlockerId { get; set; }
    public string Reason { get; set; }
    public string ApprovedBy { get; set; }
    public DateTime ApprovalDate { get; set; }
    public Dictionary<string, object> OverrideData { get; set; }
}

public class ClearanceRule
{
    public string RuleId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public string Severity { get; set; }
    public bool IsActive { get; set; }
    public string Expression { get; set; }
    public string ErrorMessage { get; set; }
    public bool IsOverridable { get; set; }
    public string OverrideLevel { get; set; }
}

public class ClearanceOptions
{
    public bool BypassCache { get; set; } = false;
    public bool IncludeHistory { get; set; } = false;
    public bool ValidateOnly { get; set; } = false;
    public int ExpirationHours { get; set; } = 24;
    public Dictionary<string, object> CustomOptions { get; set; }
}

public enum ClearanceStatus
{
    Pending,
    InProgress,
    Cleared,
    Blocked,
    Override,
    Expired,
    Cancelled,
    Error
} 