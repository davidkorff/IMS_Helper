using Xunit;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;

public class IMSValidationTests
{
    private readonly QuoteRequestValidator _quoteValidator;
    private readonly InsuredRequestValidator _insuredValidator;
    private readonly PolicyRequestValidator _policyValidator;

    public IMSValidationTests()
    {
        _quoteValidator = new QuoteRequestValidator();
        _insuredValidator = new InsuredRequestValidator();
        _policyValidator = new PolicyRequestValidator();
    }

    [Fact]
    public void QuoteRequest_ValidData_ShouldNotHaveValidationError()
    {
        // Arrange
        var request = new QuoteRequest
        {
            InsuredId = "INS123",
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            CoverageType = "Commercial",
            Limits = new List<CoverageLimit>
            {
                new CoverageLimit { Type = "General", Amount = 1000000 }
            }
        };

        // Act
        var result = _quoteValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void QuoteRequest_PastEffectiveDate_ShouldHaveValidationError()
    {
        // Arrange
        var request = new QuoteRequest
        {
            InsuredId = "INS123",
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CoverageType = "Commercial",
            Limits = new List<CoverageLimit>
            {
                new CoverageLimit { Type = "General", Amount = 1000000 }
            }
        };

        // Act
        var result = _quoteValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EffectiveDate);
    }

    [Fact]
    public void InsuredRequest_ValidData_ShouldNotHaveValidationError()
    {
        // Arrange
        var request = new InsuredRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "555-555-5555",
            Address = new Address
            {
                Street1 = "123 Main St",
                City = "Anytown",
                State = "NY",
                Zip = "12345"
            }
        };

        // Act
        var result = _insuredValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "Doe", "Invalid first name")]
    [InlineData("John", "", "Invalid last name")]
    [InlineData("John", "Doe", "invalid.email")]
    public void InsuredRequest_InvalidData_ShouldHaveValidationError(
        string firstName, 
        string lastName, 
        string email)
    {
        // Arrange
        var request = new InsuredRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = "555-555-5555",
            Address = new Address
            {
                Street1 = "123 Main St",
                City = "Anytown",
                State = "NY",
                Zip = "12345"
            }
        };

        // Act
        var result = _insuredValidator.TestValidate(request);

        // Assert
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void PolicyRequest_ValidData_ShouldNotHaveValidationError()
    {
        // Arrange
        var request = new PolicyRequest
        {
            QuoteId = "Q123",
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            PaymentPlan = "Monthly",
            Documents = new List<DocumentInfo>
            {
                new DocumentInfo 
                { 
                    Id = "DOC1", 
                    Type = "Application" 
                }
            }
        };

        // Act
        var result = _policyValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PolicyRequest_NoDocuments_ShouldHaveValidationError()
    {
        // Arrange
        var request = new PolicyRequest
        {
            QuoteId = "Q123",
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            PaymentPlan = "Monthly",
            Documents = new List<DocumentInfo>()
        };

        // Act
        var result = _policyValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Documents);
    }
}

public class ValidationMiddlewareTests
{
    private readonly TestServer _server;
    private readonly HttpClient _client;

    public ValidationMiddlewareTests()
    {
        var builder = new WebHostBuilder()
            .UseStartup<TestStartup>();
        _server = new TestServer(builder);
        _client = _server.CreateClient();
    }

    [Fact]
    public async Task ValidationMiddleware_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new QuoteRequest(); // Invalid request with no data

        // Act
        var response = await _client.PostAsJsonAsync("/api/quotes", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotEmpty(error.Errors);
    }

    [Fact]
    public async Task ValidationMiddleware_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new QuoteRequest
        {
            InsuredId = "INS123",
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            CoverageType = "Commercial",
            Limits = new List<CoverageLimit>
            {
                new CoverageLimit { Type = "General", Amount = 1000000 }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/quotes", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
} 