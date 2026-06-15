namespace FootballIQ.Infrastructure.FootballData.Models;

/// <summary>A team as represented in football-data.org responses.</summary>
public record TeamDto(int Id, string Name, string? ShortName, string? Tla);
