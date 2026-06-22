using FootballIQ.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>Long-running worker that dequeues ingestion requests and runs them via IStatsBombIngestionService. Runs for the lifetime of the app (registered as a hosted service).</summary>
public class DataIngestionBackgroundService : BackgroundService
{
    private readonly IngestionWorkQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public DataIngestionBackgroundService(IngestionWorkQueue queue, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            (int CompetitionId, int SeasonId) item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using var scope = _scopeFactory.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IStatsBombIngestionService>();
            await ingestionService.IngestSeasonAsync(item.CompetitionId, item.SeasonId, stoppingToken);
        }
    }
}
