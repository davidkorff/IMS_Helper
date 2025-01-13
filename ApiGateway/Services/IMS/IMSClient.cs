public interface IIMSClient
{
    Task AuthenticateAsync(IMSCredentials credentials);
    Task<Dictionary<string, bool>> GetPermissionsAsync();
    Task<bool> ValidateSessionAsync();
    Task<T> ExecuteRequestAsync<T>(string endpoint, object request = null);
}

public class IMSClient : IIMSClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IMSClient> _logger;
    private IMSCredentials _credentials;
    private string _sessionToken;

    public IMSClient(HttpClient httpClient, ILogger<IMSClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task AuthenticateAsync(IMSCredentials credentials)
    {
        _credentials = credentials;
        _httpClient.BaseAddress = new Uri(credentials.Url);

        var request = new
        {
            username = credentials.Username,
            password = credentials.Password,
            accountId = credentials.AccountId
        };

        try
        {
            var response = await ExecuteRequestAsync<AuthResponse>("/auth", request);
            _sessionToken = response.SessionToken;
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _sessionToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed for account {AccountId}", 
                credentials.AccountId);
            throw new IMSAuthenticationException("Authentication failed", ex);
        }
    }

    public async Task<Dictionary<string, bool>> GetPermissionsAsync()
    {
        try
        {
            var response = await ExecuteRequestAsync<PermissionsResponse>("/permissions");
            return response.Permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions");
            throw new IMSException("Failed to get permissions", ex);
        }
    }

    public async Task<bool> ValidateSessionAsync()
    {
        try
        {
            await ExecuteRequestAsync<object>("/validate-session");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<T> ExecuteRequestAsync<T>(string endpoint, object request = null)
    {
        try
        {
            HttpResponseMessage response;
            
            if (request != null)
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(request), 
                    Encoding.UTF8, 
                    "application/json");
                response = await _httpClient.PostAsync(endpoint, content);
            }
            else
            {
                response = await _httpClient.GetAsync(endpoint);
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "IMS request failed: {Endpoint}", endpoint);
            throw new IMSException($"IMS request failed: {endpoint}", ex);
        }
    }
} 