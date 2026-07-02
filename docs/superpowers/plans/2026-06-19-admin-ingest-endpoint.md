# Admin Ingest Endpoint (Task 2.6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `POST /api/admin/ingest` which enqueues a StatsBomb season for ingestion and returns immediately (202 Accepted), while a background worker processes the queue and calls the existing `IStatsBombIngestionService`.

**Architecture:** A `Channel<(int CompetitionId, int SeasonId)>`-backed queue (`IngestionWorkQueue`, implementing `IIngestionQueue`) sits between the endpoint (producer) and a `BackgroundService` (`DataIngestionBackgroundService`, consumer). The background service is a singleton, so it resolves the scoped `IStatsBombIngestionService`/`DbContext` via a fresh `IServiceScopeFactory.CreateScope()` per dequeued item.

**Tech Stack:** `System.Threading.Channels`, `Microsoft.Extensions.Hosting.BackgroundService`, ASP.NET Core Minimal APIs.

---

### Task 1: IIngestionQueue interface

**Files:**
- Create: `src/FootballIQ.Application/Interfaces/IIngestionQueue.cs`

- [ ] **Step 1: Write the interface**

```csharp
namespace FootballIQ.Application.Interfaces;

/// <summary>Queues a StatsBomb season for background ingestion.</summary>
public interface IIngestionQueue
{
    /// <summary>Enqueues a competition/season pair to be ingested by the background worker.</summary>
    ValueTask EnqueueAsync(int competitionId, int seasonId, CancellationToken ct);
}
```

No test for this step — it's a pure interface declaration, nothing to fail/pass yet. The behavior is tested in Task 2.

---

### Task 2: IngestionWorkQueue (Channel-backed implementation)

**Files:**
- Create: `src/FootballIQ.Infrastructure/BackgroundServices/IngestionWorkQueue.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/BackgroundServices/IngestionWorkQueueTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter IngestionWorkQueueTests`
Expected: FAIL to compile — `IngestionWorkQueue` does not exist (CS0246). This is the expected "red" for a new type in C#: the test can't even compile until the type exists.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Threading.Channels;
using FootballIQ.Application.Interfaces;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>In-memory FIFO queue of ingestion work items, backed by a Channel. One instance is shared (singleton) between the producer (admin endpoint) and the consumer (DataIngestionBackgroundService).</summary>
public class IngestionWorkQueue : IIngestionQueue
{
    private readonly Channel<(int CompetitionId, int SeasonId)> _channel =
        Channel.CreateUnbounded<(int CompetitionId, int SeasonId)>();

    public ValueTask EnqueueAsync(int competitionId, int seasonId, CancellationToken ct) =>
        _channel.Writer.WriteAsync((competitionId, seasonId), ct);

