namespace FootballIQ.Application.Interfaces;

/// <summary>Ingests StatsBomb match data into the database, skipping matches already recorded in the ingestion log.</summary>
public interface IStatsBombIngestionService
{
    /// <summary>Ingests every not-yet-ingested match for the given competition and season. Returns the number of matches newly ingested.</summary>
    Task<int> IngestSeasonAsync(int competitionId, int seasonId, CancellationToken ct);
}
