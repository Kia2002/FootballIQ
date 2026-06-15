using FootballIQ.Domain.Enums;

namespace FootballIQ.Domain.Entities;

/// <summary>A football player, identified by their StatsBomb player ID.</summary>
public class Player
{
    public Guid Id { get; set; }
    public int StatsBombPlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Position Position { get; set; }

    // StatsBomb's open data does not include date of birth or preferred foot.
    // These stay null until a future data source is added to enrich player profiles.
    public DateTime? DateOfBirth { get; set; }
    public Foot? PreferredFoot { get; set; }

    public string? Nationality { get; set; }

    public ICollection<PlayerSeasonStats> SeasonStats { get; set; } = new List<PlayerSeasonStats>();
}
