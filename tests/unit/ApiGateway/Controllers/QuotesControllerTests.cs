using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using ApiGateway.Controllers;
using ApiGateway.Services;
using ApiGateway.Models;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

public class QuotesControllerTests
{
    private readonly QuotesController _controller;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<QuotesController>> _loggerMock;
    private readonly Mock<IValidator<CreateQuoteRequest>> _validatorMock;

    public QuotesControllerTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<QuotesController>>();
        _validatorMock = new Mock<IValidator<CreateQuoteRequest>>();

        _controller = new QuotesController(
            _imsClientMock.Object,
            _loggerMock.Object,
            _validatorMock.Object);
    }

    [Fact]
    public async Task CreateQuote_ValidRequest_ReturnsQuote()
    {
        // Arrange
        var request = CreateValidQuoteRequest();
        var expectedResponse = new QuoteResponse { QuoteId = "Q123" };
        
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<CreateQuoteRequest>(), default))
            .ReturnsAsync(new ValidationResult());
            
        _imsClientMock.Setup(x => x.CreateQuote(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.CreateQuote(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<QuoteResponse>(okResult.Value);
        Assert.Equal(expectedResponse.QuoteId, response.QuoteId);
    }

    [Fact]
    public async Task CreateQuote_InvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var request = CreateValidQuoteRequest();
        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ProgramCode", "Program code is required")
        };
        
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<CreateQuoteRequest>(), default))
            .ReturnsAsync(new ValidationResult(validationFailures));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _controller.CreateQuote(request));
    }

    private CreateQuoteRequest CreateValidQuoteRequest()
    {
        return new CreateQuoteRequest
        {
            ProgramCode = "TEST",
            Insured = new InsuredInfo
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                Phone = "1234567890",
                Address = new AddressInfo
                {
                    Street1 = "123 Main St",
                    City = "Anytown",
                    State = "ST",
                    ZipCode = "12345"
                }
            },
            // ... additional test data
        };
    }
} 