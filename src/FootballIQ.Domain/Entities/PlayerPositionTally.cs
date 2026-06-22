using FootballIQ.Domain.Enums;

namespace FootballIQ.Domain.Entities;

/// <summary>How many times a player has been listed at a given position across all ingested lineups. Used to derive Player.Position as whichever bucket has the highest count.</summary>
public class PlayerPositionTally
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }
    public Position Position { get; set; }

    public int Count { get; set; }

    public Player Player { get; set; } = null!;
}
