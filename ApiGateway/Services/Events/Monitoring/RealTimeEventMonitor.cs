using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public class RealTimeEventMonitor : BackgroundService
{
    private readonly IEventStore _eventStore;
    private readonly IHubContext<EventHub> _hubContext;
    private readonly ILogger<RealTimeEventMonitor> _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _clientSubscriptions;
    private readonly Channel<IntegrationEvent> _eventChannel;

    public RealTimeEventMonitor(
        IEventStore eventStore,
        IHubContext<EventHub> hubContext,
        ILogger<RealTimeEventMonitor> logger)
    {
        _eventStore = eventStore;
        _hubContext = hubContext;
        _logger = logger;
        _clientSubscriptions = new ConcurrentDictionary<string, HashSet<string>>();
        _eventChannel = Channel.CreateUnbounded<IntegrationEvent>(
            new UnboundedChannelOptions { SingleReader = true });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var @event in _eventChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await BroadcastEventAsync(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error broadcasting event {EventId}", @event.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public async Task PublishEventAsync(IntegrationEvent @event)
    {
        await _eventChannel.Writer.WriteAsync(@event);
    }

    private async Task BroadcastEventAsync(IntegrationEvent @event)
    {
        var eventData = new RealTimeEventData
        {
            Id = @event.Id,
            Type = @event.Type,
            Source = @event.Source,
            Timestamp = @event.Timestamp,
            Data = @event.Data
        };

        // Broadcast to all clients subscribed to this event type
        var subscribedClients = _clientSubscriptions
            .Where(kvp => kvp.Value.Contains(@event.Type))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var clientId in subscribedClients)
        {
            try
            {
                await _hubContext.Clients
                    .Client(clientId)
                    .SendAsync("ReceiveEvent", eventData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error sending event to client {ClientId}", clientId);
            }
        }
    }

    public void SubscribeClient(string clientId, string eventType)
    {
        _clientSubscriptions.AddOrUpdate(
            clientId,
            _ => new HashSet<string> { eventType },
            (_, types) =>
            {
                types.Add(eventType);
                return types;
            });
    }

    public void UnsubscribeClient(string clientId, string eventType = null)
    {
        if (eventType == null)
        {
            _clientSubscriptions.TryRemove(clientId, out _);
        }
        else if (_clientSubscriptions.TryGetValue(clientId, out var types))
        {
            types.Remove(eventType);
        }
    }
} 