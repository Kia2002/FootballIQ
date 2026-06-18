using System.Text.Json.Serialization;

namespace FootballIQ.Infrastructure.StatsBomb.Models;

/// <summary>One team's entry from a StatsBomb lineups/{match_id}.json file.</summary>
public record StatsBombLineupTeam
{
    [JsonPropertyName("team_id")]
    public int TeamId { get; init; }

    [JsonPropertyName("team_name")]
    public string TeamName { get; init; } = string.Empty;

    [JsonPropertyName("lineup")]
    public List<StatsBombLineupPlayer> Lineup { get; init; } = new();
}

public record StatsBombLineupPlayer
{
    [JsonPropertyName("player_id")]
    public int PlayerId { get; init; }

    [JsonPropertyName("player_name")]
    public string PlayerName { get; init; } = string.Empty;

    [JsonPropertyName("player_nickname")]
    public string? PlayerNickname { get; init; }

    [JsonPropertyName("jersey_number")]
    public int JerseyNumber { get; init; }

    [JsonPropertyName("positions")]
    public List<StatsBombPlayerPosition> Positions { get; init; } = new();
}

public record StatsBombPlayerPosition
{
    [JsonPropertyName("position_id")]
    public int PositionId { get; init; }

    [JsonPropertyName("position")]
    public string Position { get; init; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("to")]
    public string? To { get; init; }

    [JsonPropertyName("from_period")]
    public int FromPeriod { get; init; }

    [JsonPropertyName("to_period")]
    public int? ToPeriod { get; init; }
}
