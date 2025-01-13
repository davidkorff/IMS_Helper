using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using ApiGateway.Services.TokenManagement;

public class TokenServiceTests
{
    private readonly ITokenService _tokenService;
    private readonly Mock<IConfiguration> _configMock;

    public TokenServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _tokenService = new TokenService(_configMock.Object);
    }

    [Fact]
    public async Task GetToken_ValidApiKey_ReturnsToken()
    {
        // Arrange
        var apiKey = "valid-api-key";

        // Act
        var token = await _tokenService.GetToken(apiKey);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task ValidateToken_ValidToken_ReturnsTrue()
    {
        // Arrange
        var token = await _tokenService.GetToken("valid-api-key");

        // Act
        var isValid = await _tokenService.ValidateToken(token);

        // Assert
        Assert.True(isValid);
    }
} 