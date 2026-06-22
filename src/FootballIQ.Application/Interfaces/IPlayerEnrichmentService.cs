namespace FootballIQ.Application.Interfaces;

/// <summary>Enriches players who are missing demographic data (currently: DateOfBirth) from an external source.</summary>
public interface IPlayerEnrichmentService
{
    /// <summary>Attempts to fill in DateOfBirth for every player that doesn't have one yet. Returns the number of players updated.</summary>
    Task<int> EnrichPlayerDemographicsAsync(CancellationToken ct);
}
