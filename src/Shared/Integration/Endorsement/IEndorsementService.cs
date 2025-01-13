public interface IEndorsementService
{
    Task<EndorsementResponse> CreateEndorsement(EndorsementRequest request);
    Task<EndorsementStatus> GetEndorsementStatus(string endorsementId);
    Task<EndorsementDetails> GetEndorsementDetails(string endorsementId);
    Task<List<EndorsementDocument>> GetEndorsementDocuments(string endorsementId);
    Task<List<EndorsementValidationError>> ValidateEndorsement(string endorsementId);
    Task<string> GeneratePreviewDocuments(string endorsementId);
    Task<EndorsementWorkflow> GetEndorsementWorkflow(string lineOfBusiness, string state);
    Task<List<EndorsementRequirement>> GetPendingRequirements(string endorsementId);
    Task<bool> SubmitRequirement(string endorsementId, EndorsementRequirementSubmission requirement);
    Task<EndorsementResponse> ProcessEndorsement(string endorsementId, EndorsementProcessRequest request);
    Task<List<AvailableEndorsement>> GetAvailableEndorsements(string policyNumber);
    Task<EndorsementComparison> CompareEndorsements(string baseEndorsementId, string comparisonEndorsementId);
}

public class EndorsementRequest
{
    public string PolicyNumber { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string EndorsementType { get; set; }
    public Dictionary<string, object> Changes { get; set; }
    public PaymentInfo PaymentInfo { get; set; }
    public List<SignedDocument> SignedDocuments { get; set; }
    public string RequestedBy { get; set; }
    public string Reason { get; set; }
    public EndorsementOptions Options { get; set; }
}

public class EndorsementResponse
{
    public string EndorsementId { get; set; }
    public string PolicyNumber { get; set; }
    public EndorsementStatus Status { get; set; }
    public decimal PremiumChange { get; set; }
    public List<EndorsementDocument> Documents { get; set; }
    public List<EndorsementRequirement> PendingRequirements { get; set; }
    public List<EndorsementValidationError> ValidationErrors { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string ProcessedBy { get; set; }
    public Dictionary<string, object> ResponseData { get; set; }
}

public class EndorsementDetails
{
    public string EndorsementId { get; set; }
    public string PolicyNumber { get; set; }
    public string EndorsementType { get; set; }
    public DateTime EffectiveDate { get; set; }
    public EndorsementStatus Status { get; set; }
    public decimal PremiumChange { get; set; }
    public string RequestedBy { get; set; }
    public DateTime RequestedDate { get; set; }
    public string ProcessedBy { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public List<EndorsementChange> Changes { get; set; }
    public List<EndorsementDocument> Documents { get; set; }
    public List<EndorsementNote> Notes { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class EndorsementChange
{
    public string FieldPath { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }
    public string ChangeType { get; set; }
    public bool RequiresApproval { get; set; }
    public string Category { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class EndorsementDocument
{
    public string DocumentId { get; set; }
    public string DocumentType { get; set; }
    public string Description { get; set; }
    public DateTime GeneratedDate { get; set; }
    public bool RequiresSignature { get; set; }
    public bool IsSigned { get; set; }
    public string SignedBy { get; set; }
    public DateTime? SignedDate { get; set; }
    public byte[] Content { get; set; }
}

public class EndorsementValidationError
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string Field { get; set; }
    public string Category { get; set; }
    public bool IsBlocking { get; set; }
    public Dictionary<string, object> Details { get; set; }
}

public class EndorsementWorkflow
{
    public string WorkflowId { get; set; }
    public string LineOfBusiness { get; set; }
    public string State { get; set; }
    public List<EndorsementStep> Steps { get; set; }
    public List<EndorsementRequirement> Requirements { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public Dictionary<string, object> WorkflowData { get; set; }
}

public class EndorsementStep
{
    public string StepId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public List<string> DependsOn { get; set; }
    public List<EndorsementAction> Actions { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class EndorsementAction
{
    public string ActionId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public List<string> RequiredRoles { get; set; }
}

public class EndorsementRequirement
{
    public string RequirementId { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; }
    public List<string> AcceptableDocuments { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class EndorsementRequirementSubmission
{
    public string RequirementId { get; set; }
    public string Type { get; set; }
    public byte[] Content { get; set; }
    public string ContentType { get; set; }
    public string FileName { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public string SubmittedBy { get; set; }
    public DateTime SubmissionDate { get; set; }
}

public class EndorsementProcessRequest
{
    public string Action { get; set; }
    public string Comments { get; set; }
    public string ProcessedBy { get; set; }
    public Dictionary<string, object> ProcessingData { get; set; }
}

public class AvailableEndorsement
{
    public string EndorsementType { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool RequiresApproval { get; set; }
    public bool HasPremiumImpact { get; set; }
    public List<string> AllowedFields { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class EndorsementComparison
{
    public string BaseEndorsementId { get; set; }
    public string ComparisonEndorsementId { get; set; }
    public decimal PremiumDifference { get; set; }
    public List<EndorsementChangeComparison> Changes { get; set; }
    public List<CoverageComparison> CoverageChanges { get; set; }
    public List<DocumentComparison> DocumentChanges { get; set; }
}

public class EndorsementChangeComparison
{
    public string FieldPath { get; set; }
    public object BaseValue { get; set; }
    public object ComparisonValue { get; set; }
    public string Category { get; set; }
    public bool IsMaterialChange { get; set; }
}

public class CoverageComparison
{
    public string CoverageType { get; set; }
    public decimal BaseLimit { get; set; }
    public decimal ComparisonLimit { get; set; }
    public decimal BasePremium { get; set; }
    public decimal ComparisonPremium { get; set; }
    public decimal PremiumDifference { get; set; }
}

public class DocumentComparison
{
    public string DocumentType { get; set; }
    public bool ExistsInBase { get; set; }
    public bool ExistsInComparison { get; set; }
    public List<string> Differences { get; set; }
}

public enum EndorsementStatus
{
    Draft,
    PendingValidation,
    PendingRequirements,
    PendingPayment,
    PendingApproval,
    Approved,
    Processed,
    Rejected,
    Cancelled,
    Failed
}

public class EndorsementOptions
{
    public bool GenerateDocuments { get; set; } = true;
    public bool ValidateOnly { get; set; } = false;
    public bool AutoApprove { get; set; } = false;
    public string DocumentDeliveryMethod { get; set; }
    public string PaymentHandling { get; set; }
    public Dictionary<string, object> CustomOptions { get; set; }
} 