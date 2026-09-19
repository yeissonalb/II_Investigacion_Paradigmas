using System.Text;
using EventStore.Client;

namespace ServiceB.Services;

public class BattleProjectionService : BackgroundService
{
    private readonly EventStoreClient _client;
    private readonly BattleReadStore _store;
    private readonly ILogger<BattleProjectionService> _logger;

    public BattleProjectionService(
        EventStoreClient client,
        BattleReadStore store,
        ILogger<BattleProjectionService> logger)
    {
        _client = client;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dropped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                using var subscription = await _client.SubscribeToAllAsync(
                    FromAll.Start,
                    eventAppeared: (_, resolvedEvent, _) =>
                    {
                        Project(resolvedEvent);
                        return Task.CompletedTask;
                    },
                    subscriptionDropped: (_, reason, exception) =>
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            dropped.TrySetResult();
                            return;
                        }

                        _logger.LogWarning(exception, "[Service-B] Suscripción caída ({Reason}). Reconectando...", reason);
                        dropped.TrySetResult();
                    },
                    cancellationToken: stoppingToken);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Service-B] Proyección suscrita a $all (streams battle-*)");
                Console.ResetColor();

                await dropped.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Service-B] EventStoreDB no disponible. Reintento en 3s...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private void Project(ResolvedEvent resolvedEvent)
    {
        var streamId = resolvedEvent.Event.EventStreamId;
        if (!streamId.StartsWith("battle-", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var eventType = resolvedEvent.Event.EventType;
        if (string.IsNullOrWhiteSpace(eventType) || eventType.StartsWith('$'))
        {
            return;
        }

        var json = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);
        var eventNumber = resolvedEvent.Event.EventNumber.ToInt64();
        var createdUtc = resolvedEvent.Event.Created.ToUniversalTime();

        _store.Apply(streamId, eventType, json, eventNumber, createdUtc);

        Console.ForegroundColor = eventType switch
        {
            "BattleStarted" => ConsoleColor.Green,
            "AttackPerformed" => ConsoleColor.Cyan,
            "HealUsed" => ConsoleColor.Magenta,
            _ => ConsoleColor.Gray
        };
        Console.WriteLine($"[Service-B] Projected {eventType} {streamId} #{eventNumber}");
        Console.ResetColor();
    }
}
