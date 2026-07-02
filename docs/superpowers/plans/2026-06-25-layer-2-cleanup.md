# Layer 2 Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 4 independent leftover issues flagged in code review after merging "Eliminate ingestion N+1 queries" (PR #3) — none are official Build Status task IDs, they're tech-debt cleanup on a dedicated branch.

**Architecture:** Each task touches exactly one file in `FootballIQ.Infrastructure` and is fully independent of the others — no shared state, can be done and committed in any order. Three of the four change observable behavior and get a new/updated test; the fourth (JsonSerializerOptions) is a pure micro-perf refactor with no behavior change, so no new test.

**Tech Stack:** xUnit, Testcontainers (Postgres) for the enrichment test, plain in-process fakes for the background service test, temp-directory-based fakes for the file reader test. No new packages.

---

### Task 1: Fix N+1 in WikidataEnrichmentService

**Files:**
- Modify: `src/FootballIQ.Infrastructure/Enrichment/WikidataEnrichmentService.cs:21-64`
- Test: `tests/FootballIQ.Infrastructure.Tests/Enrichment/WikidataEnrichmentServiceTests.cs` (existing tests act as regression guard, no new test needed — this is a pure perf refactor, behavior is unchanged)

This is a behavior-preserving refactor: the loop currently does `_context.Players.SingleAsync(p => p.Id == playerInfo.Id, ct)` to re-fetch a row that was already loaded (minus its other columns) at the top of the method. EF Core tracks entities by primary key per `DbContext` instance, so loading full `Player` entities once up front and keeping them in a dictionary removes the per-player round trip entirely, while still allowing `SaveChangesAsync` to detect and persist the `DateOfBirth` change (the entities are tracked the same way `SingleAsync` would have tracked them).

- [ ] **Step 1: Change the initial query to load full Player entities, keyed by Id**

In `WikidataEnrichmentService.cs`, replace lines 23-26:

```csharp
        var playersNeedingEnrichment = await _context.Players
            .Where(p => p.DateOfBirth == null)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);
```

with:

```csharp
        var playersNeedingEnrichment = await _context.Players
            .Where(p => p.DateOfBirth == null)
            .ToListAsync(ct);

        var playersById = playersNeedingEnrichment.ToDictionary(p => p.Id);
```

- [ ] **Step 2: Update the club-name lookup call to use the new shape**

Line 28 currently reads:

```csharp
        var clubNameByPlayerId = await GetClubNamesByPlayerIdAsync(playersNeedingEnrichment.Select(p => p.Id), ct);
```

`playersNeedingEnrichment` is now `List<Player>` instead of an anonymous-type list, but `p.Id` still resolves the same way (`Player.Id` is a `Guid`), so this line does not need to change.

- [ ] **Step 3: Replace the re-fetch with a dictionary lookup**

Replace line 55:

```csharp
                var player = await _context.Players.SingleAsync(p => p.Id == playerInfo.Id, ct);
```

with:

```csharp
                var player = playersById[playerInfo.Id];
```

- [ ] **Step 4: Run the existing test suite to confirm no regression**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter WikidataEnrichmentServiceTests`
Expected: PASS — all 4 existing tests (`WithConfidentMatch_SetsDateOfBirth`, `WithNoMatch_LeavesDateOfBirthNull`, `OnPlayerAlreadyHavingDateOfBirth_SkipsThatPlayer`, `WithTwoSeasonsAtDifferentClubs_DisambiguatesUsingMostRecentClub`) still pass unchanged. These tests assert against final DB state, not query count, so they are a correctness regression guard for this change.

- [ ] **Step 5: Commit**

```bash
git add src/FootballIQ.Infrastructure/Enrichment/WikidataEnrichmentService.cs
git commit -m "[fix] Eliminate N+1 re-fetch in WikidataEnrichmentService"
```

---

### Task 2: Stop DataIngestionBackgroundService from dying silently on exceptions

**Files:**
- Modify: `src/FootballIQ.Infrastructure/BackgroundServices/DataIngestionBackgroundService.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/BackgroundServices/DataIngestionBackgroundServiceTests.cs`

Right now, if `IngestSeasonAsync` throws (e.g. wrong `STATSBOMB_DATA_ROOT`, bad competition/season ID), the exception propagates out of `ExecuteAsync` uncaught. `BackgroundService` treats an unhandled exception from `ExecuteAsync` as fatal — the hosted service stops permanently. Every ingestion request enqueued after that point sits in the queue forever, and the API keeps returning 202 Accepted with no way to tell anything is wrong. The fix: catch the exception, log it, and keep processing the next queued item.

- [ ] **Step 1: Write the failing test — service keeps running after one item throws**

In `DataIngestionBackgroundServiceTests.cs`, add a fake that throws once then records the next call, and a test that asserts the second item still gets processed:

```csharp
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
```

Add `using Microsoft.Extensions.Logging.Abstractions;` to the top of the test file (needed for `NullLogger<T>`).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter ExecuteAsync_WhenIngestionThrows_StillProcessesNextQueuedItem`
Expected: FAIL with a compile error — this is the red step of TDD. `DataIngestionBackgroundService` still has the two-parameter constructor from Task 2.6 (`2026-06-19-admin-ingest-endpoint.md`) at this point, so the three-argument call above (`queue, scopeFactory, NullLogger<...>.Instance`) doesn't match any overload yet. Step 3 below adds the `ILogger` parameter, which fixes this compile error and makes the test runnable (and pass).

