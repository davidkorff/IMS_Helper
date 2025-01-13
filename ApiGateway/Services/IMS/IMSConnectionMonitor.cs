using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ApiGateway.Services.IMS;

public class IMSConnectionMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IMSConnectionMonitor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public IMSConnectionMonitor(
        IServiceScopeFactory scopeFactory,
        ILogger<IMSConnectionMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckConnections(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking IMS connections");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task CheckConnections(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IIMSClientFactory>();

        var activeConnections = await context.IMSConnections
            .Where(c => c.IsActive)
            .ToListAsync(stoppingToken);

        foreach (var connection in activeConnections)
        {
            try
            {
                var credentials = encryptionService.DecryptCredentials(connection.EncryptedCredentials);
                var client = clientFactory.CreateClient();
                
                await client.AuthenticateAsync(credentials);
                var isValid = await client.ValidateSessionAsync();

                connection.Status = isValid ? 
                    IMSConnectionStatus.Active : 
                    IMSConnectionStatus.Failed;
                connection.LastUsedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Connection check failed for connection {ConnectionId}", 
                    connection.Id);
                connection.Status = IMSConnectionStatus.Failed;
            }
        }

        await context.SaveChangesAsync(stoppingToken);
    }
} 