namespace FootballIQ.Domain.Entities;

/// <summary>A player's aggregated statistics for one club, competition, and season.</summary>
public class PlayerSeasonStats
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }
    public Guid ClubId { get; set; }

    public int CompetitionId { get; set; }
    public int SeasonId { get; set; }

    public int MatchesPlayed { get; set; }
    public int MinutesPlayed { get; set; }

    public int PassesCompleted { get; set; }
    public int PassesAttempted { get; set; }

    public int Goals { get; set; }
    public int Assists { get; set; }

    public double ExpectedGoals { get; set; }
    public double ExpectedAssists { get; set; }

    public int Pressures { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    // Populated by EF Core when loaded with Include(); not set directly by application code.
    public Player Player { get; set; } = null!;
    public Club Club { get; set; } = null!;
}
