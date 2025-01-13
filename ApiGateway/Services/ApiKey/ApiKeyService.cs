using System.Security.Cryptography;

public class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _repository;
    private readonly ILogger<ApiKeyService> _logger;
    private readonly IMemoryCache _cache;

    public ApiKeyService(
        IApiKeyRepository repository,
        ILogger<ApiKeyService> logger,
        IMemoryCache cache)
    {
        _repository = repository;
        _logger = logger;
        _cache = cache;
    }

    public async Task<ApiKey> GenerateApiKeyAsync(string accountId, ApiKeyOptions options)
    {
        var key = GenerateSecureApiKey();
        var hashedKey = HashApiKey(key);

        var apiKey = new ApiKey
        {
            AccountId = accountId,
            KeyHash = hashedKey,
            Name = options.Name,
            ExpiresAt = options.ExpiresAt,
            Permissions = options.Permissions,
            Environment = options.Environment,
            RateLimit = options.RateLimit,
            CreatedAt = DateTime.UtcNow,
            Status = ApiKeyStatus.Active
        };

        await _repository.CreateAsync(apiKey);
        _logger.LogInformation("Generated new API key for account {AccountId}", accountId);

        // Return the unhashed key only once
        apiKey.PlaintextKey = key;
        return apiKey;
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        var cacheKey = $"apikey:{apiKey}";
        
        if (_cache.TryGetValue<bool>(cacheKey, out var isValid))
        {
            return isValid;
        }

        var hashedKey = HashApiKey(apiKey);
        var keyDetails = await _repository.GetByHashAsync(hashedKey);

        isValid = keyDetails != null && 
                 keyDetails.Status == ApiKeyStatus.Active &&
                 (!keyDetails.ExpiresAt.HasValue || keyDetails.ExpiresAt > DateTime.UtcNow);

        _cache.Set(cacheKey, isValid, TimeSpan.FromMinutes(5));
        return isValid;
    }

    public async Task RevokeApiKeyAsync(string accountId, string keyId)
    {
        var apiKey = await _repository.GetByIdAsync(keyId);
        if (apiKey == null || apiKey.AccountId != accountId)
        {
            throw new NotFoundException("API key not found");
        }

        apiKey.Status = ApiKeyStatus.Revoked;
        apiKey.RevokedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(apiKey);
        _logger.LogInformation("Revoked API key {KeyId} for account {AccountId}", keyId, accountId);
    }

    private string GenerateSecureApiKey()
    {
        var keyBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        return Convert.ToBase64String(keyBytes);
    }

    private string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(hashBytes);
    }
} 