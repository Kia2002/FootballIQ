namespace FootballIQ.Domain.Entities;

/// <summary>Records that a StatsBomb match has been fully ingested, so re-running ingestion skips it.</summary>
public class IngestionLog
{
    public Guid Id { get; set; }
    public int StatsBombMatchId { get; set; }
    public DateTime IngestedAt { get; set; }
}
