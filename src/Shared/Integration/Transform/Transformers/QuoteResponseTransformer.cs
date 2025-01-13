using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

public class QuoteResponseTransformer : IResponseTypeTransformer
{
    public Type ResponseType => typeof(QuoteResponse);

    public async Task<TResponse> TransformAsync<TResponse>(
        XElement responseData, 
        TransformContext context) 
        where TResponse : class, new()
    {
        if (typeof(TResponse) != typeof(QuoteResponse))
        {
            throw new TransformException(
                $"Invalid response type. Expected QuoteResponse but got {typeof(TResponse).Name}");
        }

        var quote = new QuoteResponse
        {
            QuoteNumber = responseData.Element("QuoteNumber")?.Value,
            Status = Enum.Parse<QuoteStatus>(
                responseData.Element("Status")?.Value ?? 
                "Unknown"),
            Premium = decimal.Parse(
                responseData.Element("Premium")?.Value ?? "0"),
            CreatedDate = DateTime.Parse(
                responseData.Element("CreatedDate")?.Value ?? 
                DateTime.MinValue.ToString()),
            ExpirationDate = DateTime.Parse(
                responseData.Element("ExpirationDate")?.Value ?? 
                DateTime.MinValue.ToString()),
            RatingFactors = TransformRatingFactors(
                responseData.Element("RatingFactors")),
            PremiumDetails = TransformPremiumDetails(
                responseData.Element("PremiumDetails"))
        };

        return quote as TResponse;
    }

    private Dictionary<string, object> TransformRatingFactors(XElement factorsElement)
    {
        if (factorsElement == null) 
            return new Dictionary<string, object>();

        return factorsElement.Elements()
            .ToDictionary(
                e => e.Name.LocalName,
                e => TransformFactorValue(e));
    }

    private object TransformFactorValue(XElement element)
    {
        var typeAttr = element.Attribute("type")?.Value ?? "string";
        var value = element.Value;

        return typeAttr.ToLower() switch
        {
            "number" => decimal.Parse(value),
            "boolean" => bool.Parse(value),
            "date" => DateTime.Parse(value),
            _ => value
        };
    }

    private List<PremiumDetail> TransformPremiumDetails(XElement detailsElement)
    {
        if (detailsElement == null) 
            return new List<PremiumDetail>();

        return detailsElement.Elements("Detail")
            .Select(d => new PremiumDetail
            {
                Coverage = d.Element("Coverage")?.Value,
                BasePremium = decimal.Parse(
                    d.Element("BasePremium")?.Value ?? "0"),
                Modifications = decimal.Parse(
                    d.Element("Modifications")?.Value ?? "0"),
                FinalPremium = decimal.Parse(
                    d.Element("FinalPremium")?.Value ?? "0")
            })
            .ToList();
    }
} 