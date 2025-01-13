public interface ICancellationService
{
    Task<CancellationResponse> InitiateCancellation(CancellationRequest request);
    Task<CancellationStatus> GetCancellationStatus(string cancellationId);
    Task<CancellationDetails> GetCancellationDetails(string cancellationId);
    Task<List<CancellationDocument>> GetCancellationDocuments(string cancellationId);
    Task<List<CancellationValidationError>> ValidateCancellation(string cancellationId);
    Task<string> GeneratePreviewDocuments(string cancellationId);
    Task<CancellationWorkflow> GetCancellationWorkflow(string lineOfBusiness, string state);
    Task<List<CancellationRequirement>> GetPendingRequirements(string cancellationId);
    Task<bool> SubmitRequirement(string cancellationId, CancellationRequirementSubmission requirement);
    Task<CancellationResponse> ProcessCancellation(string cancellationId, CancellationProcessRequest request);
    Task<List<AvailableCancellationType>> GetAvailableCancellationTypes(string policyNumber);
    Task<CancellationCalculation> CalculateRefund(string policyNumber, CancellationCalculationRequest request);
    Task<CancellationResponse> RescindCancellation(string cancellationId, RescindRequest request);
}

public class CancellationRequest
{
    public string PolicyNumber { get; set; }
    public DateTime CancellationDate { get; set; }
    public string CancellationType { get; set; }
    public string Reason { get; set; }
    public string RequestedBy { get; set; }
    public List<SignedDocument> SignedDocuments { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
    public CancellationOptions Options { get; set; }
}

public class CancellationResponse
{
    public string CancellationId { get; set; }
    public string PolicyNumber { get; set; }
    public CancellationStatus Status { get; set; }
    public decimal RefundAmount { get; set; }
    public List<CancellationDocument> Documents { get; set; }
    public List<CancellationRequirement> PendingRequirements { get; set; }
    public List<CancellationValidationError> ValidationErrors { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string ProcessedBy { get; set; }
    public Dictionary<string, object> ResponseData { get; set; }
}

public class CancellationDetails
{
    public string CancellationId { get; set; }
    public string PolicyNumber { get; set; }
    public string CancellationType { get; set; }
    public DateTime CancellationDate { get; set; }
    public CancellationStatus Status { get; set; }
    public decimal RefundAmount { get; set; }
    public string RequestedBy { get; set; }
    public DateTime RequestedDate { get; set; }
    public string ProcessedBy { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string Reason { get; set; }
    public List<CancellationDocument> Documents { get; set; }
    public List<CancellationNote> Notes { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class CancellationDocument
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

public class CancellationValidationError
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string Field { get; set; }
    public string Category { get; set; }
    public bool IsBlocking { get; set; }
    public Dictionary<string, object> Details { get; set; }
}

public class CancellationWorkflow
{
    public string WorkflowId { get; set; }
    public string LineOfBusiness { get; set; }
    public string State { get; set; }
    public List<CancellationStep> Steps { get; set; }
    public List<CancellationRequirement> Requirements { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public Dictionary<string, object> WorkflowData { get; set; }
}

public class CancellationStep
{
    public string StepId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public List<string> DependsOn { get; set; }
    public List<CancellationAction> Actions { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class CancellationAction
{
    public string ActionId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public List<string> RequiredRoles { get; set; }
}

public class CancellationRequirement
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

public class CancellationRequirementSubmission
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

public class CancellationProcessRequest
{
    public string Action { get; set; }
    public string Comments { get; set; }
    public string ProcessedBy { get; set; }
    public Dictionary<string, object> ProcessingData { get; set; }
}

public class AvailableCancellationType
{
    public string Type { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool RequiresApproval { get; set; }
    public bool AllowsRescission { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class CancellationCalculationRequest
{
    public DateTime CancellationDate { get; set; }
    public string CancellationType { get; set; }
    public string CalculationMethod { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
}

public class CancellationCalculation
{
    public decimal EarnedPremium { get; set; }
    public decimal UnearnedPremium { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal CancellationFee { get; set; }
    public decimal OtherCharges { get; set; }
    public string CalculationMethod { get; set; }
    public Dictionary<string, decimal> Breakdown { get; set; }
}

public class RescindRequest
{
    public string Reason { get; set; }
    public string RequestedBy { get; set; }
    public List<SignedDocument> SignedDocuments { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
}

public class CancellationOptions
{
    public bool GenerateDocuments { get; set; } = true;
    public bool ValidateOnly { get; set; } = false;
    public bool AutoApprove { get; set; } = false;
    public string DocumentDeliveryMethod { get; set; }
    public string RefundHandling { get; set; }
    public Dictionary<string, object> CustomOptions { get; set; }
}

public enum CancellationStatus
{
    Draft,
    PendingValidation,
    PendingRequirements,
    PendingApproval,
    Approved,
    Processed,
    Rejected,
    Rescinded,
    Failed
} 