namespace FootballIQ.Application.Interfaces;

/// <summary>Queues a StatsBomb season for background ingestion.</summary>
public interface IIngestionQueue
{
    /// <summary>Enqueues a competition/season pair to be ingested by the background worker.</summary>
    ValueTask EnqueueAsync(int competitionId, int seasonId, CancellationToken ct);
}
