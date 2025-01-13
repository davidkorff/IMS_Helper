public interface IIMSAuthenticationService
{
    Task<AuthResult> AuthenticateAsync(AuthRequest request);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string token);
    Task<bool> ValidateTokenAsync(string token);
    Task<ClaimsPrincipal> GetClaimsFromTokenAsync(string token);
}

public class IMSAuthenticationService : IIMSAuthenticationService
{
    private readonly IIMSClient _imsClient;
    private readonly ITokenService _tokenService;
    private readonly ILogger<IMSAuthenticationService> _logger;
    private readonly IMSAuthenticationSettings _settings;

    public IMSAuthenticationService(
        IIMSClient imsClient,
        ITokenService tokenService,
        ILogger<IMSAuthenticationService> logger,
        IOptions<IMSAuthenticationSettings> settings)
    {
        _imsClient = imsClient;
        _tokenService = tokenService;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<AuthResult> AuthenticateAsync(AuthRequest request)
    {
        try
        {
            _logger.LogInformation("Authenticating user: {Username}", request.Username);

            // Validate credentials against IMS
            var imsAuthResult = await _imsClient.AuthenticateAsync(
                request.Username,
                request.Password,
                request.ProgramCode);

            if (!imsAuthResult.IsSuccessful)
            {
                _logger.LogWarning("Authentication failed for user: {Username}", 
                    request.Username);
                return AuthResult.Failed("Invalid credentials");
            }

            // Get user permissions from IMS
            var permissions = await _imsClient.GetUserPermissionsAsync(
                imsAuthResult.SessionToken);

            // Create claims for the user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim(ClaimTypes.NameIdentifier, imsAuthResult.UserId),
                new Claim("programCode", request.ProgramCode),
                new Claim("sessionToken", imsAuthResult.SessionToken)
            };

            // Add permission claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            // Generate JWT and refresh token
            var accessToken = await _tokenService.GenerateAccessTokenAsync(claims);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(
                request.Username,
                imsAuthResult.SessionToken);

            _logger.LogInformation("Successfully authenticated user: {Username}", 
                request.Username);

            return AuthResult.Success(
                accessToken,
                refreshToken,
                imsAuthResult.SessionToken,
                permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed for user: {Username}", 
                request.Username);
            throw new AuthenticationException("Authentication failed", ex);
        }
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            // Validate refresh token
            var tokenInfo = await _tokenService.ValidateRefreshTokenAsync(refreshToken);
            if (!tokenInfo.IsValid)
            {
                return AuthResult.Failed("Invalid refresh token");
            }

            // Verify IMS session is still valid
            var sessionValid = await _imsClient.ValidateSessionAsync(
                tokenInfo.SessionToken);
            if (!sessionValid)
            {
                await _tokenService.RevokeRefreshTokenAsync(refreshToken);
                return AuthResult.Failed("Session expired");
            }

            // Get updated permissions
            var permissions = await _imsClient.GetUserPermissionsAsync(
                tokenInfo.SessionToken);

            // Create new claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, tokenInfo.Username),
                new Claim("sessionToken", tokenInfo.SessionToken)
            };

            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            // Generate new tokens
            var newAccessToken = await _tokenService.GenerateAccessTokenAsync(claims);
            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(
                tokenInfo.Username,
                tokenInfo.SessionToken);

            // Revoke old refresh token
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);

            return AuthResult.Success(
                newAccessToken,
                newRefreshToken,
                tokenInfo.SessionToken,
                permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            throw new AuthenticationException("Token refresh failed", ex);
        }
    }

    public async Task RevokeTokenAsync(string token)
    {
        try
        {
            await _tokenService.RevokeRefreshTokenAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token revocation failed");
            throw new AuthenticationException("Token revocation failed", ex);
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var principal = await GetClaimsFromTokenAsync(token);
            return principal != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ClaimsPrincipal> GetClaimsFromTokenAsync(string token)
    {
        return await _tokenService.ValidateAccessTokenAsync(token);
    }
}

public class AuthRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string ProgramCode { get; set; }
}

public class AuthResult
{
    public bool IsSuccessful { get; private set; }
    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }
    public string SessionToken { get; private set; }
    public IEnumerable<string> Permissions { get; private set; }
    public string ErrorMessage { get; private set; }

    private AuthResult() { }

    public static AuthResult Success(
        string accessToken,
        string refreshToken,
        string sessionToken,
        IEnumerable<string> permissions)
    {
        return new AuthResult
        {
            IsSuccessful = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            SessionToken = sessionToken,
            Permissions = permissions
        };
    }

    public static AuthResult Failed(string errorMessage)
    {
        return new AuthResult
        {
            IsSuccessful = false,
            ErrorMessage = errorMessage
        };
    }
} 