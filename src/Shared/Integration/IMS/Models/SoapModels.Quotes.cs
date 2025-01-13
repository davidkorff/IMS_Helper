[XmlRoot(Namespace = "http://tempuri.org/")]
public class CreateSubmissionRequest
{
    public string InsuredGuid { get; set; }
    public string ProducerContactGuid { get; set; }
    public string UnderwriterGuid { get; set; }
    public DateTime SubmissionDate { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class CreateSubmissionResponse
{
    public string SubmissionGuid { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class CreateQuoteRequest
{
    public string SubmissionGuid { get; set; }
    public QuoteDetailsXml QuoteDetails { get; set; }
}

public class QuoteDetailsXml
{
    public string QuotingLocationGuid { get; set; }
    public string IssuingLocationGuid { get; set; }
    public string CompanyLocationGuid { get; set; }
    public string LineGuid { get; set; }
    public string StateId { get; set; }
    public string ProducerContactGuid { get; set; }
    public int QuoteStatusId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public int BillingTypeId { get; set; }
    public string UnderwriterGuid { get; set; }
    public int PolicyTypeId { get; set; }
    public int CostCenterId { get; set; }
    public List<QuoteOptionXml> Options { get; set; }
}

public class QuoteOptionXml
{
    public string OptionId { get; set; }
    public decimal Premium { get; set; }
    public decimal Tax { get; set; }
    public decimal Fee { get; set; }
    public List<CoverageXml> Coverages { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class BindQuoteRequest
{
    public string QuoteGuid { get; set; }
    public int InstallmentPlanId { get; set; }
    public DateTime BindDate { get; set; }
    public string UnderwriterGuid { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class BindQuoteResponse
{
    public string PolicyNumber { get; set; }
    public string PolicyId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string Status { get; set; }
} 