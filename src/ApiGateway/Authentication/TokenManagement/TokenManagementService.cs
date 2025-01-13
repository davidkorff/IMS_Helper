public interface ITokenManagementService
{
    Task<TokenMetadata> StoreTokenAsync(string userId, string accessToken, string refreshToken);
    Task<bool> ValidateTokenPairAsync(string accessToken, string refreshToken);
    Task<TokenMetadata> GetTokenMetadataAsync(string tokenId);
    Task RevokeUserTokensAsync(string userId);
    Task RevokeTokenAsync(string tokenId);
    Task<IEnumerable<TokenMetadata>> GetActiveTokensForUserAsync(string userId);
    Task<TokenUsageStats> GetTokenUsageStatsAsync(string userId);
    Task UpdateLastUsedAsync(string tokenId);
    Task CleanupExpiredTokensAsync();
}

public class TokenManagementService : ITokenManagementService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<TokenManagementService> _logger;
    private readonly TokenManagementSettings _settings;
    private readonly ITokenService _tokenService;

    public TokenManagementService(
        IDistributedCache cache,
        ILogger<TokenManagementService> logger,
        IOptions<TokenManagementSettings> settings,
        ITokenService tokenService)
    {
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;
        _tokenService = tokenService;
    }

    public async Task<TokenMetadata> StoreTokenAsync(
        string userId, 
        string accessToken, 
        string refreshToken)
    {
        var tokenId = Guid.NewGuid().ToString();
        var metadata = new TokenMetadata
        {
            TokenId = tokenId,
            UserId = userId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            RefreshExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            Status = TokenStatus.Active
        };

        await _cache.SetStringAsync(
            $"token:{tokenId}",
            JsonSerializer.Serialize(metadata),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = 
                    TimeSpan.FromDays(_settings.RefreshTokenExpirationDays)
            });

        await _cache.SetStringAsync(
            $"user_tokens:{userId}:{tokenId}",
            tokenId,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = 
                    TimeSpan.FromDays(_settings.RefreshTokenExpirationDays)
            });

        _logger.LogInformation(
            "Stored new token {TokenId} for user {UserId}", 
            tokenId, 
            userId);

        return metadata;
    }

    public async Task<bool> ValidateTokenPairAsync(
        string accessToken, 
        string refreshToken)
    {
        try
        {
            var principal = await _tokenService.ValidateAccessTokenAsync(accessToken);
            var tokenInfo = await _tokenService.ValidateRefreshTokenAsync(refreshToken);

            if (principal == null || !tokenInfo.IsValid)
            {
                return false;
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var activeTokens = await GetActiveTokensForUserAsync(userId);

            return activeTokens.Any(t => 
                t.AccessToken == accessToken && 
                t.RefreshToken == refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token pair validation failed");
            return false;
        }
    }

    public async Task<TokenMetadata> GetTokenMetadataAsync(string tokenId)
    {
        var json = await _cache.GetStringAsync($"token:{tokenId}");
        return json != null ? 
            JsonSerializer.Deserialize<TokenMetadata>(json) : 
            null;
    }

    public async Task RevokeUserTokensAsync(string userId)
    {
        var pattern = $"user_tokens:{userId}:*";
        var tokens = await GetActiveTokensForUserAsync(userId);

        foreach (var token in tokens)
        {
            await RevokeTokenAsync(token.TokenId);
        }

        _logger.LogInformation(
            "Revoked all tokens for user {UserId}", 
            userId);
    }

    public async Task RevokeTokenAsync(string tokenId)
    {
        var metadata = await GetTokenMetadataAsync(tokenId);
        if (metadata != null)
        {
            metadata.Status = TokenStatus.Revoked;
            metadata.RevokedAt = DateTime.UtcNow;

            await _cache.SetStringAsync(
                $"token:{tokenId}",
                JsonSerializer.Serialize(metadata),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = 
                        TimeSpan.FromHours(_settings.RevokedTokenRetentionHours)
                });

            await _cache.RemoveAsync($"user_tokens:{metadata.UserId}:{tokenId}");

            _logger.LogInformation(
                "Revoked token {TokenId} for user {UserId}", 
                tokenId, 
                metadata.UserId);
        }
    }

    public async Task<IEnumerable<TokenMetadata>> GetActiveTokensForUserAsync(
        string userId)
    {
        var pattern = $"user_tokens:{userId}:*";
        var tokens = new List<TokenMetadata>();

        // Note: This is a simplified implementation. In production,
        // you'd want to use a database with proper querying capabilities
        var tokenIds = await GetUserTokenIdsAsync(userId);
        foreach (var tokenId in tokenIds)
        {
            var metadata = await GetTokenMetadataAsync(tokenId);
            if (metadata?.Status == TokenStatus.Active)
            {
                tokens.Add(metadata);
            }
        }

        return tokens;
    }

    public async Task<TokenUsageStats> GetTokenUsageStatsAsync(string userId)
    {
        var tokens = await GetActiveTokensForUserAsync(userId);
        return new TokenUsageStats
        {
            ActiveTokenCount = tokens.Count(),
            OldestTokenCreatedAt = tokens.Min(t => t.CreatedAt),
            NewestTokenCreatedAt = tokens.Max(t => t.CreatedAt),
            LastTokenUseAt = tokens.Max(t => t.LastUsed)
        };
    }

    public async Task UpdateLastUsedAsync(string tokenId)
    {
        var metadata = await GetTokenMetadataAsync(tokenId);
        if (metadata != null)
        {
            metadata.LastUsed = DateTime.UtcNow;
            await _cache.SetStringAsync(
                $"token:{tokenId}",
                JsonSerializer.Serialize(metadata));
        }
    }

    public async Task CleanupExpiredTokensAsync()
    {
        // Note: This is a simplified implementation. In production,
        // you'd want to use a database with proper querying capabilities
        _logger.LogInformation("Starting expired token cleanup");

        // Implementation would depend on your storage mechanism
        // Here we're just logging that it would happen
        _logger.LogInformation("Expired token cleanup completed");
    }

    private async Task<IEnumerable<string>> GetUserTokenIdsAsync(string userId)
    {
        // Note: This is a simplified implementation. In production,
        // you'd want to use a database with proper querying capabilities
        var pattern = $"user_tokens:{userId}:*";
        var tokens = new List<string>();

        // Implementation would depend on your cache/storage mechanism
        return tokens;
    }
}

public class TokenMetadata
{
    public string TokenId { get; set; }
    public string UserId { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsed { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime RefreshExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public TokenStatus Status { get; set; }
    public Dictionary<string, string> AdditionalInfo { get; set; }
}

public class TokenUsageStats
{
    public int ActiveTokenCount { get; set; }
    public DateTime? OldestTokenCreatedAt { get; set; }
    public DateTime? NewestTokenCreatedAt { get; set; }
    public DateTime? LastTokenUseAt { get; set; }
}

public enum TokenStatus
{
    Active,
    Revoked,
    Expired
} 