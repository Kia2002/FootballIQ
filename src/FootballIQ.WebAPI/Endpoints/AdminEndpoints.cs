using FootballIQ.Application.Interfaces;

namespace FootballIQ.WebAPI.Endpoints;

/// <summary>Admin routes for triggering data ingestion.</summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/ingest", async (IngestRequest request, IIngestionQueue queue, CancellationToken ct) =>
        {
            await queue.EnqueueAsync(request.CompetitionId, request.SeasonId, ct);
            return Results.Accepted();
        });
    }

    public record IngestRequest(int CompetitionId, int SeasonId);
}
