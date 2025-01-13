using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Integration.Validation.Validators;
using Shared.Integration.Validation.Models;

public class PolicyRequestValidatorTests
{
    private readonly PolicyRequestValidator _validator;
    private readonly Mock<ILogger<PolicyRequestValidator>> _loggerMock;
    private readonly Mock<IStateService> _stateServiceMock;
    private readonly IMSValidationSettings _settings;

    public PolicyRequestValidatorTests()
    {
        _loggerMock = new Mock<ILogger<PolicyRequestValidator>>();
        _stateServiceMock = new Mock<IStateService>();
        
        _settings = new IMSValidationSettings
        {
            Behavior = new ValidationBehavior
            {
                StopOnFirstFailure = false,
                ValidateAllProperties = true
            }
        };

        _validator = new PolicyRequestValidator(
            _loggerMock.Object,
            _stateServiceMock.Object,
            Options.Create(_settings));

        SetupDefaultMocks();
    }

    [Fact]
    public async Task ValidateAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _validator.ValidateAsync(request, 
            new ValidationContext { Operation = "Create" });

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_MissingRequiredFields_ReturnsErrors()
    {
        // Arrange
        var request = new PolicyRequest();

        // Act
        var result = await _validator.ValidateAsync(request, 
            new ValidationContext { Operation = "Create" });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "PolicyNumber");
        Assert.Contains(result.Errors, e => e.Field == "EffectiveDate");
        Assert.Contains(result.Errors, e => e.Field == "State");
        Assert.Contains(result.Errors, e => e.Field == "LineOfBusiness");
    }

    [Fact]
    public async Task ValidateAsync_InvalidState_ReturnsError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.State = "Invalid";

        // Act
        var result = await _validator.ValidateAsync(request, 
            new ValidationContext { Operation = "Create" });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Field == "State" && e.Code == "VAL005");
    }

    [Fact]
    public async Task ValidateAsync_UnsupportedState_ReturnsError()
    {
        // Arrange
        var request = CreateValidRequest();
        _stateServiceMock
            .Setup(x => x.GetSupportedStatesAsync(request.LineOfBusiness))
            .ReturnsAsync(new[] { "CA", "NY" });

        // Act
        var result = await _validator.ValidateAsync(request, 
            new ValidationContext { Operation = "Create" });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Field == "State" && e.Code == "BUS001");
    }

    [Fact]
    public async Task ValidateAsync_InvalidDateRange_ReturnsError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.EffectiveDate = DateTime.Today.AddDays(-1);

        // Act
        var result = await _validator.ValidateAsync(request, 
            new ValidationContext { Operation = "Create" });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Field == "EffectiveDate" && e.Code == "VAL007");
    }

    [Fact]
    public async Task ValidateAsync_BindOperation_ValidatesPremium()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Premium = null;

        // Act
        var result = await _validator.ValidateAsync(request, 
            new ValidationContext { Operation = "Bind" });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Field == "Premium" && e.Code == "VAL002");
    }

    private PolicyRequest CreateValidRequest()
    {
        return new PolicyRequest
        {
            PolicyNumber = "POL123456",
            EffectiveDate = DateTime.Today,
            ExpirationDate = DateTime.Today.AddYears(1),
            State = "CA",
            LineOfBusiness = "GL",
            Status = "Active",
            Premium = 1000m
        };
    }

    private void SetupDefaultMocks()
    {
        _stateServiceMock
            .Setup(x => x.GetSupportedStatesAsync(It.IsAny<string>()))
            .ReturnsAsync(new[] { "CA", "NY", "TX" });
    }
} 