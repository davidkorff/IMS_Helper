using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyRepository> _logger;
    private readonly string _connectionString;

    public ApiKeyRepository(
        IConfiguration configuration,
        ILogger<ApiKeyRepository> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = _configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO ApiKeys (
                Id, AccountId, KeyHash, Name, Environment, 
                RateLimit, CreatedAt, ExpiresAt, Status
            )
            VALUES (
                @Id, @AccountId, @KeyHash, @Name, @Environment, 
                @RateLimit, @CreatedAt, @ExpiresAt, @Status
            );

            INSERT INTO ApiKeyPermissions (ApiKeyId, Permission)
            SELECT @Id, Permission
            FROM @Permissions;";

        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(sql, new
            {
                apiKey.Id,
                apiKey.AccountId,
                apiKey.KeyHash,
                apiKey.Name,
                apiKey.Environment,
                apiKey.RateLimit,
                apiKey.CreatedAt,
                apiKey.ExpiresAt,
                Status = (int)apiKey.Status,
                Permissions = apiKey.Permissions.Select(p => new { Permission = p }).ToDataTable()
            }, transaction);

            transaction.Commit();
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key");
            transaction.Rollback();
            throw;
        }
    }

    public async Task<ApiKey> GetByIdAsync(string id)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                k.*, p.Permission
            FROM ApiKeys k
            LEFT JOIN ApiKeyPermissions p ON k.Id = p.ApiKeyId
            WHERE k.Id = @Id;";

        var keyDictionary = new Dictionary<string, ApiKey>();
        
        await connection.QueryAsync<ApiKey, string, ApiKey>(
            sql,
            (key, permission) =>
            {
                if (!keyDictionary.TryGetValue(key.Id, out var apiKey))
                {
                    apiKey = key;
                    apiKey.Permissions = new List<string>();
                    keyDictionary.Add(key.Id, apiKey);
                }

                if (!string.IsNullOrEmpty(permission))
                {
                    apiKey.Permissions.Add(permission);
                }

                return apiKey;
            },
            new { Id = id },
            splitOn: "Permission"
        );

        return keyDictionary.Values.FirstOrDefault();
    }

    public async Task<ApiKey> GetByHashAsync(string keyHash)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                k.*, p.Permission
            FROM ApiKeys k
            LEFT JOIN ApiKeyPermissions p ON k.Id = p.ApiKeyId
            WHERE k.KeyHash = @KeyHash;";

        var keyDictionary = new Dictionary<string, ApiKey>();
        
        await connection.QueryAsync<ApiKey, string, ApiKey>(
            sql,
            (key, permission) =>
            {
                if (!keyDictionary.TryGetValue(key.Id, out var apiKey))
                {
                    apiKey = key;
                    apiKey.Permissions = new List<string>();
                    keyDictionary.Add(key.Id, apiKey);
                }

                if (!string.IsNullOrEmpty(permission))
                {
                    apiKey.Permissions.Add(permission);
                }

                return apiKey;
            },
            new { KeyHash = keyHash },
            splitOn: "Permission"
        );

        return keyDictionary.Values.FirstOrDefault();
    }

    public async Task<List<ApiKey>> GetByAccountIdAsync(string accountId)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                k.*, p.Permission
            FROM ApiKeys k
            LEFT JOIN ApiKeyPermissions p ON k.Id = p.ApiKeyId
            WHERE k.AccountId = @AccountId
            ORDER BY k.CreatedAt DESC;";

        var keyDictionary = new Dictionary<string, ApiKey>();
        
        await connection.QueryAsync<ApiKey, string, ApiKey>(
            sql,
            (key, permission) =>
            {
                if (!keyDictionary.TryGetValue(key.Id, out var apiKey))
                {
                    apiKey = key;
                    apiKey.Permissions = new List<string>();
                    keyDictionary.Add(key.Id, apiKey);
                }

                if (!string.IsNullOrEmpty(permission))
                {
                    apiKey.Permissions.Add(permission);
                }

                return apiKey;
            },
            new { AccountId = accountId },
            splitOn: "Permission"
        );

        return keyDictionary.Values.ToList();
    }

    public async Task<ApiKey> UpdateAsync(ApiKey apiKey)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            UPDATE ApiKeys 
            SET 
                Name = @Name,
                Environment = @Environment,
                RateLimit = @RateLimit,
                ExpiresAt = @ExpiresAt,
                Status = @Status,
                RevokedAt = @RevokedAt
            WHERE Id = @Id;

            DELETE FROM ApiKeyPermissions WHERE ApiKeyId = @Id;

            INSERT INTO ApiKeyPermissions (ApiKeyId, Permission)
            SELECT @Id, Permission
            FROM @Permissions;";

        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(sql, new
            {
                apiKey.Id,
                apiKey.Name,
                apiKey.Environment,
                apiKey.RateLimit,
                apiKey.ExpiresAt,
                Status = (int)apiKey.Status,
                apiKey.RevokedAt,
                Permissions = apiKey.Permissions.Select(p => new { Permission = p }).ToDataTable()
            }, transaction);

            transaction.Commit();
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API key");
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteAsync(string id)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            DELETE FROM ApiKeyPermissions WHERE ApiKeyId = @Id;
            DELETE FROM ApiKeys WHERE Id = @Id;";

        await connection.ExecuteAsync(sql, new { Id = id });
    }
} 