using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Shared.Integration.Transform.Interfaces;
using Shared.Integration.Transform.Models;

public class PolicyResponseTransformer : IResponseTypeTransformer
{
    public Type ResponseType => typeof(PolicyResponse);

    public async Task<TResponse> TransformAsync<TResponse>(
        XElement responseData, 
        TransformContext context) 
        where TResponse : class, new()
    {
        if (typeof(TResponse) != typeof(PolicyResponse))
        {
            throw new TransformException(
                $"Invalid response type. Expected PolicyResponse but got {typeof(TResponse).Name}");
        }

        var policy = new PolicyResponse
        {
            PolicyNumber = responseData.Element("PolicyNumber")?.Value,
            EffectiveDate = DateTime.Parse(
                responseData.Element("EffectiveDate")?.Value ?? 
                DateTime.MinValue.ToString()),
            ExpirationDate = DateTime.Parse(
                responseData.Element("ExpirationDate")?.Value ?? 
                DateTime.MinValue.ToString()),
            Status = Enum.Parse<PolicyStatus>(
                responseData.Element("Status")?.Value ?? 
                "Unknown"),
            TotalPremium = decimal.Parse(
                responseData.Element("TotalPremium")?.Value ?? "0"),
            Insured = TransformInsured(responseData.Element("Insured")),
            Coverages = TransformCoverages(responseData.Element("Coverages")),
            Documents = TransformDocuments(responseData.Element("Documents"))
        };

        return policy as TResponse;
    }

    private InsuredInfo TransformInsured(XElement insuredElement)
    {
        if (insuredElement == null) return null;

        return new InsuredInfo
        {
            Name = insuredElement.Element("Name")?.Value,
            Address = insuredElement.Element("Address")?.Value,
            Phone = insuredElement.Element("Phone")?.Value,
            Email = insuredElement.Element("Email")?.Value
        };
    }

    private List<CoverageInfo> TransformCoverages(XElement coveragesElement)
    {
        if (coveragesElement == null) return new List<CoverageInfo>();

        return coveragesElement.Elements("Coverage")
            .Select(c => new CoverageInfo
            {
                Code = c.Element("Code")?.Value,
                Description = c.Element("Description")?.Value,
                Limit = decimal.Parse(c.Element("Limit")?.Value ?? "0"),
                Premium = decimal.Parse(c.Element("Premium")?.Value ?? "0")
            })
            .ToList();
    }

    private List<DocumentInfo> TransformDocuments(XElement documentsElement)
    {
        if (documentsElement == null) return new List<DocumentInfo>();

        return documentsElement.Elements("Document")
            .Select(d => new DocumentInfo
            {
                Id = d.Element("Id")?.Value,
                Type = d.Element("Type")?.Value,
                Name = d.Element("Name")?.Value,
                CreatedDate = DateTime.Parse(
                    d.Element("CreatedDate")?.Value ?? 
                    DateTime.MinValue.ToString())
            })
            .ToList();
    }
} 