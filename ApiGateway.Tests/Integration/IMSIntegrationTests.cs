using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using Xunit;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Net.Http.Headers;

public class IMSIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testUserId;
    private readonly string _authToken;

    public IMSIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        (_testUserId, _authToken) = TestHelper.CreateTestUserAndGetToken(_factory);
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _authToken);
    }

    [Fact]
    public async Task FullConnectionLifecycle_Success()
    {
        // 1. Create Connection
        var createRequest = new CreateIMSConnectionRequest
        {
            ConnectionName = "Integration Test IMS",
            Environment = "Test",
            Credentials = new IMSCredentials
            {
                Username = "test_user",
                Password = "test_pass",
                AccountId = "test_account",
                Url = "https://test-ims.example.com"
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/ims-connections", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var connection = await createResponse.Content.ReadFromJsonAsync<IMSConnectionResponse>();
        Assert.NotNull(connection);

        // 2. Test Connection
        var testResponse = await _client.PostAsync(
            $"/api/ims-connections/{connection.Id}/test", null);
        Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
        var testResult = await testResponse.Content.ReadFromJsonAsync<TestConnectionResponse>();
        Assert.True(testResult.Success);

        // 3. Update Connection
        var updateRequest = new UpdateIMSConnectionRequest
        {
            ConnectionName = "Updated Integration Test IMS",
            Credentials = createRequest.Credentials
        };
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/ims-connections/{connection.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 4. Verify Update
        var getResponse = await _client.GetAsync($"/api/ims-connections/{connection.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var updatedConnection = await getResponse.Content
            .ReadFromJsonAsync<IMSConnectionResponse>();
        Assert.Equal(updateRequest.ConnectionName, updatedConnection.ConnectionName);

        // 5. Delete Connection
        var deleteResponse = await _client.DeleteAsync($"/api/ims-connections/{connection.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 6. Verify Deletion
        var verifyDeleteResponse = await _client.GetAsync($"/api/ims-connections/{connection.Id}");
        Assert.Equal(HttpStatusCode.NotFound, verifyDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task ConnectionPermissions_AreCorrectlyRetrieved()
    {
        // Create connection
        var connection = await CreateTestConnection();

        // Test permissions
        var testResponse = await _client.PostAsync(
            $"/api/ims-connections/{connection.Id}/test", null);
        var testResult = await testResponse.Content.ReadFromJsonAsync<TestConnectionResponse>();
        
        Assert.NotNull(testResult.Permissions);
        Assert.Contains(testResult.Permissions, p => p.Key == "read:submissions");
        Assert.Contains(testResult.Permissions, p => p.Key == "write:submissions");
    }

    [Fact]
    public async Task MultipleConnections_AreProperlyIsolated()
    {
        // Create two connections
        var connection1 = await CreateTestConnection("Connection 1");
        var connection2 = await CreateTestConnection("Connection 2");

        // Verify they're separate
        var response = await _client.GetAsync("/api/ims-connections");
        var connections = await response.Content
            .ReadFromJsonAsync<List<IMSConnectionResponse>>();
        
        Assert.Contains(connections, c => c.Id == connection1.Id);
        Assert.Contains(connections, c => c.Id == connection2.Id);
        Assert.NotEqual(connection1.Id, connection2.Id);
    }

    [Fact]
    public async Task InvalidCredentials_ReturnsAppropriateError()
    {
        var request = new CreateIMSConnectionRequest
        {
            ConnectionName = "Invalid Credentials Test",
            Environment = "Test",
            Credentials = new IMSCredentials
            {
                Username = "invalid",
                Password = "invalid",
                AccountId = "invalid",
                Url = "https://test-ims.example.com"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/ims-connections", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Authentication failed", error.Errors[0]);
    }

    private async Task<IMSConnectionResponse> CreateTestConnection(string name = "Test Connection")
    {
        var request = new CreateIMSConnectionRequest
        {
            ConnectionName = name,
            Environment = "Test",
            Credentials = new IMSCredentials
            {
                Username = "test_user",
                Password = "test_pass",
                AccountId = "test_account",
                Url = "https://test-ims.example.com"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/ims-connections", request);
        return await response.Content.ReadFromJsonAsync<IMSConnectionResponse>();
    }
}

[MemoryDiagnoser]
public class IMSValidationPerformanceTests
{
    private readonly QuoteRequestValidator _quoteValidator;
    private readonly InsuredRequestValidator _insuredValidator;
    private readonly PolicyRequestValidator _policyValidator;
    private readonly QuoteRequest _validQuoteRequest;
    private readonly InsuredRequest _validInsuredRequest;
    private readonly PolicyRequest _validPolicyRequest;

    public IMSValidationPerformanceTests()
    {
        _quoteValidator = new QuoteRequestValidator();
        _insuredValidator = new InsuredRequestValidator();
        _policyValidator = new PolicyRequestValidator();

        // Initialize valid test data
        _validQuoteRequest = new QuoteRequest
        {
            InsuredId = "INS123",
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            CoverageType = "Commercial",
            Limits = new List<CoverageLimit>
            {
                new CoverageLimit { Type = "General", Amount = 1000000 }
            }
        };

        _validInsuredRequest = new InsuredRequest
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

        _validPolicyRequest = new PolicyRequest
        {
            QuoteId = "Q123",
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            PaymentPlan = "Monthly",
            Documents = new List<DocumentInfo>
            {
                new DocumentInfo { Id = "DOC1", Type = "Application" }
            }
        };
    }

    [Benchmark]
    public void ValidateQuoteRequest_Performance()
    {
        _quoteValidator.Validate(_validQuoteRequest);
    }

    [Benchmark]
    public void ValidateInsuredRequest_Performance()
    {
        _insuredValidator.Validate(_validInsuredRequest);
    }

    [Benchmark]
    public void ValidatePolicyRequest_Performance()
    {
        _policyValidator.Validate(_validPolicyRequest);
    }

    [Benchmark]
    public void ValidateAllRequests_Performance()
    {
        _quoteValidator.Validate(_validQuoteRequest);
        _insuredValidator.Validate(_validInsuredRequest);
        _policyValidator.Validate(_validPolicyRequest);
    }
}

public class TestServerFixture : IDisposable
{
    public TestServer Server { get; }
    public HttpClient Client { get; }

    public TestServerFixture()
    {
        var builder = new WebHostBuilder()
            .UseStartup<TestStartup>();
        
        Server = new TestServer(builder);
        Client = Server.CreateClient();
    }

    public T GetService<T>()
    {
        return (T)Server.Services.GetService(typeof(T));
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }
} 