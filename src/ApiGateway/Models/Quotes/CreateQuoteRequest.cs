public class CreateQuoteRequest
{
    public string ProgramCode { get; set; }
    public InsuredInfo Insured { get; set; }
    public CoverageInfo Coverage { get; set; }
    public List<LocationInfo> Locations { get; set; }
    public PremiumInfo Premium { get; set; }
}

public class InsuredInfo
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public AddressInfo Address { get; set; }
}

public class CoverageInfo
{
    public string LineOfBusiness { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public List<string> Coverages { get; set; }
}

public class LocationInfo
{
    public AddressInfo Address { get; set; }
    public string BuildingType { get; set; }
    public decimal BuildingValue { get; set; }
    public decimal ContentsValue { get; set; }
}

public class AddressInfo
{
    public string Street1 { get; set; }
    public string Street2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
}

public class PremiumInfo
{
    public decimal BasePremium { get; set; }
    public decimal Tax { get; set; }
    public decimal Fee { get; set; }
    public decimal TotalPremium { get; set; }
} 