public interface IEventAlertService
{
    Task<string> CreateAlertRuleAsync(EventAlertRule rule);
    Task UpdateAlertRuleAsync(string ruleId, EventAlertRule rule);
    Task DeleteAlertRuleAsync(string ruleId);
    Task<List<EventAlert>> GetActiveAlertsAsync();
    Task AcknowledgeAlertAsync(string alertId, string userId);
}

public class EventAlertService : IEventAlertService
{
    private readonly IEventStore _eventStore;
    private readonly INotificationService _notificationService;
    private readonly ILogger<EventAlertService> _logger;
    private readonly ConcurrentDictionary<string, EventAlertRule> _activeRules;
    private readonly ConcurrentDictionary<string, EventAlert> _activeAlerts;

    public EventAlertService(
        IEventStore eventStore,
        INotificationService notificationService,
        ILogger<EventAlertService> logger)
    {
        _eventStore = eventStore;
        _notificationService = notificationService;
        _logger = logger;
        _activeRules = new ConcurrentDictionary<string, EventAlertRule>();
        _activeAlerts = new ConcurrentDictionary<string, EventAlert>();
    }

    public async Task<string> CreateAlertRuleAsync(EventAlertRule rule)
    {
        rule.Id = Guid.NewGuid().ToString();
        _activeRules[rule.Id] = rule;
        await SaveRuleAsync(rule);
        return rule.Id;
    }

    public async Task UpdateAlertRuleAsync(string ruleId, EventAlertRule rule)
    {
        rule.Id = ruleId;
        _activeRules[ruleId] = rule;
        await SaveRuleAsync(rule);
    }

    public async Task DeleteAlertRuleAsync(string ruleId)
    {
        _activeRules.TryRemove(ruleId, out _);
        await DeleteRuleAsync(ruleId);
    }

    public async Task<List<EventAlert>> GetActiveAlertsAsync()
    {
        return _activeAlerts.Values.ToList();
    }

    public async Task AcknowledgeAlertAsync(string alertId, string userId)
    {
        if (_activeAlerts.TryGetValue(alertId, out var alert))
        {
            alert.AcknowledgedBy = userId;
            alert.AcknowledgedAt = DateTime.UtcNow;
            _activeAlerts.TryRemove(alertId, out _);
            await SaveAlertAsync(alert);
        }
    }

    public async Task EvaluateRulesAsync(IntegrationEvent @event)
    {
        foreach (var rule in _activeRules.Values)
        {
            try
            {
                if (await ShouldTriggerAlert(@event, rule))
                {
                    var alert = new EventAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        RuleId = rule.Id,
                        EventId = @event.Id,
                        Severity = rule.Severity,
                        Message = FormatAlertMessage(rule, @event),
                        CreatedAt = DateTime.UtcNow
                    };

                    _activeAlerts[alert.Id] = alert;
                    await SaveAlertAsync(alert);
                    await NotifyAlert(alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error evaluating rule {RuleId} for event {EventId}", 
                    rule.Id, @event.Id);
            }
        }
    }

    private async Task<bool> ShouldTriggerAlert(
        IntegrationEvent @event, 
        EventAlertRule rule)
    {
        if (rule.EventType != null && rule.EventType != @event.Type)
            return false;

        if (rule.Source != null && rule.Source != @event.Source)
            return false;

        if (rule.Condition != null)
        {
            var result = await EvaluateConditionAsync(rule.Condition, @event);
            if (!result) return false;
        }

        return true;
    }

    private async Task<bool> EvaluateConditionAsync(
        AlertCondition condition, 
        IntegrationEvent @event)
    {
        switch (condition.Type)
        {
            case AlertConditionType.ErrorRate:
                var errorRate = await CalculateErrorRateAsync(
                    @event.Type, 
                    TimeSpan.FromMinutes(condition.TimeWindowMinutes));
                return errorRate >= condition.Threshold;

            case AlertConditionType.ProcessingTime:
                if (@event.Metadata.TryGetValue("processing_time", out var timeStr) &&
                    double.TryParse(timeStr, out var processingTime))
                {
                    return processingTime >= condition.Threshold;
                }
                return false;

            case AlertConditionType.EventVolume:
                var volume = await CalculateEventVolumeAsync(
                    @event.Type, 
                    TimeSpan.FromMinutes(condition.TimeWindowMinutes));
                return volume >= condition.Threshold;

            default:
                return false;
        }
    }

    private string FormatAlertMessage(EventAlertRule rule, IntegrationEvent @event)
    {
        return rule.MessageTemplate
            .Replace("{EventType}", @event.Type)
            .Replace("{Source}", @event.Source)
            .Replace("{Timestamp}", @event.Timestamp.ToString("O"));
    }

    private async Task NotifyAlert(EventAlert alert)
    {
        var rule = _activeRules[alert.RuleId];
        foreach (var channel in rule.NotificationChannels)
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    channel,
                    alert.Message,
                    alert.Severity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error sending alert notification through channel {Channel}", 
                    channel);
            }
        }
    }

    // Database operations
    private Task SaveRuleAsync(EventAlertRule rule) => 
        Task.CompletedTask; // Implement actual storage

    private Task DeleteRuleAsync(string ruleId) => 
        Task.CompletedTask; // Implement actual storage

    private Task SaveAlertAsync(EventAlert alert) => 
        Task.CompletedTask; // Implement actual storage
} 