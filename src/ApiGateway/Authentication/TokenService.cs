public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(IEnumerable<Claim> claims);
    Task<string> GenerateRefreshTokenAsync(string username, string sessionToken);
    Task<TokenInfo> ValidateRefreshTokenAsync(string refreshToken);
    Task<ClaimsPrincipal> ValidateAccessTokenAsync(string accessToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
}

public class TokenService : ITokenService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<TokenService> _logger;
    private readonly IMSAuthenticationSettings _settings;

    public TokenService(
        IDistributedCache cache,
        ILogger<TokenService> logger,
        IOptions<IMSAuthenticationSettings> settings)
    {
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<string> GenerateAccessTokenAsync(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(
            key, 
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(
        string username, 
        string sessionToken)
    {
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var tokenInfo = new TokenInfo
        {
            Username = username,
            SessionToken = sessionToken,
            IsValid = true,
            CreatedAt = DateTime.UtcNow
        };

        await _cache.SetStringAsync(
            $"refresh_token:{refreshToken}",
            JsonSerializer.Serialize(tokenInfo),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = 
                    TimeSpan.FromDays(_settings.RefreshTokenExpirationDays)
            });

        return refreshToken;
    }

    public async Task<TokenInfo> ValidateRefreshTokenAsync(string refreshToken)
    {
        var tokenJson = await _cache.GetStringAsync($"refresh_token:{refreshToken}");
        if (string.IsNullOrEmpty(tokenJson))
        {
            return new TokenInfo { IsValid = false };
        }

        var tokenInfo = JsonSerializer.Deserialize<TokenInfo>(tokenJson);
        return tokenInfo;
    }

    public async Task<ClaimsPrincipal> ValidateAccessTokenAsync(string accessToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = _settings.ValidateIssuer,
            ValidateAudience = _settings.ValidateAudience,
            ValidateLifetime = _settings.ValidateLifetime,
            ValidateIssuerSigningKey = _settings.ValidateIssuerSigningKey,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_settings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(_settings.ClockSkewMinutes)
        };

        return tokenHandler.ValidateToken(
            accessToken, 
            validationParameters, 
            out _);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        await _cache.RemoveAsync($"refresh_token:{refreshToken}");
    }
}

public class TokenInfo
{
    public string Username { get; set; }
    public string SessionToken { get; set; }
    public bool IsValid { get; set; }
    public DateTime CreatedAt { get; set; }
} 