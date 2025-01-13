using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<TokenService>> _loggerMock;
    private readonly IMSAuthenticationSettings _settings;

    public TokenServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<TokenService>>();
        _settings = new IMSAuthenticationSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = "test-super-secret-key-thats-long-enough-for-hmacsha256",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        _tokenService = new TokenService(
            _cacheMock.Object,
            _loggerMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ValidClaims_ReturnsValidToken()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("permission", "read")
        };

        // Act
        var token = await _tokenService.GenerateAccessTokenAsync(claims);

        // Assert
        Assert.NotNull(token);
        var principal = await _tokenService.ValidateAccessTokenAsync(token);
        Assert.Equal("testuser", principal.Identity.Name);
        Assert.Contains(principal.Claims, c => 
            c.Type == "permission" && c.Value == "read");
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_ValidInput_StoresTokenInfo()
    {
        // Arrange
        string capturedKey = null;
        string capturedValue = null;
        _cacheMock
            .Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, DistributedCacheEntryOptions, CancellationToken>(
                (key, value, options, token) =>
                {
                    capturedKey = key;
                    capturedValue = value;
                });

        // Act
        var token = await _tokenService.GenerateRefreshTokenAsync(
            "testuser", 
            "session123");

        // Assert
        Assert.NotNull(token);
        Assert.StartsWith("refresh_token:", capturedKey);
        Assert.Contains("testuser", capturedValue);
        Assert.Contains("session123", capturedValue);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ValidToken_ReturnsTokenInfo()
    {
        // Arrange
        var token = "valid_token";
        var tokenInfo = new TokenInfo
        {
            Username = "testuser",
            SessionToken = "session123",
            IsValid = true,
            CreatedAt = DateTime.UtcNow
        };

        _cacheMock
            .Setup(x => x.GetStringAsync(
                $"refresh_token:{token}",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(tokenInfo));

        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync(token);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(tokenInfo.Username, result.Username);
        Assert.Equal(tokenInfo.SessionToken, result.SessionToken);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_InvalidToken_ReturnsInvalidResult()
    {
        // Arrange
        var token = "invalid_token";
        _cacheMock
            .Setup(x => x.GetStringAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null);

        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync(token);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ValidToken_RemovesFromCache()
    {
        // Arrange
        var token = "token_to_revoke";
        string capturedKey = null;
        _cacheMock
            .Setup(x => x.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, token) => 
                capturedKey = key);

        // Act
        await _tokenService.RevokeRefreshTokenAsync(token);

        // Assert
        Assert.Equal($"refresh_token:{token}", capturedKey);
    }
} 