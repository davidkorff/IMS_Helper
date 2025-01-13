using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MessageRetryPolicy
{
    private readonly ILogger<MessageRetryPolicy> _logger;
    private readonly Dictionary<string, QueueConfiguration> _queueConfigs;

    public MessageRetryPolicy(
        IConfiguration configuration,
        ILogger<MessageRetryPolicy> logger)
    {
        _logger = logger;
        _queueConfigs = configuration
            .GetSection("MessageQueue:Queues")
            .Get<Dictionary<string, QueueConfiguration>>();
    }

    public async Task<bool> ShouldRetryAsync(
        string queueName, 
        MessageEnvelope<object> message, 
        Exception exception)
    {
        if (!_queueConfigs.TryGetValue(queueName, out var config))
            return false;

        var retryCount = message.Headers.TryGetValue("x-retry-count", out var retryStr) 
            ? int.Parse(retryStr) 
            : 0;

        if (retryCount >= config.MaxRetries)
        {
            _logger.LogWarning(
                "Message {MessageId} exceeded retry limit for queue {QueueName}", 
                message.MessageId, queueName);
            return false;
        }

        var delay = CalculateRetryDelay(retryCount, config.RetryDelay);
        await Task.Delay(delay);

        _logger.LogInformation(
            "Retrying message {MessageId} for queue {QueueName} (attempt {RetryCount})", 
            message.MessageId, queueName, retryCount + 1);

        return true;
    }

    private TimeSpan CalculateRetryDelay(int retryCount, TimeSpan baseDelay)
    {
        // Exponential backoff with jitter
        var exponentialDelay = baseDelay * Math.Pow(2, retryCount);
        var jitter = Random.Shared.NextDouble() * 0.3 + 0.85; // 85-115% of base delay
        return TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds * jitter);
    }
} 