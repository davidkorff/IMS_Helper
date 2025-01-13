[XmlRoot(Namespace = "http://tempuri.org/")]
public class CreateClaimRequest
{
    public string PolicyId { get; set; }
    public ClaimXml Claim { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class CreateClaimResponse
{
    public string ClaimId { get; set; }
    public string Status { get; set; }
}

public class ClaimXml
{
    public DateTime DateOfLoss { get; set; }
    public string LossDescription { get; set; }
    public string ClaimType { get; set; }
    public List<string> LossLocations { get; set; }
    public List<ClaimantXml> Claimants { get; set; }
    public decimal EstimatedLoss { get; set; }
    public List<ClaimDocumentXml> Documents { get; set; }
}

public class ClaimantXml
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string ClaimantType { get; set; }
    public AddressXml Address { get; set; }
}

public class ClaimDocumentXml
{
    public string DocumentId { get; set; }
    public string Description { get; set; }
    public string DocumentType { get; set; }
    public DateTime DocumentDate { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class UpdateClaimStatusRequest
{
    public string ClaimId { get; set; }
    public string NewStatus { get; set; }
    public string Reason { get; set; }
    public decimal? ReserveAmount { get; set; }
    public decimal? PaymentAmount { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class UpdateClaimStatusResponse
{
    public string ClaimId { get; set; }
    public string Status { get; set; }
    public DateTime UpdatedAt { get; set; }
} 