    public ValueTask<(int CompetitionId, int SeasonId)> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter IngestionWorkQueueTests`
Expected: PASS

- [ ] **Step 5: Commit**

This is one step within Task 2.6 — do **not** commit yet. Per the project's git convention, all steps in this plan are squashed into a single `[2.6]` commit at the very end (Task 5).

---

### Task 3: DataIngestionBackgroundService

**Files:**
- Create: `src/FootballIQ.Infrastructure/BackgroundServices/DataIngestionBackgroundService.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/BackgroundServices/DataIngestionBackgroundServiceTests.cs`

- [ ] **Step 1: Write the failing test**

This test uses a hand-written fake `IStatsBombIngestionService` (recorder) registered in a real `ServiceCollection`, so we can verify the background service actually resolves a scope and calls the ingestion service with the right arguments — without touching a real database.

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter DataIngestionBackgroundServiceTests`
Expected: FAIL to compile — `DataIngestionBackgroundService` does not exist (CS0246).

- [ ] **Step 3: Write minimal implementation**

```csharp
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
```

Note: the test constructs `DataIngestionBackgroundService` directly with a concrete `IngestionWorkQueue` (not the `IIngestionQueue` interface) — the background service needs the concrete dequeue-capable type, while the endpoint only needs the producer-facing `IIngestionQueue` interface. Both will resolve to the same singleton instance via DI (Task 4).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter DataIngestionBackgroundServiceTests`
Expected: PASS

---

### Task 4: AdminEndpoints + DI wiring

**Files:**
- Create: `src/FootballIQ.WebAPI/Endpoints/AdminEndpoints.cs`
- Modify: `src/FootballIQ.WebAPI/Program.cs`
- Modify: `.env.example`

- [ ] **Step 1: Write AdminEndpoints**

```csharp
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
```

- [ ] **Step 2: Add STATSBOMB_DATA_ROOT to .env.example**

Add after the `FOOTBALLDATA_API_KEY` line in `.env.example`:

```
# Path to the cloned StatsBomb open-data "data" folder (see CLAUDE.md StatsBomb Data Reference)
STATSBOMB_DATA_ROOT=data/statsbomb/data
```

- [ ] **Step 3: Wire DI in Program.cs**

Modify `src/FootballIQ.WebAPI/Program.cs`. Add these usings near the top:

```csharp
using FootballIQ.Infrastructure.BackgroundServices;
using FootballIQ.Infrastructure.StatsBomb;
```

Add this block after the existing `builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();` line:

```csharp
var statsBombDataRoot = builder.Configuration["STATSBOMB_DATA_ROOT"]
    ?? throw new InvalidOperationException("STATSBOMB_DATA_ROOT is not configured.");

builder.Services.AddScoped<IStatsBombReader>(_ => new StatsBombReader(statsBombDataRoot));
builder.Services.AddScoped<PlayerStatsAggregator>();
builder.Services.AddScoped<IStatsBombIngestionService, StatsBombIngestionService>();

builder.Services.AddSingleton<IngestionWorkQueue>();
builder.Services.AddSingleton<IIngestionQueue>(sp => sp.GetRequiredService<IngestionWorkQueue>());
builder.Services.AddHostedService<DataIngestionBackgroundService>();
```

Add this line after `app.MapPlayerEndpoints();`:

```csharp
app.MapAdminEndpoints();
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build FootballIQ.sln`
Expected: Build succeeded, 0 errors.

---

### Task 5: Manual verification, docs, commit

- [ ] **Step 1: Manually verify the endpoint end-to-end**

With `docker compose up -d` running and `data/statsbomb` cloned, run `dotnet run --project src/FootballIQ.WebAPI`, then in Swagger UI:
1. `POST /api/admin/ingest` with `{ "competitionId": 11, "seasonId": 90 }` — expect `202 Accepted` returned immediately.
2. Wait a few seconds (35 matches takes time).
3. `GET /api/players` — expect real player names from La Liga 2020/21.

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test`
Expected: all tests pass (0 failed), including the two new tests from Task 2 and Task 3.

- [ ] **Step 3: Update CLAUDE.md Build Status**

Change row 2.6 from `🔲` to `✅`.

- [ ] **Step 4: Add LEARNING.md entry**

Add a "## Task 2.6: Admin Ingest Endpoint" entry covering: IHostedService/BackgroundService, Channel<T> producer/consumer queue, the singleton-vs-scoped DI lifetime mismatch and why IServiceScopeFactory.CreateScope() solves it, and the 202 Accepted response code.

- [ ] **Step 5: Squash and commit**

```bash
git add -A
git commit -m "[2.6] Add POST /api/admin/ingest with background ingestion queue"
```

---

## Self-Review

**Spec coverage:** "Done when: POST → wait → GET /api/players returns real players" — covered by Task 5 Step 1 (manual verification) plus the automated queue/background-service tests proving the plumbing works in isolation.

**Placeholder scan:** No TBD/TODO; all code is complete and concrete.

**Type consistency:** `IIngestionQueue.EnqueueAsync(int, int, CancellationToken)` matches its use in `AdminEndpoints` and its implementation in `IngestionWorkQueue`. `IngestionWorkQueue.DequeueAsync` returns `(int CompetitionId, int SeasonId)` consistently across the queue, the background service, and both tests.
