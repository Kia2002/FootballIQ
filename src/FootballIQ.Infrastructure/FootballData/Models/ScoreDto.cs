namespace FootballIQ.Infrastructure.FootballData.Models;

/// <summary>The outcome of a match, including the winner and full-time score.</summary>
public record ScoreDto(string? Winner, ScoreDetailDto? FullTime);

/// <summary>A home/away goal count for one phase of a match (e.g. full time, half time).</summary>
public record ScoreDetailDto(int? Home, int? Away);
