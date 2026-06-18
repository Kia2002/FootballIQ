namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Per-match aggregate stats for one player, computed from raw StatsBomb events.</summary>
public record PlayerAggregateStats
{
    public int PlayerId { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public double PassCompletionPct { get; init; }

    public double TotalXg { get; init; }

    public double TotalXa { get; init; }

    public double PressuresPer90 { get; init; }
}
