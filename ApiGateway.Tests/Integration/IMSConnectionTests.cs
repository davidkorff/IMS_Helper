using System;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using ApiGateway.Models;

public class IMSConnectionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testUserId;
    private readonly string _authToken;

    public IMSConnectionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        
        // Setup test user and auth token
        (_testUserId, _authToken) = TestHelper.CreateTestUserAndGetToken(_factory);
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _authToken);
    }

    [Fact]
    public async Task CreateConnection_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var request = new CreateIMSConnectionRequest
        {
            ConnectionName = "Test IMS",
            Environment = "Test",
            Credentials = new IMSCredentials
            {
                Username = "test",
                Password = "test123",
                AccountId = "123",
                Url = "https://test-ims.example.com"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ims-connections", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IMSConnectionResponse>();
        Assert.NotNull(result);
        Assert.Equal(request.ConnectionName, result.ConnectionName);
        Assert.Equal(IMSConnectionStatus.Active, result.Status);
    }

    [Fact]
    public async Task TestConnection_WithValidConnection_ReturnsSuccess()
    {
        // Arrange
        var connection = await CreateTestConnection();

        // Act
        var response = await _client.PostAsync(
            $"/api/ims-connections/{connection.Id}/test", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TestConnectionResponse>();
        Assert.True(result.Success);
        Assert.NotNull(result.Permissions);
    }

    private async Task<IMSConnectionResponse> CreateTestConnection()
    {
        // Helper method to create a test connection
        var request = new CreateIMSConnectionRequest
        {
            ConnectionName = "Test Connection",
            Environment = "Test",
            Credentials = new IMSCredentials
            {
                Username = "test",
                Password = "test123",
                AccountId = "123",
                Url = "https://test-ims.example.com"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/ims-connections", request);
        return await response.Content.ReadFromJsonAsync<IMSConnectionResponse>();
    }
} 