- [ ] **Step 3: Add ILogger and try/catch to the background service**

Replace the full contents of `DataIngestionBackgroundService.cs`:

```csharp
using FootballIQ.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>Long-running worker that dequeues ingestion requests and runs them via IStatsBombIngestionService. Runs for the lifetime of the app (registered as a hosted service).</summary>
public class DataIngestionBackgroundService : BackgroundService
{
    private readonly IngestionWorkQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataIngestionBackgroundService> _logger;

    public DataIngestionBackgroundService(
        IngestionWorkQueue queue, IServiceScopeFactory scopeFactory, ILogger<DataIngestionBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
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

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<IStatsBombIngestionService>();
                await ingestionService.IngestSeasonAsync(item.CompetitionId, item.SeasonId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Ingestion failed for competitionId={CompetitionId}, seasonId={SeasonId}",
                    item.CompetitionId, item.SeasonId);
            }
        }
    }
}
```

The `when (ex is not OperationCanceledException)` guard makes sure a real shutdown cancellation still propagates and stops the loop cleanly, instead of being logged and swallowed like a normal failure.

The constructor now takes three parameters instead of two, so the existing `ExecuteAsync_WhenItemEnqueued_CallsIngestionServiceWithSameArgs` test (added in Task 2.6's plan, `2026-06-19-admin-ingest-endpoint.md`) no longer compiles as written. Update its `new DataIngestionBackgroundService(...)` call to also pass `NullLogger<DataIngestionBackgroundService>.Instance` as the third argument, same as the new test above.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter DataIngestionBackgroundServiceTests`
Expected: PASS — both `ExecuteAsync_WhenItemEnqueued_CallsIngestionServiceWithSameArgs` (existing, updated to pass the logger arg) and `ExecuteAsync_WhenIngestionThrows_StillProcessesNextQueuedItem` (new) pass.

- [ ] **Step 5: Commit**

```bash
git add src/FootballIQ.Infrastructure/BackgroundServices/DataIngestionBackgroundService.cs tests/FootballIQ.Infrastructure.Tests/BackgroundServices/DataIngestionBackgroundServiceTests.cs
git commit -m "[fix] Log and recover from ingestion failures in background service instead of dying silently"
```

---

### Task 3: Add file-not-found context to StatsBombReader

**Files:**
- Modify: `src/FootballIQ.Infrastructure/StatsBomb/StatsBombReader.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/StatsBomb/StatsBombReaderTests.cs`

All three read methods throw a bare `FileNotFoundException` with just the OS file path if the JSON file is missing. The fix wraps each read in try/catch and rethrows with the actual match/competition/season ID, so a failure during ingestion of, say, match 3773499 is immediately diagnosable instead of needing to decode a file path from a stack trace. This test doesn't need the real StatsBomb dataset (unlike the existing tests in this file, which skip without it) — it constructs an empty temp directory so the file is guaranteed missing.

- [ ] **Step 1: Write the failing tests**

Add to `StatsBombReaderTests.cs`:

```csharp
    [Fact]
    public async Task GetMatchesAsync_WhenFileMissing_ThrowsWithCompetitionAndSeasonContext()
    {
        var emptyDataRoot = Directory.CreateTempSubdirectory().FullName;
        var reader = new StatsBombReader(emptyDataRoot);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.GetMatchesAsync(competitionId: 11, seasonId: 90, CancellationToken.None));

        Assert.Contains("competitionId=11", ex.Message);
        Assert.Contains("seasonId=90", ex.Message);
    }

    [Fact]
    public async Task GetEventsAsync_WhenFileMissing_ThrowsWithMatchIdContext()
    {
        var emptyDataRoot = Directory.CreateTempSubdirectory().FullName;
        var reader = new StatsBombReader(emptyDataRoot);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.GetEventsAsync(matchId: 3773386, CancellationToken.None));

        Assert.Contains("matchId=3773386", ex.Message);
    }

    [Fact]
    public async Task GetLineupAsync_WhenFileMissing_ThrowsWithMatchIdContext()
    {
        var emptyDataRoot = Directory.CreateTempSubdirectory().FullName;
        var reader = new StatsBombReader(emptyDataRoot);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.GetLineupAsync(matchId: 3773386, CancellationToken.None));

        Assert.Contains("matchId=3773386", ex.Message);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter "StatsBombReaderTests"`
Expected: FAIL — the three new tests throw `FileNotFoundException`, but `Assert.Contains` fails because the current message is just the raw OS path, not `"competitionId=11"` / `"matchId=3773386"`.

- [ ] **Step 3: Add try/catch with context to each read method**

Replace the full contents of `StatsBombReader.cs`:

```csharp
using System.Text.Json;
using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Reads StatsBomb open-data JSON files from disk under a given data root (the "data/statsbomb/data" folder).</summary>
public class StatsBombReader : IStatsBombReader
{
    private readonly string _dataRoot;
    private static readonly JsonSerializerOptions JsonOptions = new();

    public StatsBombReader(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public async Task<List<StatsBombMatch>> GetMatchesAsync(int competitionId, int seasonId, CancellationToken ct)
    {
        var path = Path.Combine(_dataRoot, "matches", competitionId.ToString(), $"{seasonId}.json");
        var json = await ReadFileAsync(path, $"competitionId={competitionId}, seasonId={seasonId}", ct);
        return JsonSerializer.Deserialize<List<StatsBombMatch>>(json, JsonOptions) ?? [];
    }

    public async Task<List<StatsBombEvent>> GetEventsAsync(int matchId, CancellationToken ct)
    {
        var path = Path.Combine(_dataRoot, "events", $"{matchId}.json");
        var json = await ReadFileAsync(path, $"matchId={matchId}", ct);
        return JsonSerializer.Deserialize<List<StatsBombEvent>>(json, JsonOptions) ?? [];
    }

    public async Task<List<StatsBombLineupTeam>> GetLineupAsync(int matchId, CancellationToken ct)
    {
        var path = Path.Combine(_dataRoot, "lineups", $"{matchId}.json");
        var json = await ReadFileAsync(path, $"matchId={matchId}", ct);
        return JsonSerializer.Deserialize<List<StatsBombLineupTeam>>(json, JsonOptions) ?? [];
    }

    private static async Task<string> ReadFileAsync(string path, string context, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"StatsBomb data file not found for {context} at path '{path}'.", path, ex);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter "StatsBombReaderTests"`
Expected: PASS — all 6 tests in this file pass (3 existing, which skip without real data, and 3 new ones, which run unconditionally since they don't need real data).

- [ ] **Step 5: Commit**

```bash
git add src/FootballIQ.Infrastructure/StatsBomb/StatsBombReader.cs tests/FootballIQ.Infrastructure.Tests/StatsBomb/StatsBombReaderTests.cs
git commit -m "[fix] Include match/competition/season ID in StatsBombReader file-not-found errors"
```

---

### Task 4: Stop allocating JsonSerializerOptions per call in FootballDataClient

**Files:**
- Modify: `src/FootballIQ.Infrastructure/FootballData/FootballDataClient.cs`
- Test: none — pure micro-perf refactor, no observable behavior change, existing test is the regression guard

- [ ] **Step 1: Hoist JsonSerializerOptions to a static readonly field**

Replace the full contents of `FootballDataClient.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using FootballIQ.Infrastructure.FootballData.Models;

namespace FootballIQ.Infrastructure.FootballData;

/// <summary>Typed HTTP client for the football-data.org API.</summary>
public class FootballDataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public FootballDataClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>Fetches all matches for the given competition.</summary>
    public async Task<CompetitionMatchesResponse> GetMatchesAsync(int competitionId, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"competitions/{competitionId}/matches", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CompetitionMatchesResponse>(JsonOptions, ct);

        return result ?? throw new InvalidOperationException("football-data.org returned an empty response.");
    }
}
```

- [ ] **Step 2: Run the existing test to confirm no regression**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter FootballDataClientTests`
Expected: PASS (or SKIP if `FOOTBALLDATA_API_KEY` is not set — that's the existing, expected behavior of this test).

- [ ] **Step 3: Commit**

```bash
git add src/FootballIQ.Infrastructure/FootballData/FootballDataClient.cs
git commit -m "[fix] Stop allocating JsonSerializerOptions per call in FootballDataClient"
```

---

### Final check across all 4 tasks

- [ ] Run the full suite once more to confirm nothing else broke:

Run: `dotnet test`
Expected: All projects PASS (Testcontainers-backed tests require `docker compose up -d` to be running first — confirm with `docker compose ps` showing `postgres` healthy before running this).
