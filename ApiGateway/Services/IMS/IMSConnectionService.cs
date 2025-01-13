using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ApiGateway.Data;
using ApiGateway.Services.Encryption;
using ApiGateway.Services.IMS;

public class IMSConnectionService : IIMSConnectionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly IIMSClient _imsClient;
    private readonly ILogger<IMSConnectionService> _logger;

    public IMSConnectionService(
        ApplicationDbContext context,
        ICredentialEncryptionService encryptionService,
        IIMSClient imsClient,
        ILogger<IMSConnectionService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _imsClient = imsClient;
        _logger = logger;
    }

    public async Task<IMSConnectionResponse> CreateConnectionAsync(
        string userId, 
        CreateIMSConnectionRequest request)
    {
        // Test connection before saving
        var testResult = await TestConnectionWithCredentialsAsync(request.Credentials);
        if (!testResult.Success)
        {
            throw new ValidationException(testResult.Message);
        }

        var connection = new IMSConnection
        {
            UserId = userId,
            ConnectionName = request.ConnectionName,
            Environment = request.Environment,
            EncryptedCredentials = _encryptionService.EncryptCredentials(request.Credentials),
            Status = testResult.Status,
            IsActive = true
        };

        // Save permissions
        if (testResult.Permissions != null)
        {
            connection.Permissions = testResult.Permissions
                .Select(p => new IMSConnectionPermission
                {
                    PermissionName = p.Key,
                    IsGranted = p.Value
                })
                .ToList();
        }

        _context.IMSConnections.Add(connection);
        await _context.SaveChangesAsync();

        return await GetConnectionAsync(userId, connection.Id);
    }

    public async Task<TestConnectionResponse> TestConnectionAsync(string userId, string connectionId)
    {
        var connection = await GetConnectionEntityAsync(userId, connectionId);
        var credentials = _encryptionService.DecryptCredentials(connection.EncryptedCredentials);
        
        return await TestConnectionWithCredentialsAsync(credentials);
    }

    private async Task<TestConnectionResponse> TestConnectionWithCredentialsAsync(IMSCredentials credentials)
    {
        try
        {
            // Test authentication
            await _imsClient.AuthenticateAsync(credentials);

            // Test permissions
            var permissions = await _imsClient.GetPermissionsAsync();

            return new TestConnectionResponse
            {
                Success = true,
                Status = IMSConnectionStatus.Active,
                Permissions = permissions,
                Message = "Connection successful"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return new TestConnectionResponse
            {
                Success = false,
                Status = IMSConnectionStatus.Failed,
                Message = "Connection test failed: " + ex.Message
            };
        }
    }

    // Other implementation methods...
} 