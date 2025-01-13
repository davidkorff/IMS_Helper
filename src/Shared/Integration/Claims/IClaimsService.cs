public interface IClaimsService
{
    Task<string> CreateClaim(ClaimCreationRequest request);
    Task<ClaimDetails> GetClaimDetails(string claimId);
    Task<ClaimStatus> GetClaimStatus(string claimId);
    Task<List<ClaimActivity>> GetClaimActivities(string claimId);
    Task<string> UpdateClaim(string claimId, ClaimUpdateRequest request);
    Task<string> AssignClaim(string claimId, ClaimAssignmentRequest request);
    Task<List<ClaimDocument>> GetClaimDocuments(string claimId);
    Task<string> UploadClaimDocument(string claimId, ClaimDocumentUpload document);
    Task<PaymentResponse> ProcessClaimPayment(string claimId, PaymentRequest request);
    Task<List<ClaimNote>> GetClaimNotes(string claimId);
    Task<string> AddClaimNote(string claimId, ClaimNoteRequest note);
}

public class ClaimCreationRequest
{
    public string PolicyNumber { get; set; }
    public DateTime DateOfLoss { get; set; }
    public string LossDescription { get; set; }
    public string ReportedBy { get; set; }
    public DateTime ReportedDate { get; set; }
    public string LossType { get; set; }
    public string CauseOfLoss { get; set; }
    public Address LossLocation { get; set; }
    public List<ClaimContact> Contacts { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
    public List<InitialClaimDocument> InitialDocuments { get; set; }
}

public class ClaimDetails
{
    public string ClaimId { get; set; }
    public string PolicyNumber { get; set; }
    public string InsuredName { get; set; }
    public DateTime DateOfLoss { get; set; }
    public string LossDescription { get; set; }
    public ClaimStatus Status { get; set; }
    public string AdjusterId { get; set; }
    public string AdjusterName { get; set; }
    public decimal? ReserveAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public List<Coverage> Coverages { get; set; }
    public List<ClaimContact> Contacts { get; set; }
    public List<ClaimActivity> Activities { get; set; }
    public List<ClaimDocument> Documents { get; set; }
    public List<ClaimNote> Notes { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
}

public class ClaimUpdateRequest
{
    public string ClaimId { get; set; }
    public string Status { get; set; }
    public decimal? ReserveAmount { get; set; }
    public string LossDescription { get; set; }
    public List<ClaimContact> UpdatedContacts { get; set; }
    public Dictionary<string, object> UpdatedData { get; set; }
    public string UpdatedBy { get; set; }
    public string UpdateReason { get; set; }
}

public class ClaimAssignmentRequest
{
    public string AdjusterId { get; set; }
    public string AssignmentReason { get; set; }
    public string AssignedBy { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; }
    public Dictionary<string, object> AssignmentData { get; set; }
}

public class ClaimDocumentUpload
{
    public string DocumentType { get; set; }
    public string Description { get; set; }
    public byte[] Content { get; set; }
    public string FileName { get; set; }
    public string UploadedBy { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}

public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string PaymentType { get; set; }
    public string PayeeName { get; set; }
    public string PayeeType { get; set; }
    public string Coverage { get; set; }
    public string PaymentReason { get; set; }
    public string ApprovedBy { get; set; }
    public Dictionary<string, object> PaymentDetails { get; set; }
}

public class PaymentResponse
{
    public string PaymentId { get; set; }
    public string Status { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string TransactionNumber { get; set; }
    public decimal Amount { get; set; }
    public string PayeeName { get; set; }
    public string ProcessedBy { get; set; }
}

public class ClaimNote
{
    public string NoteId { get; set; }
    public string ClaimId { get; set; }
    public string NoteType { get; set; }
    public string Content { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsPrivate { get; set; }
    public List<string> Tags { get; set; }
}

public class ClaimNoteRequest
{
    public string NoteType { get; set; }
    public string Content { get; set; }
    public string CreatedBy { get; set; }
    public bool IsPrivate { get; set; }
    public List<string> Tags { get; set; }
}

public class ClaimActivity
{
    public string ActivityId { get; set; }
    public string ClaimId { get; set; }
    public string ActivityType { get; set; }
    public string Description { get; set; }
    public string PerformedBy { get; set; }
    public DateTime ActivityDate { get; set; }
    public Dictionary<string, object> ActivityData { get; set; }
}

public class ClaimDocument
{
    public string DocumentId { get; set; }
    public string DocumentType { get; set; }
    public string FileName { get; set; }
    public string Description { get; set; }
    public DateTime UploadDate { get; set; }
    public string UploadedBy { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}

public class ClaimContact
{
    public string ContactId { get; set; }
    public string ContactType { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public Address Address { get; set; }
    public string Role { get; set; }
    public Dictionary<string, string> AdditionalInfo { get; set; }
}

public class Coverage
{
    public string CoverageCode { get; set; }
    public string Description { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal? DeductibleAmount { get; set; }
    public decimal? ReserveAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string Status { get; set; }
}

public class InitialClaimDocument
{
    public string DocumentType { get; set; }
    public string Description { get; set; }
    public byte[] Content { get; set; }
    public string FileName { get; set; }
}

public enum ClaimStatus
{
    New,
    UnderInvestigation,
    PendingInformation,
    InProcess,
    PendingPayment,
    Closed,
    Reopened,
    Denied
} 