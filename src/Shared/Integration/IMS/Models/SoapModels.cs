public class SoapEnvelope<T> where T : class
{
    [XmlElement(Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public SoapHeader Header { get; set; }

    [XmlElement(Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public SoapBody<T> Body { get; set; }

    public SoapEnvelope(T request, string authToken = null)
    {
        Header = new SoapHeader { AuthenticationToken = authToken };
        Body = new SoapBody<T> { Request = request };
    }
}

public class SoapHeader
{
    public string AuthenticationToken { get; set; }
}

public class SoapBody<T>
{
    [XmlElement(Namespace = "http://tempuri.org/")]
    public T Request { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class LoginRequest
{
    public string ProgramCode { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class LoginResponse
{
    public string Token { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class GetPolicyRequest
{
    public string PolicyId { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class GetPolicyResponse
{
    public PolicyXml Policy { get; set; }
}

public class PolicyXml
{
    public string PolicyId { get; set; }
    public string QuoteId { get; set; }
    public string PolicyNumber { get; set; }
    public string Status { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public InsuredXml Insured { get; set; }
    public CoverageXml Coverage { get; set; }
    public PremiumXml Premium { get; set; }
    public List<EndorsementXml> Endorsements { get; set; }
}

public class InsuredXml
{
    public string InsuredId { get; set; }
    public string Name { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
}

public class CoverageXml
{
    public string LineOfBusiness { get; set; }
    public string CoverageType { get; set; }
    public decimal Limit { get; set; }
    public decimal Deductible { get; set; }
    public List<string> Endorsements { get; set; }
}

public class PremiumXml
{
    public decimal BasePremium { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TotalPremium { get; set; }
    public string PaymentPlan { get; set; }
    public string BillingType { get; set; }
}

public class EndorsementXml
{
    public string EndorsementId { get; set; }
    public string Type { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Status { get; set; }
    public decimal PremiumChange { get; set; }
}

// Add more request/response models for other IMS operations... 