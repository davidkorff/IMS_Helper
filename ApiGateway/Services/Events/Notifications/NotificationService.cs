public interface INotificationService
{
    Task SendNotificationAsync(NotificationChannel channel, string message, AlertSeverity severity);
    Task SendEscalatedNotificationAsync(EventAlert alert, EscalationLevel level);
    Task<List<NotificationStatus>> GetNotificationStatusAsync(string alertId);
}

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IEmailService _emailService;
    private readonly ISlackService _slackService;
    private readonly IPagerDutyService _pagerDutyService;
    private readonly ConcurrentDictionary<string, List<NotificationStatus>> _notificationHistory;

    public NotificationService(
        ILogger<NotificationService> logger,
        IConfiguration configuration,
        HttpClient httpClient,
        IEmailService emailService,
        ISlackService slackService,
        IPagerDutyService pagerDutyService)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
        _emailService = emailService;
        _slackService = slackService;
        _pagerDutyService = pagerDutyService;
        _notificationHistory = new ConcurrentDictionary<string, List<NotificationStatus>>();
    }

    public async Task SendNotificationAsync(
        NotificationChannel channel, 
        string message, 
        AlertSeverity severity)
    {
        try
        {
            switch (channel.Type)
            {
                case NotificationChannelType.Email:
                    await SendEmailNotificationAsync(channel, message, severity);
                    break;
                case NotificationChannelType.Slack:
                    await SendSlackNotificationAsync(channel, message, severity);
                    break;
                case NotificationChannelType.PagerDuty:
                    await SendPagerDutyNotificationAsync(channel, message, severity);
                    break;
                case NotificationChannelType.Webhook:
                    await SendWebhookNotificationAsync(channel, message, severity);
                    break;
                default:
                    throw new NotSupportedException($"Channel type {channel.Type} not supported");
            }

            RecordNotificationStatus(channel, message, severity, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending notification through channel {ChannelType}", 
                channel.Type);
            RecordNotificationStatus(channel, message, severity, false, ex.Message);
            throw;
        }
    }

    public async Task SendEscalatedNotificationAsync(
        EventAlert alert, 
        EscalationLevel level)
    {
        var escalationConfig = GetEscalationConfig(level);
        var escalatedMessage = FormatEscalatedMessage(alert, level);

        foreach (var channel in escalationConfig.Channels)
        {
            try
            {
                await SendNotificationAsync(
                    channel, 
                    escalatedMessage, 
                    AlertSeverity.Critical);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error sending escalated notification for alert {AlertId}", 
                    alert.Id);
            }
        }
    }

    public Task<List<NotificationStatus>> GetNotificationStatusAsync(string alertId)
    {
        return Task.FromResult(
            _notificationHistory.GetValueOrDefault(alertId, new List<NotificationStatus>()));
    }

    private async Task SendEmailNotificationAsync(
        NotificationChannel channel, 
        string message, 
        AlertSeverity severity)
    {
        var emailMessage = new EmailMessage
        {
            To = channel.Configuration["recipients"],
            Subject = $"Alert: {severity} - {message.Split('\n')[0]}",
            Body = FormatEmailBody(message, severity),
            Priority = MapSeverityToEmailPriority(severity)
        };

        await _emailService.SendAsync(emailMessage);
    }

    private async Task SendSlackNotificationAsync(
        NotificationChannel channel, 
        string message, 
        AlertSeverity severity)
    {
        var slackMessage = new SlackMessage
        {
            Channel = channel.Configuration["channel"],
            Text = FormatSlackMessage(message, severity),
            Attachments = new[]
            {
                new SlackAttachment
                {
                    Color = MapSeverityToColor(severity),
                    Text = message,
                    Footer = $"Alert Severity: {severity}"
                }
            }
        };

        await _slackService.SendMessageAsync(slackMessage);
    }

    private async Task SendPagerDutyNotificationAsync(
        NotificationChannel channel, 
        string message, 
        AlertSeverity severity)
    {
        var incident = new PagerDutyIncident
        {
            ServiceId = channel.Configuration["serviceId"],
            Title = message.Split('\n')[0],
            Description = message,
            Priority = MapSeverityToPagerDutyPriority(severity)
        };

        await _pagerDutyService.CreateIncidentAsync(incident);
    }

    private async Task SendWebhookNotificationAsync(
        NotificationChannel channel, 
        string message, 
        AlertSeverity severity)
    {
        var payload = new
        {
            message,
            severity,
            timestamp = DateTime.UtcNow,
            metadata = channel.Configuration
        };

        var response = await _httpClient.PostAsJsonAsync(
            channel.Configuration["url"], 
            payload);
        
        response.EnsureSuccessStatusCode();
    }

    private void RecordNotificationStatus(
        NotificationChannel channel,
        string message,
        AlertSeverity severity,
        bool success,
        string error = null)
    {
        var status = new NotificationStatus
        {
            ChannelType = channel.Type,
            Timestamp = DateTime.UtcNow,
            Message = message,
            Severity = severity,
            Success = success,
            Error = error
        };

        _notificationHistory.AddOrUpdate(
            channel.Id,
            _ => new List<NotificationStatus> { status },
            (_, list) =>
            {
                list.Add(status);
                return list;
            });
    }

    private EscalationConfig GetEscalationConfig(EscalationLevel level)
    {
        return _configuration
            .GetSection($"AlertEscalation:Levels:{level}")
            .Get<EscalationConfig>();
    }

    private string FormatEscalatedMessage(EventAlert alert, EscalationLevel level)
    {
        return $"""
            ESCALATED ALERT - Level {level}
            
            Original Alert: {alert.Message}
            Created: {alert.CreatedAt:O}
            Severity: {alert.Severity}
            Time Since Creation: {DateTime.UtcNow - alert.CreatedAt}
            
            This alert has been escalated due to lack of acknowledgment.
            Please take immediate action.
            """;
    }

    private static string MapSeverityToColor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "#FF0000",
        AlertSeverity.High => "#FFA500",
        AlertSeverity.Medium => "#FFFF00",
        AlertSeverity.Low => "#00FF00",
        _ => "#808080"
    };
} 