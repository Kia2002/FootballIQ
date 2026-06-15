namespace FootballIQ.Infrastructure.FootballData.Models;

/// <summary>The competition a set of matches belongs to, e.g. La Liga.</summary>
public record CompetitionDto(int Id, string Name, string Code);
