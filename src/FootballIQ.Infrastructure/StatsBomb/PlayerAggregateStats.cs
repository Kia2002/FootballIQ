namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Per-match aggregate stats for one player, computed from raw StatsBomb events.</summary>
public record PlayerAggregateStats
{
    public int PlayerId { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public int PassesCompleted { get; init; }

    public int PassesAttempted { get; init; }

    public int Goals { get; init; }

    public int Assists { get; init; }

    public double TotalXg { get; init; }

    public double TotalXa { get; init; }

    public int Pressures { get; init; }

    public double MinutesPlayed { get; init; }

    public double PassCompletionPct =>
        PassesAttempted == 0 ? 0 : (double)PassesCompleted / PassesAttempted;

    public double PressuresPer90 =>
        MinutesPlayed == 0 ? 0 : Pressures / (MinutesPlayed / 90.0);
}
