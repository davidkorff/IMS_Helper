using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class TokenManagementServiceTests
{
    private readonly TokenManagementService _service;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<TokenManagementService>> _loggerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly TokenManagementSettings _settings;

    public TokenManagementServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<TokenManagementService>>();
        _tokenServiceMock = new Mock<ITokenService>();
        _settings = new TokenManagementSettings();

        _service = new TokenManagementService(
            _cacheMock.Object,
            _loggerMock.Object,
            Options.Create(_settings),
            _tokenServiceMock.Object);

        SetupDefaultMocks();
    }

    [Fact]
    public async Task StoreTokenAsync_ValidTokens_StoresMetadata()
    {
        // Arrange
        var userId = "user123";
        var accessToken = "access123";
        var refreshToken = "refresh123";

        string capturedTokenKey = null;
        string capturedUserKey = null;

        _cacheMock
            .Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, DistributedCacheEntryOptions, CancellationToken>(
                (key, value, options, token) =>
                {
                    if (key.StartsWith("token:"))
                        capturedTokenKey = key;
                    else if (key.StartsWith("user_tokens:"))
                        capturedUserKey = key;
                });

        // Act
        var result = await _service.StoreTokenAsync(userId, accessToken, refreshToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(accessToken, result.AccessToken);
        Assert.Equal(refreshToken, result.RefreshToken);
        Assert.Equal(TokenStatus.Active, result.Status);
        Assert.NotNull(capturedTokenKey);
        Assert.NotNull(capturedUserKey);
    }

    [Fact]
    public async Task ValidateTokenPairAsync_ValidTokens_ReturnsTrue()
    {
        // Arrange
        var accessToken = "valid_access";
        var refreshToken = "valid_refresh";
        var userId = "user123";

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }));

        _tokenServiceMock
            .Setup(x => x.ValidateAccessTokenAsync(accessToken))
            .ReturnsAsync(principal);

        _tokenServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(refreshToken))
            .ReturnsAsync(new TokenInfo { IsValid = true });

        // Act
        var result = await _service.ValidateTokenPairAsync(accessToken, refreshToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RevokeTokenAsync_ValidToken_RevokesAndUpdatesMetadata()
    {
        // Arrange
        var tokenId = "token123";
        var metadata = new TokenMetadata
        {
            TokenId = tokenId,
            UserId = "user123",
            Status = TokenStatus.Active
        };

        _cacheMock
            .Setup(x => x.GetStringAsync(
                $"token:{tokenId}",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(metadata));

        // Act
        await _service.RevokeTokenAsync(tokenId);

        // Assert
        _cacheMock.Verify(x => x.SetStringAsync(
            $"token:{tokenId}",
            It.Is<string>(s => s.Contains("Revoked")),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetTokenUsageStatsAsync_WithActiveTokens_ReturnsStats()
    {
        // Arrange
        var userId = "user123";
        var now = DateTime.UtcNow;
        var tokens = new[]
        {
            new TokenMetadata
            {
                CreatedAt = now.AddDays(-5),
                LastUsed = now.AddHours(-1),
                Status = TokenStatus.Active
            },
            new TokenMetadata
            {
                CreatedAt = now.AddDays(-1),
                LastUsed = now,
                Status = TokenStatus.Active
            }
        };

        SetupMockTokens(userId, tokens);

        // Act
        var stats = await _service.GetTokenUsageStatsAsync(userId);

        // Assert
        Assert.Equal(2, stats.ActiveTokenCount);
        Assert.Equal(now.AddDays(-5), stats.OldestTokenCreatedAt);
        Assert.Equal(now.AddDays(-1), stats.NewestTokenCreatedAt);
        Assert.Equal(now, stats.LastTokenUseAt);
    }

    private void SetupDefaultMocks()
    {
        _cacheMock
            .Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupMockTokens(string userId, IEnumerable<TokenMetadata> tokens)
    {
        var tokenIds = tokens.Select(t => Guid.NewGuid().ToString()).ToList();
        
        for (int i = 0; i < tokens.Count(); i++)
        {
            var token = tokens.ElementAt(i);
            token.TokenId = tokenIds[i];
            _cacheMock
                .Setup(x => x.GetStringAsync(
                    $"token:{tokenIds[i]}",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(JsonSerializer.Serialize(token));
        }
    }
} 