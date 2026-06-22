using System.Threading.Channels;
using FootballIQ.Application.Interfaces;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>In-memory FIFO queue of enrichment work items, backed by a Channel. One instance is shared (singleton) between the producer (admin endpoint) and the consumer (PlayerDemographicsBackgroundService).</summary>
public class EnrichmentWorkQueue : IEnrichmentQueue
{
    private readonly Channel<bool> _channel = Channel.CreateUnbounded<bool>();

    public ValueTask EnqueueAsync(CancellationToken ct) =>
        _channel.Writer.WriteAsync(true, ct);

    public ValueTask<bool> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
