using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public class IMSHealthCheck : IHealthCheck
{
    private readonly IIMSConnectionPool _connectionPool;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IMSHealthCheck> _logger;

    public IMSHealthCheck(
        IIMSConnectionPool connectionPool,
        ApplicationDbContext context,
        ILogger<IMSHealthCheck> logger)
    {
        _connectionPool = connectionPool;
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var activeConnections = await _context.IMSConnections
            .Where(c => c.IsActive && c.Status == IMSConnectionStatus.Active)
            .CountAsync(cancellationToken);

        data.Add("ActiveConnections", activeConnections);

        try
        {
            // Test a sample connection if available
            var testConnection = await _context.IMSConnections
                .Where(c => c.IsActive && c.Status == IMSConnectionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            if (testConnection != null)
            {
                var client = await _connectionPool.GetClientAsync(
                    testConnection.Id, testConnection);
                var isValid = await client.ValidateSessionAsync();
                data.Add("ConnectionTest", isValid ? "Success" : "Failed");

                return isValid
                    ? HealthCheckResult.Healthy("IMS connection is healthy", data)
                    : HealthCheckResult.Degraded("IMS connection validation failed", null, data);
            }

            return HealthCheckResult.Healthy("No active connections to test", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IMS health check failed");
            return HealthCheckResult.Unhealthy("IMS health check failed", ex, data);
        }
    }
} 