using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Shared.Integration.Rating;

public class IMSRatingEngineTests
{
    private readonly IMSRatingEngine _engine;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSRatingEngine>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSRatingSettings _settings;

    public IMSRatingEngineTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSRatingEngine>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSRatingSettings
        {
            EngineSettings = new RatingEngineSettings
            {
                EnableParallelProcessing = true,
                MaxConcurrentCalculations = 5
            }
        };

        _engine = new IMSRatingEngine(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task RatePolicy_ValidRequest_ReturnsRatingResponse()
    {
        // Arrange
        var request = CreateSampleRatingRequest();
        SetupRatingResponse("RATE123");

        // Act
        var response = await _engine.RatePolicy(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("RATE123", response.RatingId);
        Assert.Equal(request.QuoteId, response.QuoteId);
        VerifyRatingRequest(request.QuoteId);
    }

    [Fact]
    public async Task GetRatingFactors_ValidRequest_ReturnsFactors()
    {
        // Arrange
        var lineOfBusiness = "FLOOD";
        var state = "FL";
        var factorsXml = CreateSampleFactorsXml();

        SetupFactorsRetrieval(lineOfBusiness, state, factorsXml);
        SetupCache($"rating_factors_{lineOfBusiness}_{state}", null);

        // Act
        var factors = await _engine.GetRatingFactors(lineOfBusiness, state);

        // Assert
        Assert.NotNull(factors);
        Assert.NotEmpty(factors);
        Assert.All(factors, factor =>
        {
            Assert.NotNull(factor.FactorId);
            Assert.NotNull(factor.Name);
        });
    }

    [Fact]
    public async Task CalculatePremium_ValidRequest_ReturnsPremium()
    {
        // Arrange
        var quoteId = "QUOTE123";
        var request = new PremiumCalculationRequest
        {
            RatingData = new Dictionary<string, object>
            {
                { "Coverage", 250000 },
                { "Deductible", 1000 }
            }
        };

        SetupPremiumCalculation(quoteId, 1500.00m);

        // Act
        var premium = await _engine.CalculatePremium(quoteId, request);

        // Assert
        Assert.Equal(1500.00m, premium);
        VerifyPremiumCalculation(quoteId);
    }

    [Fact]
    public async Task GetAvailableDiscounts_ValidQuote_ReturnsDiscounts()
    {
        // Arrange
        var quoteId = "QUOTE123";
        var discountsXml = CreateSampleDiscountsXml();

        SetupDiscountsRetrieval(quoteId, discountsXml);

        // Act
        var discounts = await _engine.GetAvailableDiscounts(quoteId);

        // Assert
        Assert.NotNull(discounts);
        Assert.NotEmpty(discounts);
        Assert.All(discounts, discount =>
        {
            Assert.NotNull(discount.DiscountId);
            Assert.NotNull(discount.Name);
            Assert.True(discount.MaximumPercentage > 0);
        });
    }

    [Fact]
    public async Task ValidateRatingData_InvalidData_ReturnsValidationErrors()
    {
        // Arrange
        var quoteId = "QUOTE123";
        var data = new Dictionary<string, object>
        {
            { "Coverage", -1000 } // Invalid coverage amount
        };

        SetupValidationResponse(quoteId, false);

        // Act
        var result = await _engine.ValidateRatingData(quoteId, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error =>
        {
            Assert.NotNull(error.ErrorCode);
            Assert.NotNull(error.Message);
        });
    }

    // Helper methods
    private RatingRequest CreateSampleRatingRequest()
    {
        return new RatingRequest
        {
            QuoteId = "QUOTE123",
            LineOfBusiness = "FLOOD",
            State = "FL",
            EffectiveDate = DateTime.Today.AddDays(30),
            RatingData = new Dictionary<string, object>
            {
                { "Coverage", 250000 },
                { "Deductible", 1000 },
                { "Construction", "Frame" }
            }
        };
    }

    private void SetupRatingResponse(string ratingId)
    {
        var responseXml = CreateSampleRatingResponseXml(ratingId);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "RatePolicy",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupFactorsRetrieval(string lineOfBusiness, string state, string factorsXml)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetRatingFactors",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(lineOfBusiness) && 
                    r.Parameters.Contains(state))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = factorsXml });
    }

    private void SetupPremiumCalculation(string quoteId, decimal premium)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CalculatePremium",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(quoteId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = premium.ToString() });
    }

    private void SetupValidationResponse(string quoteId, bool isValid)
    {
        var responseXml = CreateSampleValidationResponseXml(isValid);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "ValidateRatingData",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(quoteId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupCache<T>(string key, T value)
    {
        var cacheEntry = Mock.Of<ICacheEntry>();
        _cacheMock
            .Setup(x => x.CreateEntry(key))
            .Returns(cacheEntry);
    }

    private void VerifyRatingRequest(string quoteId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "RatePolicy",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(quoteId))),
            Times.Once);
    }

    private void VerifyPremiumCalculation(string quoteId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CalculatePremium",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(quoteId))),
            Times.Once);
    }

    private string CreateSampleRatingResponseXml(string ratingId)
    {
        return $@"
            <RatingResponse>
                <RatingId>{ratingId}</RatingId>
                <QuoteId>QUOTE123</QuoteId>
                <BasePremium>1000.00</BasePremium>
                <TotalPremium>1500.00</TotalPremium>
                <RatingDate>{DateTime.Now:yyyy-MM-dd}</RatingDate>
            </RatingResponse>";
    }

    private string CreateSampleFactorsXml()
    {
        return @"
            <RatingFactors>
                <Factor>
                    <FactorId>FACTOR_001</FactorId>
                    <Name>Construction Type</Name>
                    <Description>Building construction classification</Description>
                    <Category>Property</Category>
                    <Values>
                        <Value>
                            <Code>FRAME</Code>
                            <Description>Frame Construction</Description>
                            <Value>1.25</Value>
                        </Value>
                    </Values>
                </Factor>
            </RatingFactors>";
    }

    private string CreateSampleDiscountsXml()
    {
        return @"
            <Discounts>
                <Discount>
                    <DiscountId>DISC_001</DiscountId>
                    <Name>Safety Features</Name>
                    <Description>Discount for safety features</Description>
                    <MaximumPercentage>15.0</MaximumPercentage>
                </Discount>
            </Discounts>";
    }

    private string CreateSampleValidationResponseXml(bool isValid)
    {
        return $@"
            <ValidationResult>
                <IsValid>{isValid}</IsValid>
                <Errors>
                    <Error>
                        <ErrorCode>ERR001</ErrorCode>
                        <Message>Coverage amount must be positive</Message>
                        <Field>Coverage</Field>
                    </Error>
                </Errors>
            </ValidationResult>";
    }
} 