# Idempotent StatsBomb Ingestion (Task 2.5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Session note:** this plan is being executed *inline*, one step at a time, with the user (a junior dev) reading and understanding every change — not dispatched to subagents. Treat the steps below as a checklist for the session, not an autonomous handoff.

**Goal:** Build a `StatsBombIngestionService` that reads matches/events/lineups via the existing `IStatsBombReader`, writes `Club`, `Player`, `Match`, and `PlayerSeasonStats` rows to Postgres, and is safe to re-run: running it twice for the same competition/season produces identical row counts (no duplicates).

**Architecture:** A new `IngestionLog` table records one row per StatsBomb match ID once it has been fully processed. Before processing a match, the service checks this table; if a row already exists, the match is skipped entirely. The check-and-write happens inside the same `SaveChangesAsync` call as the rest of that match's data, so a crash mid-match never leaves a "half-ingested, marked as done" state.

**Tech Stack:** EF Core 9 (migrations, `DbContext`), existing `PlayerStatsAggregator`/`IStatsBombReader` from Tasks 2.3–2.4, xUnit + Testcontainers.PostgreSql for the integration test (same pattern as `PlayerRepositoryTests`).

---

## Design decision: raw counts, not pre-computed rates, in `PlayerAggregateStats`

`PlayerAggregateStats` (Task 2.4) currently stores `PassCompletionPct` and `PressuresPer90` — both *rates*, computed per match. Ingestion needs to **accumulate across matches** (a player's season pass accuracy isn't the average of per-match percentages — that's statistically wrong, a version of Simpson's paradox: 10/10 one match + 0/10 another match is 50% overall, not the average of 100% and 0%). So `PlayerAggregateStats` needs to expose the raw counts (`PassesCompleted`, `PassesAttempted`, `Pressures`, `MinutesPlayed`) instead of pre-baked rates. `PassCompletionPct` and `PressuresPer90` become computed properties derived from those raw fields — same names, same values, so the existing `PlayerStatsAggregatorTests` assertions keep passing unchanged.

`PlayerSeasonStats.Goals` / `.Assists` are out of scope for this task — the aggregator never computed goal/assist counts in 2.4, and adding that is a separate concern. They stay at their default `0` until a future task adds it.

## File Structure

