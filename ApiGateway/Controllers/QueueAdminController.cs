using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/queue-admin")]
    [Authorize(Roles = "Admin")]
    public class QueueAdminController : ControllerBase
    {
        private readonly IMessageQueueService _queueService;
        private readonly QueueMetricsCollector _metricsCollector;
        private readonly ILogger<QueueAdminController> _logger;

        public QueueAdminController(
            IMessageQueueService queueService,
            QueueMetricsCollector metricsCollector,
            ILogger<QueueAdminController> logger)
        {
            _queueService = queueService;
            _metricsCollector = metricsCollector;
            _logger = logger;
        }

        [HttpGet("queues")]
        public async Task<ActionResult<List<QueueStatus>>> GetQueues()
        {
            try
            {
                var queues = await _queueService.GetQueuesAsync();
                return Ok(queues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving queue status");
                return StatusCode(500, "Error retrieving queue status");
            }
        }

        [HttpGet("queues/{queueName}/messages")]
        public async Task<ActionResult<List<QueueMessage>>> PeekMessages(
            string queueName, 
            [FromQuery] int count = 10)
        {
            try
            {
                var messages = await _queueService.PeekMessagesAsync(queueName, count);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error peeking messages from queue {QueueName}", queueName);
                return StatusCode(500, "Error retrieving messages");
            }
        }

        [HttpPost("queues/{queueName}/purge")]
        public async Task<IActionResult> PurgeQueue(string queueName)
        {
            try
            {
                await _queueService.PurgeQueueAsync(queueName);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging queue {QueueName}", queueName);
                return StatusCode(500, "Error purging queue");
            }
        }

        [HttpGet("queues/{queueName}/metrics")]
        public async Task<ActionResult<QueueMetrics>> GetQueueMetrics(string queueName)
        {
            try
            {
                var metrics = await _metricsCollector.GetQueueMetricsAsync(queueName);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics for queue {QueueName}", queueName);
                return StatusCode(500, "Error retrieving queue metrics");
            }
        }
    }
} 