public class CreateClaimRequest
{
    public string PolicyId { get; set; }
    public DateTime DateOfLoss { get; set; }
    public string LossDescription { get; set; }
    public ClaimType Type { get; set; }
    public List<string> LossLocations { get; set; }
    public List<ClaimantInfo> Claimants { get; set; }
    public decimal EstimatedLoss { get; set; }
    public List<ClaimDocumentMetadata> Documents { get; set; }
}

public class ClaimResponse
{
    public string ClaimId { get; set; }
    public string PolicyId { get; set; }
    public DateTime DateOfLoss { get; set; }
    public string LossDescription { get; set; }
    public ClaimType Type { get; set; }
    public ClaimStatus Status { get; set; }
    public List<string> LossLocations { get; set; }
    public List<ClaimantInfo> Claimants { get; set; }
    public decimal EstimatedLoss { get; set; }
    public decimal ReserveAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ClaimDocument> Documents { get; set; }
    public List<ClaimNote> Notes { get; set; }
}

public class ClaimantInfo
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public ClaimantType Type { get; set; }
    public AddressInfo Address { get; set; }
}

public class ClaimDocumentMetadata
{
    public string Description { get; set; }
    public ClaimDocumentType Type { get; set; }
    public DateTime DocumentDate { get; set; }
}

public class ClaimDocument
{
    public string DocumentId { get; set; }
    public string FileName { get; set; }
    public ClaimDocumentType Type { get; set; }
    public string Description { get; set; }
    public DateTime DocumentDate { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class ClaimNote
{
    public string NoteId { get; set; }
    public string Content { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public NoteType Type { get; set; }
}

public class UpdateClaimStatusRequest
{
    public ClaimStatus NewStatus { get; set; }
    public string Reason { get; set; }
    public decimal? ReserveAmount { get; set; }
    public decimal? PaymentAmount { get; set; }
}

public enum ClaimType
{
    Property,
    Liability,
    WorkersComp,
    Auto,
    Other
}

public enum ClaimStatus
{
    New,
    UnderReview,
    NeedsMoreInfo,
    Approved,
    InPayment,
    Closed,
    Denied
}

public enum ClaimantType
{
    Insured,
    ThirdParty,
    Vendor,
    Other
}

public enum ClaimDocumentType
{
    LossReport,
    PoliceReport,
    Invoice,
    Estimate,
    Photo,
    Other
}

public enum NoteType
{
    Internal,
    External,
    System
} 