- Modify: `src/FootballIQ.Infrastructure/StatsBomb/PlayerAggregateStats.cs` — raw counts instead of rates
- Modify: `src/FootballIQ.Infrastructure/StatsBomb/PlayerStatsAggregator.cs` — populate raw counts
- Create: `src/FootballIQ.Domain/Entities/IngestionLog.cs`
- Create: `src/FootballIQ.Infrastructure/Persistence/Configurations/IngestionLogConfiguration.cs`
- Modify: `src/FootballIQ.Infrastructure/Persistence/FootballIQDbContext.cs` — add `DbSet<IngestionLog>`
- Create: EF migration `AddIngestionLog`
- Create: `src/FootballIQ.Application/Interfaces/IStatsBombIngestionService.cs`
- Create: `src/FootballIQ.Infrastructure/StatsBomb/StatsBombIngestionService.cs`
- Create: `tests/FootballIQ.Infrastructure.Tests/StatsBomb/FakeStatsBombReader.cs` — hand-written test double (no mocking library used in this project's Infrastructure tests)
- Create: `tests/FootballIQ.Infrastructure.Tests/StatsBomb/StatsBombIngestionServiceTests.cs`

---

### Task 1: Refactor `PlayerAggregateStats` to expose raw counts

**Files:**
- Modify: `src/FootballIQ.Infrastructure/StatsBomb/PlayerAggregateStats.cs`
- Modify: `src/FootballIQ.Infrastructure/StatsBomb/PlayerStatsAggregator.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/StatsBomb/PlayerStatsAggregatorTests.cs` (existing — must keep passing with zero edits)

- [ ] **Step 1: Run the existing aggregator tests to confirm the current baseline passes**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter PlayerStatsAggregatorTests`
Expected: all 4 tests PASS (this is our regression baseline before refactoring)

- [ ] **Step 2: Rewrite `PlayerAggregateStats.cs` with raw fields + derived properties**

```csharp
namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Per-match aggregate stats for one player, computed from raw StatsBomb events.</summary>
public record PlayerAggregateStats
{
    public int PlayerId { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public int PassesCompleted { get; init; }

    public int PassesAttempted { get; init; }

    public double TotalXg { get; init; }

    public double TotalXa { get; init; }

    public int Pressures { get; init; }

    public double MinutesPlayed { get; init; }

    public double PassCompletionPct =>
        PassesAttempted == 0 ? 0 : (double)PassesCompleted / PassesAttempted;

    public double PressuresPer90 =>
        MinutesPlayed == 0 ? 0 : Pressures / (MinutesPlayed / 90.0);
}
```

- [ ] **Step 3: Update `PlayerStatsAggregator.ComputeStats` to populate the raw fields**

Replace the `Select` projection body in `ComputeStats` with:

```csharp
        return eventsByPlayer
            .Select(group =>
            {
                var passes = group.Where(e => e.Type.Name == "Pass").ToList();
                var totalPasses = passes.Count;
                var completedPasses = passes.Count(e => e.Pass?.Outcome is null);

                var totalXg = group
                    .Where(e => e.Type.Name == "Shot")
                    .Sum(e => e.Shot?.StatsbombXg ?? 0);

                var totalXa = passes
                    .Where(e => e.Pass?.ShotAssist == true && e.Pass.AssistedShotId is not null)
                    .Sum(e => shotXgById.GetValueOrDefault(e.Pass!.AssistedShotId!, 0));

                var totalPressures = group.Count(e => e.Type.Name == "Pressure");
                var minutesPlayed = minutesPlayedByPlayerId.GetValueOrDefault(group.Key.Id, 0);

                return new PlayerAggregateStats
                {
                    PlayerId = group.Key.Id,
                    PlayerName = group.Key.Name,
                    PassesCompleted = completedPasses,
                    PassesAttempted = totalPasses,
                    TotalXg = totalXg,
                    TotalXa = totalXa,
                    Pressures = totalPressures,
                    MinutesPlayed = minutesPlayed
                };
            })
            .ToList();
```

- [ ] **Step 4: Run the existing aggregator tests again — must still pass with zero test-file edits**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter PlayerStatsAggregatorTests`
Expected: all 4 tests PASS (same assertions, now backed by computed properties)

- [ ] **Step 5: Commit**

```powershell
git add src/FootballIQ.Infrastructure/StatsBomb/PlayerAggregateStats.cs src/FootballIQ.Infrastructure/StatsBomb/PlayerStatsAggregator.cs
git commit -m "[2.5] Expose raw pass/pressure counts from PlayerAggregateStats"
```

---

### Task 2: `IngestionLog` entity + EF configuration + migration

**Files:**
- Create: `src/FootballIQ.Domain/Entities/IngestionLog.cs`
- Create: `src/FootballIQ.Infrastructure/Persistence/Configurations/IngestionLogConfiguration.cs`
- Modify: `src/FootballIQ.Infrastructure/Persistence/FootballIQDbContext.cs`

- [ ] **Step 1: Create the entity**

```csharp
namespace FootballIQ.Domain.Entities;

/// <summary>Records that a StatsBomb match has been fully ingested, so re-running ingestion skips it.</summary>
public class IngestionLog
{
    public Guid Id { get; set; }
    public int StatsBombMatchId { get; set; }
    public DateTime IngestedAt { get; set; }
}
```

- [ ] **Step 2: Create the EF configuration with a unique index on `StatsBombMatchId`**

```csharp
using FootballIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballIQ.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the IngestionLog entity.</summary>
public class IngestionLogConfiguration : IEntityTypeConfiguration<IngestionLog>
{
    public void Configure(EntityTypeBuilder<IngestionLog> builder)
    {
        builder.ToTable("ingestion_log");

        builder.HasKey(l => l.Id);

        builder.HasIndex(l => l.StatsBombMatchId)
            .IsUnique();
    }
}
```

- [ ] **Step 3: Add the `DbSet` to `FootballIQDbContext`**

In `src/FootballIQ.Infrastructure/Persistence/FootballIQDbContext.cs`, add next to the other `DbSet`s:

```csharp
    public DbSet<IngestionLog> IngestionLogs => Set<IngestionLog>();
```

- [ ] **Step 4: Build to confirm it compiles**

Run: `dotnet build FootballIQ.sln`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Create the migration** (⚠️ explain to the user what this generates and wait for confirmation before running)

```powershell
dotnet ef migrations add AddIngestionLog --project src/FootballIQ.Infrastructure --startup-project src/FootballIQ.WebAPI
```

Expected: a new file under `src/FootballIQ.Infrastructure/Persistence/Migrations/` creating the `ingestion_log` table with a unique index on `statsbomb_match_id`.

- [ ] **Step 6: Commit**

```powershell
git add src/FootballIQ.Domain/Entities/IngestionLog.cs src/FootballIQ.Infrastructure/Persistence/Configurations/IngestionLogConfiguration.cs src/FootballIQ.Infrastructure/Persistence/FootballIQDbContext.cs src/FootballIQ.Infrastructure/Persistence/Migrations/
git commit -m "[2.5] Add IngestionLog entity, configuration, and migration"
```

---

### Task 3: `IStatsBombIngestionService` interface (Application layer)

**Files:**
- Create: `src/FootballIQ.Application/Interfaces/IStatsBombIngestionService.cs`

- [ ] **Step 1: Define the interface**

```csharp
namespace FootballIQ.Application.Interfaces;

/// <summary>Ingests StatsBomb match data into the database, skipping matches already recorded in the ingestion log.</summary>
public interface IStatsBombIngestionService
{
    /// <summary>Ingests every not-yet-ingested match for the given competition and season. Returns the number of matches newly ingested.</summary>
    Task<int> IngestSeasonAsync(int competitionId, int seasonId, CancellationToken ct);
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build FootballIQ.sln`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```powershell
git add src/FootballIQ.Application/Interfaces/IStatsBombIngestionService.cs
git commit -m "[2.5] Add IStatsBombIngestionService interface"
```

---

### Task 4: Failing integration test for idempotent ingestion (TDD — write this before the service exists)

**Files:**
- Create: `tests/FootballIQ.Infrastructure.Tests/StatsBomb/FakeStatsBombReader.cs`
- Create: `tests/FootballIQ.Infrastructure.Tests/StatsBomb/StatsBombIngestionServiceTests.cs`

- [ ] **Step 1: Create a hand-written fake `IStatsBombReader`** (one match, two teams, three players — small enough to read in full)

```csharp
using FootballIQ.Infrastructure.StatsBomb;
using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

/// <summary>Returns fixed, hand-built StatsBomb data for one match, so ingestion tests don't depend on the gitignored data/statsbomb folder.</summary>
public class FakeStatsBombReader : IStatsBombReader
{
    public const int MatchId = 9001;
    public const int HomeTeamId = 1;
    public const int AwayTeamId = 2;
    public const int HomePlayerId = 10;
    public const int AwayPlayerId = 20;

    public Task<List<StatsBombMatch>> GetMatchesAsync(int competitionId, int seasonId, CancellationToken ct)
    {
        return Task.FromResult(new List<StatsBombMatch>
        {
            new()
            {
                MatchId = MatchId,
                MatchDate = "2021-05-01",
                HomeTeam = new StatsBombHomeTeam { HomeTeamId = HomeTeamId, HomeTeamName = "Home FC" },
                AwayTeam = new StatsBombAwayTeam { AwayTeamId = AwayTeamId, AwayTeamName = "Away FC" },
                HomeScore = 2,
                AwayScore = 1,
                Competition = new StatsBombCompetition { CompetitionId = competitionId, CompetitionName = "Test League" },
                Season = new StatsBombSeason { SeasonId = seasonId, SeasonName = "2020/2021" }
            }
        });
    }

    public Task<List<StatsBombEvent>> GetEventsAsync(int matchId, CancellationToken ct)
    {
        return Task.FromResult(new List<StatsBombEvent>
        {
            new()
            {
                Type = new StatsBombNamedId { Id = 30, Name = "Pass" },
                Team = new StatsBombNamedId { Id = HomeTeamId, Name = "Home FC" },
                Player = new StatsBombNamedId { Id = HomePlayerId, Name = "Home Player" },
                Pass = new StatsBombPassData()
            },
            new()
            {
                Type = new StatsBombNamedId { Id = 16, Name = "Shot" },
                Team = new StatsBombNamedId { Id = AwayTeamId, Name = "Away FC" },
                Player = new StatsBombNamedId { Id = AwayPlayerId, Name = "Away Player" },
                Shot = new StatsBombShotData { StatsbombXg = 0.25 }
            }
        });
    }

    public Task<List<StatsBombLineupTeam>> GetLineupAsync(int matchId, CancellationToken ct)
    {
        return Task.FromResult(new List<StatsBombLineupTeam>
        {
            new()
            {
                TeamId = HomeTeamId,
                TeamName = "Home FC",
                Lineup = new List<StatsBombLineupPlayer>
                {
                    new() { PlayerId = HomePlayerId, PlayerName = "Home Player" }
                }
            },
            new()
            {
                TeamId = AwayTeamId,
                TeamName = "Away FC",
                Lineup = new List<StatsBombLineupPlayer>
                {
                    new() { PlayerId = AwayPlayerId, PlayerName = "Away Player" }
                }
            }
        });
    }
}
```

- [ ] **Step 2: Write the failing test**

```csharp
using FootballIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

public class StatsBombIngestionServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private FootballIQDbContext _context = null!;
    private StatsBombIngestionService _service = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<FootballIQDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new FootballIQDbContext(options);
        await _context.Database.MigrateAsync();

        _service = new StatsBombIngestionService(new FakeStatsBombReader(), _context, new PlayerStatsAggregator());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task IngestSeasonAsync_RunTwice_ProducesIdenticalRowCounts()
    {
        var firstRunCount = await _service.IngestSeasonAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        Assert.Equal(1, firstRunCount);
        Assert.Equal(2, await _context.Clubs.CountAsync());
        Assert.Equal(2, await _context.Players.CountAsync());
        Assert.Equal(1, await _context.Matches.CountAsync());
        Assert.Equal(2, await _context.PlayerSeasonStats.CountAsync());
        Assert.Equal(1, await _context.IngestionLogs.CountAsync());

        var secondRunCount = await _service.IngestSeasonAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        Assert.Equal(0, secondRunCount);
        Assert.Equal(2, await _context.Clubs.CountAsync());
        Assert.Equal(2, await _context.Players.CountAsync());
        Assert.Equal(1, await _context.Matches.CountAsync());
        Assert.Equal(2, await _context.PlayerSeasonStats.CountAsync());
        Assert.Equal(1, await _context.IngestionLogs.CountAsync());
    }

    [Fact]
    public async Task IngestSeasonAsync_AccumulatesSeasonStatsAcrossMatches()
    {
        await _service.IngestSeasonAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        var homePlayer = await _context.Players.SingleAsync(p => p.StatsBombPlayerId == FakeStatsBombReader.HomePlayerId);
        var seasonStats = await _context.PlayerSeasonStats.SingleAsync(s => s.PlayerId == homePlayer.Id);

        Assert.Equal(1, seasonStats.MatchesPlayed);
        Assert.Equal(1, seasonStats.PassesAttempted);
        Assert.Equal(1, seasonStats.PassesCompleted);
    }
}
```

- [ ] **Step 3: Run the test to confirm it fails because `StatsBombIngestionService` doesn't exist yet**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter StatsBombIngestionServiceTests`
Expected: FAIL with a compile error — `StatsBombIngestionService` not found

- [ ] **Step 4: Commit the test (it's expected to fail — that's the TDD checkpoint, not a problem)**

```powershell
git add tests/FootballIQ.Infrastructure.Tests/StatsBomb/FakeStatsBombReader.cs tests/FootballIQ.Infrastructure.Tests/StatsBomb/StatsBombIngestionServiceTests.cs
git commit -m "[2.5] Add failing test for idempotent StatsBomb ingestion"
```

---

### Task 5: Implement `StatsBombIngestionService`

**Files:**
- Create: `src/FootballIQ.Infrastructure/StatsBomb/StatsBombIngestionService.cs`

- [ ] **Step 1: Implement the service**

```csharp
using FootballIQ.Application.Interfaces;
using FootballIQ.Domain.Entities;
using FootballIQ.Infrastructure.Persistence;
using FootballIQ.Infrastructure.StatsBomb.Models;
using Microsoft.EntityFrameworkCore;

namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Reads StatsBomb match data via IStatsBombReader and persists it, skipping matches already recorded in IngestionLog.</summary>
public class StatsBombIngestionService : IStatsBombIngestionService
{
    private readonly IStatsBombReader _reader;
    private readonly FootballIQDbContext _context;
    private readonly PlayerStatsAggregator _aggregator;

    public StatsBombIngestionService(IStatsBombReader reader, FootballIQDbContext context, PlayerStatsAggregator aggregator)
    {
        _reader = reader;
        _context = context;
        _aggregator = aggregator;
    }

    public async Task<int> IngestSeasonAsync(int competitionId, int seasonId, CancellationToken ct)
    {
        var matches = await _reader.GetMatchesAsync(competitionId, seasonId, ct);
        var ingestedCount = 0;

        foreach (var match in matches)
        {
            var alreadyIngested = await _context.IngestionLogs
                .AnyAsync(l => l.StatsBombMatchId == match.MatchId, ct);

            if (alreadyIngested)
            {
                continue;
            }

            await IngestMatchAsync(match, competitionId, seasonId, ct);
            ingestedCount++;
        }

        return ingestedCount;
    }

    private async Task IngestMatchAsync(StatsBombMatch match, int competitionId, int seasonId, CancellationToken ct)
    {
        var events = await _reader.GetEventsAsync(match.MatchId, ct);
        var lineups = await _reader.GetLineupAsync(match.MatchId, ct);

        var homeClub = await GetOrCreateClubAsync(match.HomeTeam.HomeTeamId, match.HomeTeam.HomeTeamName, ct);
        var awayClub = await GetOrCreateClubAsync(match.AwayTeam.AwayTeamId, match.AwayTeam.AwayTeamName, ct);

        _context.Matches.Add(new Match
        {
            Id = Guid.NewGuid(),
            StatsBombMatchId = match.MatchId,
            CompetitionId = competitionId,
            SeasonId = seasonId,
            MatchDate = DateTime.Parse(match.MatchDate),
            HomeClubId = homeClub.Id,
            AwayClubId = awayClub.Id,
            HomeScore = match.HomeScore,
            AwayScore = match.AwayScore
        });

        var clubIdByStatsBombPlayerId = lineups.ToDictionary(
            team => team,
            team => team.TeamId == match.HomeTeam.HomeTeamId ? homeClub.Id : awayClub.Id)
            .SelectMany(kvp => kvp.Key.Lineup.Select(p => (p.PlayerId, ClubId: kvp.Value)))
            .ToDictionary(x => x.PlayerId, x => x.ClubId);

        var playerStats = _aggregator.ComputeStats(events, lineups);

        foreach (var stats in playerStats)
        {
            if (!clubIdByStatsBombPlayerId.TryGetValue(stats.PlayerId, out var clubId))
            {
                continue;
            }

            var player = await GetOrCreatePlayerAsync(stats.PlayerId, stats.PlayerName, ct);
            await UpsertSeasonStatsAsync(player.Id, clubId, competitionId, seasonId, stats, ct);
        }

        _context.IngestionLogs.Add(new IngestionLog
        {
            Id = Guid.NewGuid(),
            StatsBombMatchId = match.MatchId,
            IngestedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
    }

    private async Task<Club> GetOrCreateClubAsync(int statsBombTeamId, string name, CancellationToken ct)
    {
        var club = await _context.Clubs.FirstOrDefaultAsync(c => c.StatsBombTeamId == statsBombTeamId, ct);
        if (club is not null)
        {
            return club;
        }

        club = new Club { Id = Guid.NewGuid(), StatsBombTeamId = statsBombTeamId, Name = name };
        _context.Clubs.Add(club);
        return club;
    }

    private async Task<Player> GetOrCreatePlayerAsync(int statsBombPlayerId, string name, CancellationToken ct)
    {
        var player = await _context.Players.FirstOrDefaultAsync(p => p.StatsBombPlayerId == statsBombPlayerId, ct);
        if (player is not null)
        {
            return player;
        }

        player = new Player { Id = Guid.NewGuid(), StatsBombPlayerId = statsBombPlayerId, Name = name };
        _context.Players.Add(player);
        return player;
    }

    private async Task UpsertSeasonStatsAsync(
        Guid playerId, Guid clubId, int competitionId, int seasonId, PlayerAggregateStats stats, CancellationToken ct)
    {
        var seasonStats = await _context.PlayerSeasonStats.FirstOrDefaultAsync(
            s => s.PlayerId == playerId && s.ClubId == clubId && s.CompetitionId == competitionId && s.SeasonId == seasonId,
            ct);

        if (seasonStats is null)
        {
            seasonStats = new PlayerSeasonStats
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ClubId = clubId,
                CompetitionId = competitionId,
                SeasonId = seasonId
            };
            _context.PlayerSeasonStats.Add(seasonStats);
        }

        seasonStats.MatchesPlayed += 1;
        seasonStats.MinutesPlayed += (int)stats.MinutesPlayed;
        seasonStats.PassesCompleted += stats.PassesCompleted;
        seasonStats.PassesAttempted += stats.PassesAttempted;
        seasonStats.ExpectedGoals += stats.TotalXg;
        seasonStats.ExpectedAssists += stats.TotalXa;
        seasonStats.Pressures += stats.Pressures;
        seasonStats.LastUpdatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 2: Run the ingestion tests — both should now pass**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter StatsBombIngestionServiceTests`
Expected: PASS — 2 tests

- [ ] **Step 3: Run the full test suite to confirm no regressions**

Run: `dotnet test`
Expected: all tests PASS

- [ ] **Step 4: Commit**

```powershell
git add src/FootballIQ.Infrastructure/StatsBomb/StatsBombIngestionService.cs
git commit -m "[2.5] Implement idempotent StatsBombIngestionService"
```

---

## Self-Review

**Spec coverage:** "Done when: Running twice → identical row counts, no duplicates" → covered by `IngestSeasonAsync_RunTwice_ProducesIdenticalRowCounts`. "IngestionLog table" → Task 2. Accumulation correctness (the reason for Task 1's refactor) → covered by `IngestSeasonAsync_AccumulatesSeasonStatsAcrossMatches`.

**Out of scope, explicitly:** Goals/Assists computation (no aggregator support yet), wiring to an HTTP endpoint (that's Task 2.6), `DataIngestionBackgroundService` (also 2.6 territory per the planned folder structure).

**Type consistency check:** `PlayerAggregateStats` fields used in `StatsBombIngestionService` (`PassesCompleted`, `PassesAttempted`, `Pressures`, `MinutesPlayed`, `TotalXg`, `TotalXa`) match exactly what Task 1 adds. `IStatsBombIngestionService.IngestSeasonAsync` signature matches the implementation in Task 5.
