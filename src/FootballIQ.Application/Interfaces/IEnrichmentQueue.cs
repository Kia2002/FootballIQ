namespace FootballIQ.Application.Interfaces;

/// <summary>Queues a request to run player demographic enrichment in the background.</summary>
public interface IEnrichmentQueue
{
    /// <summary>Enqueues an enrichment run to be processed by the background worker.</summary>
    ValueTask EnqueueAsync(CancellationToken ct);
}
