using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using ApiGateway.Models;

public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        var request = new AuthRequest
        {
            Username = "testuser",
            Password = "password",
            ProgramCode = "TEST"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AuthRequest
        {
            Username = "invalid",
            Password = "wrong",
            ProgramCode = "TEST"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        var loginRequest = new AuthRequest
        {
            Username = "testuser",
            Password = "password",
            ProgramCode = "TEST"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResult>();

        var refreshRequest = new RefreshRequest
        {
            RefreshToken = authResult.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.NotEqual(authResult.AccessToken, result.AccessToken);
        Assert.NotEqual(authResult.RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task Logout_ValidToken_SuccessfullyLogs()
    {
        // Arrange
        var loginRequest = new AuthRequest
        {
            Username = "testuser",
            Password = "password",
            ProgramCode = "TEST"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResult>();

        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

        var logoutRequest = new LogoutRequest
        {
            RefreshToken = authResult.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify refresh token is no longer valid
        var refreshRequest = new RefreshRequest
        {
            RefreshToken = authResult.RefreshToken
        };
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }
} 