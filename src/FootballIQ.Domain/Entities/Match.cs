namespace FootballIQ.Domain.Entities;

/// <summary>A single football match between two clubs, identified by its StatsBomb match ID.</summary>
public class Match
{
    public Guid Id { get; set; }
    public int StatsBombMatchId { get; set; }

    public int CompetitionId { get; set; }
    public int SeasonId { get; set; }

    public DateTime MatchDate { get; set; }

    public Guid HomeClubId { get; set; }
    public Guid AwayClubId { get; set; }

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
