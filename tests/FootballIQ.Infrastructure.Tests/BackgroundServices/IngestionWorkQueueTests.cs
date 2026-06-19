using FootballIQ.Infrastructure.BackgroundServices;

namespace FootballIQ.Infrastructure.Tests.BackgroundServices;

public class IngestionWorkQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ThenDequeueAsync_ReturnsSameItem()
    {
        var queue = new IngestionWorkQueue();

        await queue.EnqueueAsync(competitionId: 11, seasonId: 90, CancellationToken.None);
        var item = await queue.DequeueAsync(CancellationToken.None);

        Assert.Equal(11, item.CompetitionId);
        Assert.Equal(90, item.SeasonId);
    }
}
