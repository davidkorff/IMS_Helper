using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Shared.Integration.Transform;
using Shared.Integration.Transform.Transformers;
using Shared.Integration.Transform.Settings;

public class IMSResponseTransformerTests
{
    private readonly IMSResponseTransformer _transformer;
    private readonly Mock<ILogger<IMSResponseTransformer>> _loggerMock;
    private readonly IMSTransformSettings _settings;

    public IMSResponseTransformerTests()
    {
        _loggerMock = new Mock<ILogger<IMSResponseTransformer>>();
        _settings = new IMSTransformSettings
        {
            Options = new TransformationOptions
            {
                StrictMapping = true,
                IgnoreCase = true,
                TrimStrings = true
            }
        };

        var typeTransformers = new List<IResponseTypeTransformer>
        {
            new PolicyResponseTransformer(),
            new QuoteResponseTransformer()
        };

        _transformer = new IMSResponseTransformer(
            _loggerMock.Object,
            typeTransformers,
            Options.Create(_settings));
    }

    [Fact]
    public async Task TransformAsync_PolicyResponse_TransformsCorrectly()
    {
        // Arrange
        var soapResponse = CreateSamplePolicyResponse();

        // Act
        var result = await _transformer.TransformAsync<PolicyResponse>(
            soapResponse,
            new TransformContext { Operation = "GetPolicy" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("POL123", result.PolicyNumber);
        Assert.Equal(PolicyStatus.Active, result.Status);
        Assert.Equal(1000.00m, result.TotalPremium);
        Assert.NotNull(result.Insured);
        Assert.Equal("John Doe", result.Insured.Name);
        Assert.NotEmpty(result.Coverages);
    }

    [Fact]
    public async Task TransformAsync_QuoteResponse_TransformsCorrectly()
    {
        // Arrange
        var soapResponse = CreateSampleQuoteResponse();

        // Act
        var result = await _transformer.TransformAsync<QuoteResponse>(
            soapResponse,
            new TransformContext { Operation = "GetQuote" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("QTE123", result.QuoteNumber);
        Assert.Equal(QuoteStatus.Valid, result.Status);
        Assert.Equal(500.00m, result.Premium);
        Assert.NotNull(result.RatingFactors);
        Assert.NotEmpty(result.PremiumDetails);
    }

    [Fact]
    public async Task TransformAsync_InvalidXml_ThrowsTransformException()
    {
        // Arrange
        var invalidXml = "<Invalid>XML</Invalid>";

        // Act & Assert
        await Assert.ThrowsAsync<TransformException>(() =>
            _transformer.TransformAsync<PolicyResponse>(
                invalidXml,
                new TransformContext { Operation = "GetPolicy" }));
    }

    [Fact]
    public async Task TransformAsync_MissingRequiredField_ThrowsTransformException()
    {
        // Arrange
        var incompleteResponse = CreateIncompleteResponse();

        // Act & Assert
        await Assert.ThrowsAsync<TransformException>(() =>
            _transformer.TransformAsync<PolicyResponse>(
                incompleteResponse,
                new TransformContext { Operation = "GetPolicy" }));
    }

    private string CreateSamplePolicyResponse()
    {
        return @"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <GetPolicyResponse>
                        <PolicyNumber>POL123</PolicyNumber>
                        <Status>Active</Status>
                        <EffectiveDate>2024-01-01</EffectiveDate>
                        <ExpirationDate>2025-01-01</ExpirationDate>
                        <TotalPremium>1000.00</TotalPremium>
                        <Insured>
                            <Name>John Doe</Name>
                            <Address>123 Main St</Address>
                            <Phone>555-1234</Phone>
                            <Email>john@example.com</Email>
                        </Insured>
                        <Coverages>
                            <Coverage>
                                <Code>GL</Code>
                                <Description>General Liability</Description>
                                <Limit>1000000</Limit>
                                <Premium>500.00</Premium>
                            </Coverage>
                        </Coverages>
                    </GetPolicyResponse>
                </soap:Body>
            </soap:Envelope>";
    }

    private string CreateSampleQuoteResponse()
    {
        return @"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <GetQuoteResponse>
                        <QuoteNumber>QTE123</QuoteNumber>
                        <Status>Valid</Status>
                        <Premium>500.00</Premium>
                        <CreatedDate>2024-01-01</CreatedDate>
                        <ExpirationDate>2024-02-01</ExpirationDate>
                        <RatingFactors>
                            <Factor type='number'>1.25</Factor>
                            <Experience type='number'>0.95</Experience>
                            <Territory type='string'>CA</Territory>
                        </RatingFactors>
                        <PremiumDetails>
                            <Detail>
                                <Coverage>GL</Coverage>
                                <BasePremium>400.00</BasePremium>
                                <Modifications>100.00</Modifications>
                                <FinalPremium>500.00</FinalPremium>
                            </Detail>
                        </PremiumDetails>
                    </GetQuoteResponse>
                </soap:Body>
            </soap:Envelope>";
    }

    private string CreateIncompleteResponse()
    {
        return @"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <GetPolicyResponse>
                        <Status>Active</Status>
                    </GetPolicyResponse>
                </soap:Body>
            </soap:Envelope>";
    }
} 