using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class QueueOptimizer : IHostedService
{
    private readonly ILogger<QueueOptimizer> _logger;
    private readonly NotificationPriorityQueue _queue;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters;
    private readonly ConcurrentDictionary<string, AdaptiveThrottling> _throttling;
    private Timer _optimizationTimer;

    public QueueOptimizer(
        ILogger<QueueOptimizer> logger,
        NotificationPriorityQueue queue,
        IConfiguration configuration)
    {
        _logger = logger;
        _queue = queue;
        _configuration = configuration;
        _rateLimiters = new ConcurrentDictionary<string, RateLimiter>();
        _throttling = new ConcurrentDictionary<string, AdaptiveThrottling>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _optimizationTimer = new Timer(
            OptimizeQueue, 
            null, 
            TimeSpan.Zero, 
            TimeSpan.FromSeconds(30));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _optimizationTimer?.Dispose();
        return Task.CompletedTask;
    }

    public bool ShouldThrottle(string channel)
    {
        var throttling = _throttling.GetOrAdd(channel, 
            _ => new AdaptiveThrottling());

        var rateLimiter = _rateLimiters.GetOrAdd(channel, 
            CreateRateLimiter);

        return !rateLimiter.TryAcquire() || throttling.ShouldThrottle();
    }

    private void OptimizeQueue(object state)
    {
        try
        {
            foreach (var channel in _throttling.Keys)
            {
                var metrics = _queue.GetChannelMetrics(channel);
                var throttling = _throttling[channel];
                
                // Update throttling based on error rates and latency
                throttling.UpdateMetrics(
                    metrics.ErrorRate,
                    metrics.AverageLatency);

                // Adjust rate limits based on channel health
                AdjustRateLimits(channel, metrics);
            }

            // Log optimization results
            LogOptimizationMetrics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during queue optimization");
        }
    }

    private RateLimiter CreateRateLimiter(string channel)
    {
        var config = _configuration
            .GetSection($"Notifications:Channels:{channel}:RateLimit")
            .Get<RateLimitConfig>();

        return new RateLimiter(
            config?.RequestsPerSecond ?? 10,
            config?.BurstSize ?? 20);
    }

    private void AdjustRateLimits(string channel, ChannelMetrics metrics)
    {
        var rateLimiter = _rateLimiters[channel];
        var currentHealth = metrics.CalculateHealth();

        switch (currentHealth.Status)
        {
            case HealthStatus.Critical:
                rateLimiter.SetRate(rateLimiter.CurrentRate * 0.5);
                break;
            case HealthStatus.Unhealthy:
                rateLimiter.SetRate(rateLimiter.CurrentRate * 0.8);
                break;
            case HealthStatus.Healthy:
                rateLimiter.SetRate(Math.Min(
                    rateLimiter.CurrentRate * 1.2,
                    rateLimiter.MaxRate));
                break;
        }
    }

    private void LogOptimizationMetrics()
    {
        foreach (var (channel, rateLimiter) in _rateLimiters)
        {
            _logger.LogInformation(
                "Channel {Channel} rate limit: {Rate}/s (burst: {Burst})",
                channel,
                rateLimiter.CurrentRate,
                rateLimiter.BurstSize);
        }
    }
}

public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly double _maxRate;
    private double _currentRate;
    private readonly int _burstSize;
    private DateTime _lastRefill = DateTime.UtcNow;
    private double _tokens;
    private readonly object _lock = new();

    public RateLimiter(double requestsPerSecond, int burstSize)
    {
        _maxRate = requestsPerSecond;
        _currentRate = requestsPerSecond;
        _burstSize = burstSize;
        _tokens = burstSize;
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public double CurrentRate => _currentRate;
    public double MaxRate => _maxRate;
    public int BurstSize => _burstSize;

    public bool TryAcquire()
    {
        lock (_lock)
        {
            RefillTokens();
            if (_tokens >= 1)
            {
                _tokens -= 1;
                return true;
            }
            return false;
        }
    }

    public void SetRate(double newRate)
    {
        lock (_lock)
        {
            _currentRate = Math.Max(1, Math.Min(newRate, _maxRate));
        }
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        _lastRefill = now;

        _tokens = Math.Min(
            _burstSize,
            _tokens + elapsed * _currentRate);
    }
}

public class AdaptiveThrottling
{
    private readonly Queue<ErrorSample> _errorSamples = new();
    private readonly Queue<LatencySample> _latencySamples = new();
    private readonly object _lock = new();
    private const int MaxSamples = 100;
    private const double ErrorThreshold = 0.2;
    private const double LatencyThreshold = 1000; // ms

    public bool ShouldThrottle()
    {
        lock (_lock)
        {
            CleanupOldSamples();

            if (!_errorSamples.Any() || !_latencySamples.Any())
                return false;

            var errorRate = _errorSamples.Average(s => s.ErrorRate);
            var avgLatency = _latencySamples.Average(
                s => s.Latency.TotalMilliseconds);

            return errorRate > ErrorThreshold || 
                   avgLatency > LatencyThreshold;
        }
    }

    public void UpdateMetrics(double errorRate, TimeSpan latency)
    {
        lock (_lock)
        {
            _errorSamples.Enqueue(new ErrorSample
            {
                Timestamp = DateTime.UtcNow,
                ErrorRate = errorRate
            });

            _latencySamples.Enqueue(new LatencySample
            {
                Timestamp = DateTime.UtcNow,
                Latency = latency
            });

            CleanupOldSamples();
        }
    }

    private void CleanupOldSamples()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(5);

        while (_errorSamples.Count > MaxSamples || 
               (_errorSamples.Any() && _errorSamples.Peek().Timestamp < cutoff))
            _errorSamples.Dequeue();

        while (_latencySamples.Count > MaxSamples || 
               (_latencySamples.Any() && _latencySamples.Peek().Timestamp < cutoff))
            _latencySamples.Dequeue();
    }
}

public class ErrorSample
{
    public DateTime Timestamp { get; set; }
    public double ErrorRate { get; set; }
}

public class LatencySample
{
    public DateTime Timestamp { get; set; }
    public TimeSpan Latency { get; set; }
} 