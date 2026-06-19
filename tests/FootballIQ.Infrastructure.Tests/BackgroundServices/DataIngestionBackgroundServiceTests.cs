using FootballIQ.Application.Interfaces;
using FootballIQ.Infrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;

namespace FootballIQ.Infrastructure.Tests.BackgroundServices;

public class DataIngestionBackgroundServiceTests
{
    private class RecordingIngestionService : IStatsBombIngestionService
    {
        public TaskCompletionSource<(int CompetitionId, int SeasonId)> Called = new();

        public Task<int> IngestSeasonAsync(int competitionId, int seasonId, CancellationToken ct)
        {
            Called.TrySetResult((competitionId, seasonId));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenItemEnqueued_CallsIngestionServiceWithSameArgs()
    {
        var recordingService = new RecordingIngestionService();
        var services = new ServiceCollection();
        services.AddSingleton<IStatsBombIngestionService>(recordingService);
        var provider = services.BuildServiceProvider();

        var queue = new IngestionWorkQueue();
        var backgroundService = new DataIngestionBackgroundService(queue, provider.GetRequiredService<IServiceScopeFactory>());

        await backgroundService.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        var called = await recordingService.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await backgroundService.StopAsync(CancellationToken.None);

        Assert.Equal(11, called.CompetitionId);
        Assert.Equal(90, called.SeasonId);
    }
}
