namespace FootballIQ.Domain.Entities;

/// <summary>A football club, identified by its StatsBomb team ID.</summary>
public class Club
{
    public Guid Id { get; set; }
    public int StatsBombTeamId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<PlayerSeasonStats> SeasonStats { get; set; } = new List<PlayerSeasonStats>();
}
