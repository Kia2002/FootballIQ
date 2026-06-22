using System.Text.Json.Serialization;

namespace FootballIQ.Infrastructure.StatsBomb.Models;

/// <summary>One entry from a StatsBomb matches/{competition_id}/{season_id}.json file.</summary>
public record StatsBombMatch
{
    [JsonPropertyName("match_id")]
    public int MatchId { get; init; }

    [JsonPropertyName("match_date")]
    public string MatchDate { get; init; } = string.Empty;

    [JsonPropertyName("home_team")]
    public StatsBombHomeTeam HomeTeam { get; init; } = null!;

    [JsonPropertyName("away_team")]
    public StatsBombAwayTeam AwayTeam { get; init; } = null!;

    [JsonPropertyName("home_score")]
    public int HomeScore { get; init; }

    [JsonPropertyName("away_score")]
    public int AwayScore { get; init; }

    [JsonPropertyName("competition")]
    public StatsBombCompetition Competition { get; init; } = null!;

    [JsonPropertyName("season")]
    public StatsBombSeason Season { get; init; } = null!;
}

public record StatsBombHomeTeam
{
    [JsonPropertyName("home_team_id")]
    public int HomeTeamId { get; init; }

    [JsonPropertyName("home_team_name")]
    public string HomeTeamName { get; init; } = string.Empty;
}

public record StatsBombAwayTeam
{
    [JsonPropertyName("away_team_id")]
    public int AwayTeamId { get; init; }

    [JsonPropertyName("away_team_name")]
    public string AwayTeamName { get; init; } = string.Empty;
}

public record StatsBombCompetition
{
    [JsonPropertyName("competition_id")]
    public int CompetitionId { get; init; }

    [JsonPropertyName("competition_name")]
    public string CompetitionName { get; init; } = string.Empty;
}

public record StatsBombSeason
{
    [JsonPropertyName("season_id")]
    public int SeasonId { get; init; }

    [JsonPropertyName("season_name")]
    public string SeasonName { get; init; } = string.Empty;
}
