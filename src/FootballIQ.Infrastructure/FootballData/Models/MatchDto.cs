namespace FootballIQ.Infrastructure.FootballData.Models;

/// <summary>A single fixture/result from football-data.org.</summary>
public record MatchDto(int Id, DateTime UtcDate, string Status, int? Matchday, TeamDto HomeTeam, TeamDto AwayTeam, ScoreDto Score);
