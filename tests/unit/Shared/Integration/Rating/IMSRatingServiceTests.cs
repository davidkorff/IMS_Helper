using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class IMSRatingServiceTests
{
    private readonly IMSRatingService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSRatingService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSRatingSettings _settings;

    public IMSRatingServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSRatingService>>();
        _cacheMock = new Mock<IMemoryCache>();
        _settings = new IMSRatingSettings
        {
            DefaultRatingOption = "STD_FLOOD",
            EnableCaching = true,
            CacheExpirationMinutes = 60,
            DefaultFactorValues = new Dictionary<string, string>
            {
                { "TERRITORY", "01" },
                { "CONSTRUCTION", "1" }
            }
        };

        _service = new IMSRatingService(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task RateQuote_ValidRequest_ReturnsRatingResponse()
    {
        // Arrange
        var quoteId = "Q123456";
        var request = new RatingRequest
        {
            QuoteId = quoteId,
            RatingOptionId = "STD_FLOOD",
            RatingData = new Dictionary<string, object>
            {
                { "COVERAGE_A", 250000 },
                { "DEDUCTIBLE", 1000 }
            },
            UnderwriterGuid = "UW123",
            EffectiveDate = DateTime.Today
        };

        var ratingXml = CreateSampleRatingResponseXml();
        SetupIMSClientResponse("ImportRatingXml", ratingXml);

        // Act
        var response = await _service.RateQuote(quoteId, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(quoteId, response.QuoteId);
        Assert.Equal(1000m, response.BasePremium);
        Assert.Equal(2, response.Modifications.Count);
        Assert.Equal(1250m, response.TotalPremium);
        
        VerifyIMSClientCall("ImportRatingXml", Times.Once());
    }

    [Fact]
    public async Task GetRatingOptions_ValidRequest_ReturnsOptions()
    {
        // Arrange
        var lineOfBusiness = "FLOOD";
        var state = "FL";
        var optionsXml = CreateSampleRatingOptionsXml();
        
        SetupIMSClientResponse("GetRatingOptions_WS", optionsXml);
        SetupCache("rating_options_FLOOD_FL", null);

        // Act
        var options = await _service.GetRatingOptions(lineOfBusiness, state);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(2, options.Count);
        Assert.Contains(options, o => o.Name == "Standard Flood");
        Assert.Contains(options, o => o.Name == "Preferred Flood");
    }

    [Fact]
    public async Task ValidateRatingData_InvalidData_ReturnsValidationErrors()
    {
        // Arrange
        var request = new RatingRequest
        {
            RatingOptionId = "STD_FLOOD",
            RatingData = new Dictionary<string, object>
            {
                { "COVERAGE_A", 5000000 }, // Exceeds maximum
                { "DEDUCTIBLE", 500 } // Invalid value
            }
        };

        var factorsXml = CreateSampleRatingFactorsXml();
        SetupIMSClientResponse("GetRatingFactors_WS", factorsXml);
        SetupCache("rating_factors_STD_FLOOD", null);

        // Act
        var validation = await _service.ValidateRatingData(request);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(2, validation.Errors.Count);
        Assert.Contains(validation.Errors, 
            e => e.FactorCode == "COVERAGE_A" && 
                 e.Message.Contains("must be less than"));
        Assert.Contains(validation.Errors,
            e => e.FactorCode == "DEDUCTIBLE" && 
                 e.Message.Contains("must be one of"));
    }

    [Fact]
    public async Task GenerateRatingWorksheet_ValidRequest_ReturnsWorksheet()
    {
        // Arrange
        var quoteId = "Q123456";
        var ratingOptionId = "STD_FLOOD";
        var worksheetData = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("Sample worksheet content"));
        
        SetupIMSClientResponse("GenerateRatingWorksheet_WS", worksheetData);

        // Act
        var worksheet = await _service.GenerateRatingWorksheet(quoteId, ratingOptionId);

        // Assert
        Assert.NotNull(worksheet);
        Assert.True(worksheet.Length > 0);
        VerifyIMSClientCall("GenerateRatingWorksheet_WS", Times.Once());
    }

    [Fact]
    public async Task RateQuote_WithOverrides_AppliesOverridesCorrectly()
    {
        // Arrange
        var quoteId = "Q123456";
        var request = new RatingRequest
        {
            QuoteId = quoteId,
            RatingOptionId = "STD_FLOOD",
            RatingData = new Dictionary<string, object>
            {
                { "COVERAGE_A", 250000 },
                { "DEDUCTIBLE", 1000 }
            },
            Overrides = new List<RatingOverride>
            {
                new RatingOverride
                {
                    FactorCode = "TERRITORY",
                    Value = "02",
                    Reason = "Risk reassessment",
                    ApprovedBy = "UW123"
                }
            }
        };

        SetupIMSClientResponse("ImportRatingXml", CreateSampleRatingResponseXml());

        // Act
        var response = await _service.RateQuote(quoteId, request);

        // Assert
        Assert.NotNull(response);
        VerifyOverrideWasApplied(request.Overrides.First());
    }

    [Fact]
    public async Task ValidateRatingData_WithMissingDependencies_ReturnsErrors()
    {
        // Arrange
        var request = new RatingRequest
        {
            RatingOptionId = "STD_FLOOD",
            RatingData = new Dictionary<string, object>
            {
                { "SPRINKLER_CREDIT", "Y" } // Requires CONSTRUCTION_TYPE
            }
        };

        SetupIMSClientResponse("GetRatingFactors_WS", CreateSampleRatingFactorsWithDependencies());

        // Act
        var validation = await _service.ValidateRatingData(request);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, 
            e => e.Message.Contains("Required dependent factor"));
    }

    [Fact]
    public async Task GetRatingFactors_CacheEnabled_UsesCachedData()
    {
        // Arrange
        var ratingOptionId = "STD_FLOOD";
        var factorsXml = CreateSampleRatingFactorsXml();
        var cachedFactors = ParseRatingFactors(factorsXml);

        object cached = cachedFactors;
        _cacheMock.Setup(x => x.TryGetValue(
            $"rating_factors_{ratingOptionId}", 
            out cached))
            .Returns(true);

        // Act
        var factors = await _service.GetRatingFactors(ratingOptionId);

        // Assert
        Assert.Equal(cachedFactors.Count, factors.Count);
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ExecuteCommandRequest>()),
            Times.Never);
    }

    [Theory]
    [InlineData("COVERAGE_A", 0, false, "must be greater than")]
    [InlineData("COVERAGE_A", 10000000, false, "must be less than")]
    [InlineData("DEDUCTIBLE", 750, false, "must be one of")]
    [InlineData("TERRITORY", "99", false, "invalid territory code")]
    [InlineData("COVERAGE_A", 250000, true, null)]
    private async Task ValidateRatingData_VariousScenarios(
        string factor, 
        object value, 
        bool expectedValid, 
        string expectedErrorContent)
    {
        // Arrange
        var request = new RatingRequest
        {
            RatingOptionId = "STD_FLOOD",
            RatingData = new Dictionary<string, object>
            {
                { factor, value }
            }
        };

        SetupIMSClientResponse("GetRatingFactors_WS", CreateSampleRatingFactorsXml());

        // Act
        var validation = await _service.ValidateRatingData(request);

        // Assert
        Assert.Equal(expectedValid, validation.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(validation.Errors, 
                e => e.FactorCode == factor && 
                     e.Message.Contains(expectedErrorContent));
        }
    }

    private void SetupIMSClientResponse(string procedure, string result)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<ExecuteCommandRequest>(r => 
                    r.ProcedureName.Contains(procedure))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = result });
    }

    private void SetupCache(string key, object value)
    {
        var cacheEntry = Mock.Of<ICacheEntry>();
        _cacheMock
            .Setup(x => x.CreateEntry(key))
            .Returns(cacheEntry);
    }

    private void VerifyIMSClientCall(string procedure, Times times)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<ExecuteCommandRequest>(r => 
                    r.ProcedureName.Contains(procedure))),
            times);
    }

    private string CreateSampleRatingResponseXml()
    {
        return @"
            <RatingResponse>
                <QuoteId>Q123456</QuoteId>
                <RatingId>R789</RatingId>
                <BasePremium>1000.00</BasePremium>
                <Modifications>
                    <Modification>
                        <Type>TERRITORY</Type>
                        <Description>Territory Factor</Description>
                        <Factor>1.15</Factor>
                        <Amount>150.00</Amount>
                    </Modification>
                    <Modification>
                        <Type>CONSTRUCTION</Type>
                        <Description>Construction Type</Description>
                        <Factor>1.10</Factor>
                        <Amount>100.00</Amount>
                    </Modification>
                </Modifications>
                <Fees>
                    <Fee>
                        <Type>POLICY</Type>
                        <Description>Policy Fee</Description>
                        <Amount>25.00</Amount>
                        <IsOptional>false</IsOptional>
                    </Fee>
                </Fees>
                <TotalPremium>1250.00</TotalPremium>
                <RatedDate>2024-03-21T10:30:00</RatedDate>
                <RatedBy>SYSTEM</RatedBy>
            </RatingResponse>";
    }

    private string CreateSampleRatingOptionsXml()
    {
        return @"
            <RatingOptions>
                <Option>
                    <Id>STD_FLOOD</Id>
                    <Name>Standard Flood</Name>
                    <Description>Standard flood coverage</Description>
                    <Version>1.0</Version>
                    <EffectiveDate>2024-01-01</EffectiveDate>
                    <SupportedStates>
                        <State>FL</State>
                        <State>TX</State>
                    </SupportedStates>
                </Option>
                <Option>
                    <Id>PREF_FLOOD</Id>
                    <Name>Preferred Flood</Name>
                    <Description>Preferred flood coverage</Description>
                    <Version>1.0</Version>
                    <EffectiveDate>2024-01-01</EffectiveDate>
                    <SupportedStates>
                        <State>FL</State>
                    </SupportedStates>
                </Option>
            </RatingOptions>";
    }

    private string CreateSampleRatingFactorsXml()
    {
        return @"
            <RatingFactors>
                <Factor>
                    <Code>COVERAGE_A</Code>
                    <Name>Building Coverage</Name>
                    <Description>Building coverage amount</Description>
                    <DataType>Decimal</DataType>
                    <Required>true</Required>
                    <ValidationRules>
                        <MinValue>50000</MinValue>
                        <MaxValue>1000000</MaxValue>
                    </ValidationRules>
                </Factor>
                <Factor>
                    <Code>DEDUCTIBLE</Code>
                    <Name>Deductible</Name>
                    <Description>Policy deductible</Description>
                    <DataType>Decimal</DataType>
                    <Required>true</Required>
                    <AllowedValues>
                        <Value>1000</Value>
                        <Value>2500</Value>
                        <Value>5000</Value>
                    </AllowedValues>
                </Factor>
            </RatingFactors>";
    }

    private string CreateSampleRatingFactorsWithDependencies()
    {
        return @"
            <RatingFactors>
                <Factor>
                    <Code>SPRINKLER_CREDIT</Code>
                    <Name>Sprinkler System Credit</Name>
                    <DataType>String</DataType>
                    <Required>false</Required>
                    <ValidationRules>
                        <Dependencies>
                            <Dependency>CONSTRUCTION_TYPE</Dependency>
                        </Dependencies>
                    </ValidationRules>
                </Factor>
            </RatingFactors>";
    }

    private void VerifyOverrideWasApplied(RatingOverride @override)
    {
        _imsClientMock.Verify(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<ExecuteCommandRequest>(r => 
                r.Parameters.Contains(@override.FactorCode) && 
                r.Parameters.Contains(@override.Value.ToString()))),
            Times.Once);
    }
} 