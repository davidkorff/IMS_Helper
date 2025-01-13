using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ApiGateway.Services;
using System;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/monitoring")]
    public class LoadTestMonitoringController : ControllerBase
    {
        private readonly ILogger<LoadTestMonitoringController> _logger;
        private readonly IMetricsRegistry _metricsRegistry;

        public LoadTestMonitoringController(
            ILogger<LoadTestMonitoringController> logger,
            IMetricsRegistry metricsRegistry)
        {
            _logger = logger;
            _metricsRegistry = metricsRegistry;
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            var health = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                ActiveScenarios = _metricsRegistry.GetActiveScenarios(),
                SystemMetrics = new
                {
                    CpuUsage = GetCpuUsage(),
                    MemoryUsage = GetMemoryUsage(),
                    ThreadCount = GetThreadCount()
                }
            };

            return Ok(health);
        }

        [HttpGet("metrics/live")]
        public IActionResult GetLiveMetrics()
        {
            var metrics = new
            {
                RequestRate = _metricsRegistry.GetRequestRate(),
                ErrorRate = _metricsRegistry.GetErrorRate(),
                AverageResponseTime = _metricsRegistry.GetAverageResponseTime(),
                ActiveUsers = _metricsRegistry.GetActiveUsers()
            };

            return Ok(metrics);
        }
    }
} 