public class PolicyResponse
{
    public string PolicyId { get; set; }
    public string QuoteId { get; set; }
    public PolicyStatus Status { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public InsuredInfo Insured { get; set; }
    public CoverageInfo Coverage { get; set; }
    public PremiumInfo Premium { get; set; }
    public List<EndorsementInfo> Endorsements { get; set; }
}

public class EndorsementRequest
{
    public DateTime EffectiveDate { get; set; }
    public string Description { get; set; }
    public List<CoverageChange> Changes { get; set; }
}

public class EndorsementResponse
{
    public string EndorsementId { get; set; }
    public string PolicyId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public EndorsementStatus Status { get; set; }
    public PremiumInfo PremiumChange { get; set; }
}

public class CancellationRequest
{
    public DateTime CancellationDate { get; set; }
    public CancellationReason Reason { get; set; }
    public string Description { get; set; }
}

public class CancellationResponse
{
    public string CancellationId { get; set; }
    public string PolicyId { get; set; }
    public DateTime CancellationDate { get; set; }
    public CancellationStatus Status { get; set; }
    public decimal ReturnPremium { get; set; }
}

public class ReinstatementRequest
{
    public string Reason { get; set; }
    public List<DocumentMetadata> SupportingDocuments { get; set; }
}

public class PolicyHistoryEntry
{
    public DateTime Timestamp { get; set; }
    public PolicyAction Action { get; set; }
    public string Description { get; set; }
    public string UserId { get; set; }
    public Dictionary<string, object> Changes { get; set; }
}

public enum PolicyStatus
{
    Active,
    Cancelled,
    Expired,
    PendingCancellation,
    Reinstated
}

public enum EndorsementStatus
{
    Draft,
    Pending,
    Approved,
    Rejected
}

public enum CancellationReason
{
    NonPayment,
    Underwriting,
    InsuredRequest,
    Other
}

public enum PolicyAction
{
    Created,
    Endorsed,
    Cancelled,
    Reinstated,
    Renewed
} 