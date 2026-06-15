namespace FootballIQ.Application.DTOs;

/// <summary>A player as returned by the API — a shaped view over the Player entity.</summary>
public record PlayerDto(Guid Id, string Name, string Position, string? Nationality, DateTime? DateOfBirth, string? PreferredFoot);
