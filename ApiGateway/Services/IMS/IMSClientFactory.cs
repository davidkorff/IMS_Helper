public interface IIMSClientFactory
{
    IIMSClient CreateClient();
}

public class IMSClientFactory : IIMSClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMSMetrics _metrics;
    private readonly IMSRetryPolicy _retryPolicy;
    private readonly IMSResponseCache _cache;
    private readonly ILogger<IMSClient> _logger;

    public IMSClientFactory(
        IHttpClientFactory httpClientFactory,
        IMSMetrics metrics,
        IMSRetryPolicy retryPolicy,
        IMSResponseCache cache,
        ILogger<IMSClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _metrics = metrics;
        _retryPolicy = retryPolicy;
        _cache = cache;
        _logger = logger;
    }

    public IIMSClient CreateClient()
    {
        var httpClient = _httpClientFactory.CreateClient("IMS");
        
        return new ResilientIMSClient(
            new IMSClient(httpClient, _logger),
            _metrics,
            _retryPolicy,
            _cache,
            _logger);
    }
}

public class ResilientIMSClient : IIMSClient
{
    private readonly IIMSClient _inner;
    private readonly IMSMetrics _metrics;
    private readonly IMSRetryPolicy _retryPolicy;
    private readonly IMSResponseCache _cache;
    private readonly ILogger _logger;

    public ResilientIMSClient(
        IIMSClient inner,
        IMSMetrics metrics,
        IMSRetryPolicy retryPolicy,
        IMSResponseCache cache,
        ILogger logger)
    {
        _inner = inner;
        _metrics = metrics;
        _retryPolicy = retryPolicy;
        _cache = cache;
        _logger = logger;
    }

    public async Task AuthenticateAsync(IMSCredentials credentials)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _retryPolicy.ExecuteAsync(
                () => _inner.AuthenticateAsync(credentials),
                "authenticate");
            
            _metrics.RecordRequest("authenticate", "POST");
            _metrics.RecordResponseTime("authenticate", sw.Elapsed);
        }
        catch (Exception ex)
        {
            _metrics.RecordError("authenticate", ex.GetType().Name);
            throw;
        }
    }

    public async Task<Dictionary<string, bool>> GetPermissionsAsync()
    {
        return await _cache.GetOrSetAsync(
            "permissions",
            async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await _retryPolicy.ExecuteAsync(
                        () => _inner.GetPermissionsAsync(),
                        "get_permissions");
                    
                    _metrics.RecordRequest("permissions", "GET");
                    _metrics.RecordResponseTime("permissions", sw.Elapsed);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _metrics.RecordError("permissions", ex.GetType().Name);
                    throw;
                }
            });
    }

    public async Task<bool> ValidateSessionAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _retryPolicy.ExecuteAsync(
                () => _inner.ValidateSessionAsync(),
                "validate_session");
            
            _metrics.RecordRequest("validate_session", "GET");
            _metrics.RecordResponseTime("validate_session", sw.Elapsed);
            
            return result;
        }
        catch (Exception ex)
        {
            _metrics.RecordError("validate_session", ex.GetType().Name);
            throw;
        }
    }

    public async Task<T> ExecuteRequestAsync<T>(string endpoint, object request = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _retryPolicy.ExecuteAsync(
                () => _inner.ExecuteRequestAsync<T>(endpoint, request),
                endpoint);
            
            _metrics.RecordRequest(endpoint, request != null ? "POST" : "GET");
            _metrics.RecordResponseTime(endpoint, sw.Elapsed);
            
            return result;
        }
        catch (Exception ex)
        {
            _metrics.RecordError(endpoint, ex.GetType().Name);
            throw;
        }
    }
} 