public interface ITokenService
{
    Task<string> GetToken(string apiKey);
    Task<bool> ValidateToken(string token);
    Task InvalidateToken(string token);
} 