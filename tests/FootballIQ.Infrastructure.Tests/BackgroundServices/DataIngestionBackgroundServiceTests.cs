using FootballIQ.Application.Interfaces;
using FootballIQ.Infrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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

    private class ThrowOnceThenRecordingIngestionService : IStatsBombIngestionService
    {
        private bool _hasThrown;
        public TaskCompletionSource<(int CompetitionId, int SeasonId)> Called = new();

        public Task<int> IngestSeasonAsync(int competitionId, int seasonId, CancellationToken ct)
        {
            if (!_hasThrown)
            {
                _hasThrown = true;
                throw new InvalidOperationException("Simulated ingestion failure");
            }

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
        var backgroundService = new DataIngestionBackgroundService(
            queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DataIngestionBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        var called = await recordingService.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await backgroundService.StopAsync(CancellationToken.None);

        Assert.Equal(11, called.CompetitionId);
        Assert.Equal(90, called.SeasonId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIngestionThrows_StillProcessesNextQueuedItem()
    {
        var throwingThenRecordingService = new ThrowOnceThenRecordingIngestionService();
        var services = new ServiceCollection();
        services.AddSingleton<IStatsBombIngestionService>(throwingThenRecordingService);
        var provider = services.BuildServiceProvider();

        var queue = new IngestionWorkQueue();
        var backgroundService = new DataIngestionBackgroundService(
            queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DataIngestionBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(competitionId: 11, seasonId: 90, CancellationToken.None);
        await queue.EnqueueAsync(competitionId: 11, seasonId: 91, CancellationToken.None);

        var called = await throwingThenRecordingService.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await backgroundService.StopAsync(CancellationToken.None);

        Assert.Equal(11, called.CompetitionId);
        Assert.Equal(91, called.SeasonId);
    }
}
