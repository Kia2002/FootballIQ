using FootballIQ.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>Long-running worker that dequeues enrichment requests and runs them via IPlayerEnrichmentService. Runs for the lifetime of the app (registered as a hosted service).</summary>
public class PlayerDemographicsBackgroundService : BackgroundService
{
    private readonly EnrichmentWorkQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public PlayerDemographicsBackgroundService(EnrichmentWorkQueue queue, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using var scope = _scopeFactory.CreateScope();
            var enrichmentService = scope.ServiceProvider.GetRequiredService<IPlayerEnrichmentService>();
            await enrichmentService.EnrichPlayerDemographicsAsync(stoppingToken);
        }
    }
}
