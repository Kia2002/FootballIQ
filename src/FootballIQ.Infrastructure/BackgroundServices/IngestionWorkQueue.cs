using System.Threading.Channels;
using FootballIQ.Application.Interfaces;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>In-memory FIFO queue of ingestion work items, backed by a Channel. One instance is shared (singleton) between the producer (admin endpoint) and the consumer (DataIngestionBackgroundService).</summary>
public class IngestionWorkQueue : IIngestionQueue
{
    private readonly Channel<(int CompetitionId, int SeasonId)> _channel =
        Channel.CreateUnbounded<(int CompetitionId, int SeasonId)>();

    public ValueTask EnqueueAsync(int competitionId, int seasonId, CancellationToken ct) =>
        _channel.Writer.WriteAsync((competitionId, seasonId), ct);

    public ValueTask<(int CompetitionId, int SeasonId)> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
