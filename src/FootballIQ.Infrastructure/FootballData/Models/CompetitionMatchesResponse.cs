namespace FootballIQ.Infrastructure.FootballData.Models;

/// <summary>The top-level response from football-data.org's competition matches endpoint.</summary>
public record CompetitionMatchesResponse(CompetitionDto Competition, IReadOnlyList<MatchDto> Matches);
