using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApiGateway.Authentication;

public class IMSAuthenticationServiceTests
{
    private readonly IMSAuthenticationService _authService;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<IMSAuthenticationService>> _loggerMock;
    private readonly IMSAuthenticationSettings _settings;

    public IMSAuthenticationServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<IMSAuthenticationService>>();
        _settings = new IMSAuthenticationSettings();

        _authService = new IMSAuthenticationService(
            _imsClientMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object,
            Options.Create(_settings));

        SetupDefaultMocks();
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsSuccessResult()
    {
        // Arrange
        var request = new AuthRequest
        {
            Username = "testuser",
            Password = "password",
            ProgramCode = "TEST"
        };

        // Act
        var result = await _authService.AuthenticateAsync(request);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.NotEmpty(result.Permissions);
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidCredentials_ReturnsFailedResult()
    {
        // Arrange
        var request = new AuthRequest
        {
            Username = "invalid",
            Password = "wrong",
            ProgramCode = "TEST"
        };

        _imsClientMock
            .Setup(x => x.AuthenticateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(new IMSAuthResult { IsSuccessful = false });

        // Act
        var result = await _authService.AuthenticateAsync(request);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Equal("Invalid credentials", result.ErrorMessage);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        var refreshToken = "valid_refresh_token";
        var tokenInfo = new TokenInfo
        {
            Username = "testuser",
            SessionToken = "session_token",
            IsValid = true
        };

        _tokenServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(refreshToken))
            .ReturnsAsync(tokenInfo);

        // Act
        var result = await _authService.RefreshTokenAsync(refreshToken);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ReturnsFailedResult()
    {
        // Arrange
        var refreshToken = "invalid_refresh_token";
        _tokenServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(refreshToken))
            .ReturnsAsync(new TokenInfo { IsValid = false });

        // Act
        var result = await _authService.RefreshTokenAsync(refreshToken);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Equal("Invalid refresh token", result.ErrorMessage);
    }

    private void SetupDefaultMocks()
    {
        _imsClientMock
            .Setup(x => x.AuthenticateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(new IMSAuthResult 
            { 
                IsSuccessful = true,
                UserId = "user123",
                SessionToken = "session_token" 
            });

        _imsClientMock
            .Setup(x => x.GetUserPermissionsAsync(It.IsAny<string>()))
            .ReturnsAsync(new[] { "read", "write" });

        _imsClientMock
            .Setup(x => x.ValidateSessionAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _tokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync("access_token");

        _tokenServiceMock
            .Setup(x => x.GenerateRefreshTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync("refresh_token");
    }
} 