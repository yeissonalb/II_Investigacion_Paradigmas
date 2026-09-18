using System.Text;
using System.Text.Json;
using EventStore.Client;
using ServiceA.Models;

namespace ServiceA.Services;

public class EventStoreService
{
    private readonly EventStoreClient _client;
    private readonly ILogger<EventStoreService> _logger;

    public EventStoreService(EventStoreClient client, ILogger<EventStoreService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Normaliza el nombre del stream respetando el contrato acordado: battle-{id}
    /// </summary>
    public static string GetStreamName(string battleId)
    {
        var cleanId = battleId.StartsWith("battle-", StringComparison.OrdinalIgnoreCase)
            ? battleId["battle-".Length..]
            : battleId;
        return $"battle-{cleanId}";
    }

    /// <summary>
    /// Lee todos los eventos del stream cronológicamente para reconstruir el estado actual en memoria (Replaying).
    /// </summary>
    public async Task<BattleAggregate> ReplayBattleAsync(string battleId, CancellationToken ct = default)
    {
        var streamName = GetStreamName(battleId);
        var aggregate = new BattleAggregate();

        try
        {
            var readResult = _client.ReadStreamAsync(
                Direction.Forwards,
                streamName,
                StreamPosition.Start,
                cancellationToken: ct
            );

            await foreach (var resolvedEvent in readResult.WithCancellation(ct))
            {
                var eventType = resolvedEvent.Event.EventType;
                var json = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);
                aggregate.Apply(eventType, json);
            }
        }
        catch (StreamNotFoundException)
        {
            // El stream aún no ha sido creado en EventStoreDB
        }
        catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            // Manejo de compatibilidad en grpc
        }

        return aggregate;
    }

    /// <summary>
    /// Guarda un nuevo evento inmutable al final del stream en EventStoreDB.
    /// </summary>
    public async Task AppendEventAsync(string battleId, string eventType, object eventData, CancellationToken ct = default)
    {
        var streamName = GetStreamName(battleId);
        var json = JsonSerializer.Serialize(eventData);
        var eventBytes = Encoding.UTF8.GetBytes(json);

        var eventPayload = new EventData(
            Uuid.NewUuid(),
            eventType,
            eventBytes
        );

        await _client.AppendToStreamAsync(
            streamName,
            StreamState.Any,
            new[] { eventPayload },
            cancellationToken: ct
        );
    }
}
