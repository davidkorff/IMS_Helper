using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class DeadLetterQueueManager : BackgroundService
{
    private readonly IMessageQueueService _queueService;
    private readonly ILogger<DeadLetterQueueManager> _logger;
    private readonly Dictionary<string, QueueConfiguration> _queueConfigs;

    public DeadLetterQueueManager(
        IMessageQueueService queueService,
        IConfiguration configuration,
        ILogger<DeadLetterQueueManager> logger)
    {
        _queueService = queueService;
        _logger = logger;
        _queueConfigs = configuration
            .GetSection("MessageQueue:Queues")
            .Get<Dictionary<string, QueueConfiguration>>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var (queueName, config) in _queueConfigs)
            {
                if (!config.EnableDeadLetterQueue) continue;

                try
                {
                    await ProcessDeadLetterQueueAsync(queueName, config, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error processing dead letter queue for {QueueName}", queueName);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessDeadLetterQueueAsync(
        string queueName, 
        QueueConfiguration config, 
        CancellationToken stoppingToken)
    {
        var dlqName = $"{queueName}.dlq";
        var message = await _queueService.ConsumeAsync<object>(dlqName, stoppingToken);

        while (message != null && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation(
                    "Processing dead letter message {MessageId} for queue {QueueName}", 
                    message.MessageId, queueName);

                // Analyze message headers for retry information
                var retryCount = message.Headers.TryGetValue("x-retry-count", out var retryStr) 
                    ? int.Parse(retryStr) 
                    : 0;

                if (retryCount < config.MaxRetries)
                {
                    message.Headers["x-retry-count"] = (retryCount + 1).ToString();
                    await _queueService.PublishAsync(queueName, message.Payload, message.Headers);
                    await _queueService.AcknowledgeAsync(dlqName, message.MessageId);
                }
                else
                {
                    _logger.LogWarning(
                        "Message {MessageId} exceeded retry limit for queue {QueueName}", 
                        message.MessageId, queueName);
                    await _queueService.AcknowledgeAsync(dlqName, message.MessageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing dead letter message {MessageId}", message.MessageId);
                await _queueService.RejectAsync(dlqName, message.MessageId, true);
            }

            message = await _queueService.ConsumeAsync<object>(dlqName, stoppingToken);
        }
    }
} 