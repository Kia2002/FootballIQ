using FootballIQ.Application.DTOs;
using FootballIQ.Application.Interfaces;

namespace FootballIQ.WebAPI.Endpoints;

/// <summary>Routes for querying players.</summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/players", async (IPlayerRepository repository, CancellationToken ct) =>
        {
            var players = await repository.GetAllAsync(ct);

            var dtos = players.Select(p => new PlayerDto(
                p.Id, p.Name, p.Position.ToString(), p.Nationality, p.DateOfBirth, p.PreferredFoot?.ToString()));

            return Results.Ok(dtos);
        });
    }
}
