using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class AlertEscalationManager : BackgroundService
{
    private readonly IEventAlertService _alertService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AlertEscalationManager> _logger;
    private readonly ConcurrentDictionary<string, EscalationState> _escalationStates;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public AlertEscalationManager(
        IEventAlertService alertService,
        INotificationService notificationService,
        ILogger<AlertEscalationManager> logger)
    {
        _alertService = alertService;
        _notificationService = notificationService;
        _logger = logger;
        _escalationStates = new ConcurrentDictionary<string, EscalationState>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForEscalationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for alert escalations");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckForEscalationsAsync()
    {
        var activeAlerts = await _alertService.GetActiveAlertsAsync();

        foreach (var alert in activeAlerts)
        {
            try
            {
                await ProcessAlertEscalationAsync(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing escalation for alert {AlertId}", 
                    alert.Id);
            }
        }
    }

    private async Task ProcessAlertEscalationAsync(EventAlert alert)
    {
        var state = _escalationStates.GetOrAdd(alert.Id, _ => new EscalationState());
        var timeSinceCreation = DateTime.UtcNow - alert.CreatedAt;

        var nextLevel = DetermineEscalationLevel(
            timeSinceCreation, 
            state.CurrentLevel,
            alert.Severity);

        if (nextLevel != state.CurrentLevel)
        {
            await EscalateAlertAsync(alert, nextLevel);
            state.CurrentLevel = nextLevel;
            state.LastEscalationTime = DateTime.UtcNow;
        }
    }

    private EscalationLevel DetermineEscalationLevel(
        TimeSpan alertAge,
        EscalationLevel currentLevel,
        AlertSeverity severity)
    {
        return (alertAge, severity, currentLevel) switch
        {
            // Critical alerts escalate quickly
            (var age, AlertSeverity.Critical, EscalationLevel.None)
                when age >= TimeSpan.FromMinutes(5)
                => EscalationLevel.TeamLead,
            
            (var age, AlertSeverity.Critical, EscalationLevel.TeamLead)
                when age >= TimeSpan.FromMinutes(15)
                => EscalationLevel.Manager,
            
            (var age, AlertSeverity.Critical, EscalationLevel.Manager)
                when age >= TimeSpan.FromMinutes(30)
                => EscalationLevel.Executive,

            // High severity alerts
            (var age, AlertSeverity.High, EscalationLevel.None)
                when age >= TimeSpan.FromMinutes(15)
                => EscalationLevel.TeamLead,
            
            (var age, AlertSeverity.High, EscalationLevel.TeamLead)
                when age >= TimeSpan.FromMinutes(45)
                => EscalationLevel.Manager,

            // Medium severity alerts
            (var age, AlertSeverity.Medium, EscalationLevel.None)
                when age >= TimeSpan.FromMinutes(30)
                => EscalationLevel.TeamLead,

            // Keep current level for all other cases
            _ => currentLevel
        };
    }

    private async Task EscalateAlertAsync(
        EventAlert alert, 
        EscalationLevel newLevel)
    {
        try
        {
            await _notificationService.SendEscalatedNotificationAsync(alert, newLevel);
            
            _logger.LogInformation(
                "Alert {AlertId} escalated to {Level}", 
                alert.Id, newLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error escalating alert {AlertId} to level {Level}", 
                alert.Id, newLevel);
            throw;
        }
    }

    private class EscalationState
    {
        public EscalationLevel CurrentLevel { get; set; }
        public DateTime? LastEscalationTime { get; set; }
    }
}

public enum EscalationLevel
{
    None,
    TeamLead,
    Manager,
    Executive
}

public class EscalationConfig
{
    public TimeSpan Threshold { get; set; }
    public List<NotificationChannel> Channels { get; set; }
    public bool RequireAcknowledgment { get; set; }
} 