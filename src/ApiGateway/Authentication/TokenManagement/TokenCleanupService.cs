using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

public class TokenCleanupService : BackgroundService
{
    private readonly ITokenManagementService _tokenManagement;
    private readonly ILogger<TokenCleanupService> _logger;
    private readonly TokenManagementSettings _settings;

    public TokenCleanupService(
        ITokenManagementService tokenManagement,
        ILogger<TokenCleanupService> logger,
        IOptions<TokenManagementSettings> settings)
    {
        _tokenManagement = tokenManagement;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnableTokenCleanup)
        {
            _logger.LogInformation("Token cleanup service is disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting token cleanup cycle");
                await _tokenManagement.CleanupExpiredTokensAsync();
                _logger.LogInformation("Token cleanup cycle completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token cleanup");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.TokenCleanupIntervalMinutes), 
                stoppingToken);
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token cleanup service is starting");
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token cleanup service is stopping");
        await base.StopAsync(cancellationToken);
    }
} 