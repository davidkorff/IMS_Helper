public interface IIssuanceService
{
    Task<IssuanceResponse> IssuePolicy(IssuanceRequest request);
    Task<IssuanceStatus> GetIssuanceStatus(string issuanceId);
    Task<PolicyDetails> GetPolicyDetails(string policyNumber);
    Task<List<IssuanceDocument>> GetIssuanceDocuments(string issuanceId);
    Task<List<IssuanceValidationError>> ValidateForIssuance(string quoteId);
    Task<string> GeneratePreviewDocuments(string quoteId);
    Task<IssuanceWorkflow> GetIssuanceWorkflow(string lineOfBusiness, string state);
    Task<List<IssuanceRequirement>> GetPendingRequirements(string issuanceId);
    Task<bool> SubmitRequirement(string issuanceId, IssuanceRequirementSubmission requirement);
    Task<IssuanceResponse> ReissuePolicy(ReissuanceRequest request);
}

public class IssuanceRequest
{
    public string QuoteId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public PaymentInfo PaymentInfo { get; set; }
    public List<SignedDocument> SignedDocuments { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
    public IssuanceOptions Options { get; set; }
}

public class IssuanceResponse
{
    public string IssuanceId { get; set; }
    public string PolicyNumber { get; set; }
    public IssuanceStatus Status { get; set; }
    public List<IssuanceDocument> Documents { get; set; }
    public List<IssuanceRequirement> PendingRequirements { get; set; }
    public List<IssuanceValidationError> ValidationErrors { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string ProcessedBy { get; set; }
    public Dictionary<string, object> ResponseData { get; set; }
}

public class PaymentInfo
{
    public string PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string PaymentToken { get; set; }
    public BillingInfo BillingInfo { get; set; }
    public Dictionary<string, object> PaymentMetadata { get; set; }
}

public class BillingInfo
{
    public string BillingType { get; set; }
    public string PaymentPlan { get; set; }
    public Address BillingAddress { get; set; }
    public string AccountNumber { get; set; }
    public string RoutingNumber { get; set; }
    public string AccountType { get; set; }
    public CreditCardInfo CreditCard { get; set; }
}

public class CreditCardInfo
{
    public string LastFourDigits { get; set; }
    public string CardType { get; set; }
    public string ExpirationMonth { get; set; }
    public string ExpirationYear { get; set; }
    public string CardholderName { get; set; }
}

public class SignedDocument
{
    public string DocumentId { get; set; }
    public string DocumentType { get; set; }
    public DateTime SignedDate { get; set; }
    public string SignedBy { get; set; }
    public string SignatureMethod { get; set; }
    public byte[] SignedContent { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}

public class IssuanceDocument
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

public class IssuanceValidationError
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string Field { get; set; }
    public string Category { get; set; }
    public bool IsBlocking { get; set; }
    public Dictionary<string, object> Details { get; set; }
}

public class IssuanceWorkflow
{
    public string WorkflowId { get; set; }
    public string LineOfBusiness { get; set; }
    public string State { get; set; }
    public List<IssuanceStep> Steps { get; set; }
    public List<IssuanceRequirement> Requirements { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public Dictionary<string, object> WorkflowData { get; set; }
}

public class IssuanceStep
{
    public string StepId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public List<string> DependsOn { get; set; }
    public List<IssuanceAction> Actions { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class IssuanceAction
{
    public string ActionId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public List<string> RequiredRoles { get; set; }
}

public class IssuanceRequirement
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

public class IssuanceRequirementSubmission
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

public class ReissuanceRequest
{
    public string PolicyNumber { get; set; }
    public string ReissuanceReason { get; set; }
    public DateTime EffectiveDate { get; set; }
    public Dictionary<string, object> Changes { get; set; }
    public List<SignedDocument> SignedDocuments { get; set; }
    public IssuanceOptions Options { get; set; }
}

public class IssuanceOptions
{
    public bool GenerateDocuments { get; set; } = true;
    public bool ValidateOnly { get; set; } = false;
    public bool AutoApprove { get; set; } = false;
    public string DocumentDeliveryMethod { get; set; }
    public string PaymentHandling { get; set; }
    public Dictionary<string, object> CustomOptions { get; set; }
}

public enum IssuanceStatus
{
    Pending,
    InProgress,
    PendingRequirements,
    PendingPayment,
    PendingApproval,
    Approved,
    Issued,
    Failed,
    Cancelled
} 