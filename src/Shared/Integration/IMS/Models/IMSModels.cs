public class CompanyLineResponse
{
    public List<CompanyLine> CompanyLines { get; set; }
}

public class CompanyLine
{
    public string CompanyLineGUID { get; set; }
    public string LocationName { get; set; }
    public string CompanyLocationGUID { get; set; }
    public string LineName { get; set; }
    public string LineGUID { get; set; }
    public string StateID { get; set; }
    public List<Office> Offices { get; set; }
    public List<User> Users { get; set; }
    public List<BillType> BillTypes { get; set; }
}

public class SubmissionRequest
{
    public string InsuredGuid { get; set; }
    public string ProducerContactGuid { get; set; }
    public string UnderwriterGuid { get; set; }
    public DateTime SubmissionDate { get; set; }
}

public class QuoteRequest
{
    public string SubmissionGuid { get; set; }
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
}

public class BindRequest
{
    public string QuoteGuid { get; set; }
    public int InstallmentPlanId { get; set; }
    public DateTime BindDate { get; set; }
    public string UnderwriterGuid { get; set; }
}

public class BindResponse
{
    public string PolicyNumber { get; set; }
    public string PolicyId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
} 