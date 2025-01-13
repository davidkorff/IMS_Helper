public interface IApiKeyRepository
{
    Task<ApiKey> CreateAsync(ApiKey apiKey);
    Task<ApiKey> GetByIdAsync(string id);
    Task<ApiKey> GetByHashAsync(string keyHash);
    Task<List<ApiKey>> GetByAccountIdAsync(string accountId);
    Task<ApiKey> UpdateAsync(ApiKey apiKey);
    Task DeleteAsync(string id);
} 