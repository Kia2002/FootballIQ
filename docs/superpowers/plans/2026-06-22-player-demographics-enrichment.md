# Player Demographic Enrichment (Task 2.8) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Populate `Player.DateOfBirth` for the majority of ingested players using Wikidata, disambiguating by name + club so we never write a wrong birth date.

**Architecture:** A pure matching function (`PlayerDemographicsMatcher`) decides if a set of Wikidata candidates confidently identifies one player, given the club we already know them by. An `IWikidataClient` (Infrastructure-internal, mirrors `IStatsBombReader`) hides the real two-step Wikidata lookup (fuzzy name search → batched fact fetch by ID) behind one method. `WikidataEnrichmentService` (implements Application-level `IPlayerEnrichmentService`, mirrors `IStatsBombIngestionService`) orchestrates: load null-DOB players → batch their names → call the client → run the matcher → save confident results. A `Channel<T>` + `BackgroundService` pair (copy of 2.6's ingestion queue pattern) lets `POST /api/admin/enrich-players` return `202 Accepted` instead of blocking.

**Tech Stack:** .NET 9, xUnit, Testcontainers.PostgreSql (already in the solution), `System.Text.Json`, `System.Net.Http`.

**Reference:** Design doc at `docs/superpowers/specs/2026-06-22-player-demographics-enrichment-design.md`.

---

## Task 1: `PlayerDemographicsMatcher` (pure matching logic)

**Files:**
- Create: `src/FootballIQ.Infrastructure/Enrichment/Models/WikidataPersonResult.cs`
- Create: `src/FootballIQ.Infrastructure/Enrichment/PlayerDemographicsMatcher.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/Enrichment/PlayerDemographicsMatcherTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FootballIQ.Infrastructure.Enrichment;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

public class PlayerDemographicsMatcherTests
{
    [Fact]
    public void FindConfidentBirthDate_WithOneCandidateMatchingClub_ReturnsBirthDate()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1987, 6, 24), Clubs = ["FC Barcelona"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Equal(new DateTime(1987, 6, 24), result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithNoCandidates_ReturnsNull()
    {
        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", []);

        Assert.Null(result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithNoCandidateMatchingClub_ReturnsNull()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1990, 1, 1), Clubs = ["Manchester United"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Null(result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithTwoCandidatesBothMatchingClub_ReturnsNull()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1990, 1, 1), Clubs = ["FC Barcelona"] },
            new() { BirthDate = new DateTime(1995, 5, 5), Clubs = ["FC Barcelona"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Null(result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithAccentedClubName_StillMatches()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1992, 3, 3), Clubs = ["Deportivo Alaves"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Deportivo Alavés", candidates);

        Assert.Equal(new DateTime(1992, 3, 3), result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter "FullyQualifiedName~PlayerDemographicsMatcherTests"`
Expected: FAIL with `CS0246: The type or namespace name 'PlayerDemographicsMatcher' could not be found` (and `WikidataPersonResult` not found) — the types don't exist yet.

- [ ] **Step 3: Write the model**

```csharp
namespace FootballIQ.Infrastructure.Enrichment.Models;

/// <summary>One Wikidata candidate match for a player name search: a birth date and the clubs they're recorded as having played for.</summary>
public record WikidataPersonResult
{
    public DateTime BirthDate { get; init; }
    public List<string> Clubs { get; init; } = [];
}
```

- [ ] **Step 4: Write the matcher**

```csharp
using System.Globalization;
using System.Text;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Decides whether a set of Wikidata candidates confidently identifies one player, using the club we already know them by as the disambiguator. Never guesses: zero or multiple club-matching candidates both return null.</summary>
public static class PlayerDemographicsMatcher
{
    public static DateTime? FindConfidentBirthDate(string targetClubName, IReadOnlyList<WikidataPersonResult> candidates)
    {
        var normalizedTarget = Normalize(targetClubName);

        var matches = candidates
            .Where(candidate => candidate.Clubs.Any(club => ClubsMatch(normalizedTarget, club)))
            .ToList();

        return matches.Count == 1 ? matches[0].BirthDate : null;
    }

    private static bool ClubsMatch(string normalizedTarget, string candidateClub)
    {
        var normalizedCandidate = Normalize(candidateClub);
        return normalizedCandidate.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedCandidate);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter "FullyQualifiedName~PlayerDemographicsMatcherTests"`
Expected: PASS, 5/5 tests green.

- [ ] **Step 6: Commit**

```bash
git add src/FootballIQ.Infrastructure/Enrichment/Models/WikidataPersonResult.cs src/FootballIQ.Infrastructure/Enrichment/PlayerDemographicsMatcher.cs tests/FootballIQ.Infrastructure.Tests/Enrichment/PlayerDemographicsMatcherTests.cs
git commit -m "[2.8] Add name+club matcher for Wikidata player demographic candidates"
```

---

## Task 2: `IWikidataClient` interface + `FakeWikidataClient` test double

**Files:**
- Create: `src/FootballIQ.Infrastructure/Enrichment/IWikidataClient.cs`
- Create: `tests/FootballIQ.Infrastructure.Tests/Enrichment/FakeWikidataClient.cs`

This task has no test of its own — it's the seam that lets Task 4's orchestration tests run without the real network. Built before the real client (Task 3) so Task 4 can be developed and tested against the fake first.

- [ ] **Step 1: Write the interface**

```csharp
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Looks up Wikidata candidates for a batch of player names. Infrastructure-internal, like IStatsBombReader — only WikidataEnrichmentService depends on this.</summary>
public interface IWikidataClient
{
    Task<Dictionary<string, List<WikidataPersonResult>>> SearchByNamesAsync(IReadOnlyList<string> names, CancellationToken ct);
}
```

- [ ] **Step 2: Write the fake**

```csharp
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

/// <summary>Returns fixed, hand-built Wikidata results so enrichment tests don't depend on the real network.</summary>
public class FakeWikidataClient : IWikidataClient
{
    private readonly Dictionary<string, List<WikidataPersonResult>> _resultsByName;

    public FakeWikidataClient(Dictionary<string, List<WikidataPersonResult>> resultsByName)
    {
        _resultsByName = resultsByName;
    }

    public Task<Dictionary<string, List<WikidataPersonResult>>> SearchByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
    {
        var result = names
            .Where(_resultsByName.ContainsKey)
            .ToDictionary(name => name, name => _resultsByName[name]);

        return Task.FromResult(result);
    }
}
```

- [ ] **Step 3: Build to confirm it compiles**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add src/FootballIQ.Infrastructure/Enrichment/IWikidataClient.cs tests/FootballIQ.Infrastructure.Tests/Enrichment/FakeWikidataClient.cs
git commit -m "[2.8] Add IWikidataClient interface and fake test double"
```

---

## Task 3: `IPlayerEnrichmentService` + `WikidataEnrichmentService` orchestration

**Files:**
- Create: `src/FootballIQ.Application/Interfaces/IPlayerEnrichmentService.cs`
- Create: `src/FootballIQ.Infrastructure/Enrichment/WikidataEnrichmentService.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/Enrichment/WikidataEnrichmentServiceTests.cs`

This is the orchestration: load players with no birth date, batch their names (50 per batch — Wikidata search-API etiquette), ask the client, run each player's candidates through the matcher using their club, save confident hits. Testcontainers (real Postgres) + `FakeWikidataClient`, same pattern as `StatsBombIngestionServiceTests`.

- [ ] **Step 1: Write the failing test**

```csharp
using FootballIQ.Domain.Entities;
using FootballIQ.Infrastructure.Enrichment;
using FootballIQ.Infrastructure.Enrichment.Models;
using FootballIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

public class WikidataEnrichmentServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private FootballIQDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<FootballIQDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new FootballIQDbContext(options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<(Guid playerId, Guid clubId)> SeedPlayerWithClubAsync(string playerName, string clubName)
    {
        var club = new Club { Id = Guid.NewGuid(), StatsBombTeamId = new Random().Next(), Name = clubName };
        var player = new Player { Id = Guid.NewGuid(), StatsBombPlayerId = new Random().Next(), Name = playerName };
        var stats = new PlayerSeasonStats
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            ClubId = club.Id,
            CompetitionId = 11,
            SeasonId = 90
        };

        _context.Clubs.Add(club);
        _context.Players.Add(player);
        _context.PlayerSeasonStats.Add(stats);
        await _context.SaveChangesAsync(CancellationToken.None);

        return (player.Id, club.Id);
    }

    [Fact]
    public async Task EnrichPlayerDemographicsAsync_WithConfidentMatch_SetsDateOfBirth()
    {
        var (playerId, _) = await SeedPlayerWithClubAsync("Lionel Messi", "FC Barcelona");

        var fakeClient = new FakeWikidataClient(new Dictionary<string, List<WikidataPersonResult>>
        {
            ["Lionel Messi"] = [new() { BirthDate = new DateTime(1987, 6, 24), Clubs = ["FC Barcelona"] }]
        });

        var service = new WikidataEnrichmentService(fakeClient, _context);
        var updatedCount = await service.EnrichPlayerDemographicsAsync(CancellationToken.None);

        Assert.Equal(1, updatedCount);

        var player = await _context.Players.SingleAsync(p => p.Id == playerId);
        Assert.Equal(new DateTime(1987, 6, 24), player.DateOfBirth);
    }

    [Fact]
    public async Task EnrichPlayerDemographicsAsync_WithNoMatch_LeavesDateOfBirthNull()
    {
        var (playerId, _) = await SeedPlayerWithClubAsync("Unknown Player", "FC Barcelona");

        var fakeClient = new FakeWikidataClient(new Dictionary<string, List<WikidataPersonResult>>());

        var service = new WikidataEnrichmentService(fakeClient, _context);
        var updatedCount = await service.EnrichPlayerDemographicsAsync(CancellationToken.None);

        Assert.Equal(0, updatedCount);

        var player = await _context.Players.SingleAsync(p => p.Id == playerId);
        Assert.Null(player.DateOfBirth);
    }

    [Fact]
    public async Task EnrichPlayerDemographicsAsync_OnPlayerAlreadyHavingDateOfBirth_SkipsThatPlayer()
    {
        var (playerId, _) = await SeedPlayerWithClubAsync("Already Known", "FC Barcelona");
        var existingPlayer = await _context.Players.SingleAsync(p => p.Id == playerId);
        existingPlayer.DateOfBirth = new DateTime(2000, 1, 1);
        await _context.SaveChangesAsync(CancellationToken.None);

        var fakeClient = new FakeWikidataClient(new Dictionary<string, List<WikidataPersonResult>>
        {
            ["Already Known"] = [new() { BirthDate = new DateTime(1999, 9, 9), Clubs = ["FC Barcelona"] }]
        });

        var service = new WikidataEnrichmentService(fakeClient, _context);
        var updatedCount = await service.EnrichPlayerDemographicsAsync(CancellationToken.None);

        Assert.Equal(0, updatedCount);

        var player = await _context.Players.SingleAsync(p => p.Id == playerId);
        Assert.Equal(new DateTime(2000, 1, 1), player.DateOfBirth);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter "FullyQualifiedName~WikidataEnrichmentServiceTests"`
Expected: FAIL with `CS0246: The type or namespace name 'WikidataEnrichmentService' could not be found`.

- [ ] **Step 3: Write the Application interface**

```csharp
namespace FootballIQ.Application.Interfaces;

/// <summary>Enriches players who are missing demographic data (currently: DateOfBirth) from an external source.</summary>
public interface IPlayerEnrichmentService
{
    /// <summary>Attempts to fill in DateOfBirth for every player that doesn't have one yet. Returns the number of players updated.</summary>
    Task<int> EnrichPlayerDemographicsAsync(CancellationToken ct);
}
```

- [ ] **Step 4: Write the orchestration service**

```csharp
using FootballIQ.Application.Interfaces;
using FootballIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Fills in Player.DateOfBirth using Wikidata, batching name lookups and matching by player name + their known club to avoid guessing on ambiguous names.</summary>
public class WikidataEnrichmentService : IPlayerEnrichmentService
{
    private const int BatchSize = 50;

    private readonly IWikidataClient _client;
    private readonly FootballIQDbContext _context;

    public WikidataEnrichmentService(IWikidataClient client, FootballIQDbContext context)
    {
        _client = client;
        _context = context;
    }

    public async Task<int> EnrichPlayerDemographicsAsync(CancellationToken ct)
    {
        var playersNeedingEnrichment = await _context.Players
            .Where(p => p.DateOfBirth == null)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var clubNameByPlayerId = await GetClubNamesByPlayerIdAsync(playersNeedingEnrichment.Select(p => p.Id), ct);

        var updatedCount = 0;

        foreach (var batch in playersNeedingEnrichment.Chunk(BatchSize))
        {
            var names = batch.Select(p => p.Name).Distinct().ToList();
            var candidatesByName = await _client.SearchByNamesAsync(names, ct);

            foreach (var playerInfo in batch)
            {
                if (!clubNameByPlayerId.TryGetValue(playerInfo.Id, out var clubName))
                {
                    continue;
                }

                if (!candidatesByName.TryGetValue(playerInfo.Name, out var candidates))
                {
                    continue;
                }

                var birthDate = PlayerDemographicsMatcher.FindConfidentBirthDate(clubName, candidates);
                if (birthDate is null)
                {
                    continue;
                }

                var player = await _context.Players.SingleAsync(p => p.Id == playerInfo.Id, ct);
                player.DateOfBirth = birthDate;
                updatedCount++;
            }

            await _context.SaveChangesAsync(ct);
        }

        return updatedCount;
    }

    private async Task<Dictionary<Guid, string>> GetClubNamesByPlayerIdAsync(IEnumerable<Guid> playerIds, CancellationToken ct)
    {
        var idList = playerIds.ToList();

        var rows = await _context.PlayerSeasonStats
            .Where(s => idList.Contains(s.PlayerId))
            .Select(s => new { s.PlayerId, s.Club.Name })
            .ToListAsync(ct);

        // A player can have stats rows for more than one club (transfers, multiple seasons).
        // We only need one club to disambiguate against, so take the first and group instead
        // of Distinct() - Distinct() would throw a duplicate-key error in ToDictionaryAsync
        // for any player with more than one (PlayerId, ClubName) pair.
        return rows
            .GroupBy(x => x.PlayerId)
            .ToDictionary(g => g.Key, g => g.First().Name);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter "FullyQualifiedName~WikidataEnrichmentServiceTests"`
Expected: PASS, 3/3 tests green.

- [ ] **Step 6: Commit**

```bash
git add src/FootballIQ.Application/Interfaces/IPlayerEnrichmentService.cs src/FootballIQ.Infrastructure/Enrichment/WikidataEnrichmentService.cs tests/FootballIQ.Infrastructure.Tests/Enrichment/WikidataEnrichmentServiceTests.cs
git commit -m "[2.8] Add WikidataEnrichmentService orchestration with Testcontainers tests"
```

---

## Task 4: Real `WikidataClient`

**Files:**
- Create: `src/FootballIQ.Infrastructure/Enrichment/WikidataClient.cs`
- Test: `tests/FootballIQ.Infrastructure.Tests/Enrichment/WikidataClientTests.cs`

This is the only piece that talks to the real network. Two real Wikidata calls happen inside `SearchByNamesAsync`, hidden behind the one interface method: (a) `https://www.wikidata.org/w/api.php?action=wbsearchentities` per name, to fuzzy-match a name to candidate entity IDs (QIDs); (b) one SPARQL query (`https://query.wikidata.org/sparql`) batching all collected QIDs with a `VALUES` clause, fetching `P569` (date of birth) and `P54` (member of sports team) for each.

Because this hits the real network, its test is marked to skip in normal CI runs and is run manually during development to sanity-check coverage — consistent with the design doc's "no automated test hits the real endpoint" rule.

- [ ] **Step 1: Write the (network-dependent, manually-run) test**

```csharp
using FootballIQ.Infrastructure.Enrichment;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

public class WikidataClientTests
{
    [Fact(Skip = "Hits the real Wikidata network — run manually to sanity-check coverage, not part of CI.")]
    public async Task SearchByNamesAsync_WithRealMessiName_FindsBarcelonaAndCorrectBirthDate()
    {
        var client = new WikidataClient(new HttpClient());

        var results = await client.SearchByNamesAsync(["Lionel Messi"], CancellationToken.None);

        Assert.True(results.ContainsKey("Lionel Messi"));
        var candidates = results["Lionel Messi"];
        Assert.Contains(candidates, c => c.Clubs.Any(club => club.Contains("Barcelona")) && c.BirthDate.Year == 1987);
    }
}
```

- [ ] **Step 2: Confirm the test is skipped, not run, by default**

Run: `dotnet test tests/FootballIQ.Infrastructure.Tests --filter "FullyQualifiedName~WikidataClientTests"`
Expected: `Skipped: 1` (no network call happens, no failure either — `WikidataClient` doesn't exist yet so this also won't compile until Step 3).

- [ ] **Step 3: Write the real client**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Looks up player birth dates and club history on Wikidata: a fuzzy name search per name to find candidate entity IDs, then one batched SPARQL query to fetch facts for all of them at once.</summary>
public class WikidataClient : IWikidataClient
{
    private readonly HttpClient _httpClient;

    public WikidataClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Dictionary<string, List<WikidataPersonResult>>> SearchByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
    {
        var entityIdsByName = new Dictionary<string, List<string>>();

        foreach (var name in names)
        {
            entityIdsByName[name] = await SearchEntityIdsAsync(name, ct);
        }

        var allEntityIds = entityIdsByName.Values.SelectMany(ids => ids).Distinct().ToList();
        if (allEntityIds.Count == 0)
        {
            return new Dictionary<string, List<WikidataPersonResult>>();
        }

        var factsByEntityId = await FetchFactsAsync(allEntityIds, ct);

        return entityIdsByName.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Where(factsByEntityId.ContainsKey).Select(id => factsByEntityId[id]).ToList());
    }

    private async Task<List<string>> SearchEntityIdsAsync(string name, CancellationToken ct)
    {
        var url = $"https://www.wikidata.org/w/api.php?action=wbsearchentities&search={Uri.EscapeDataString(name)}&language=en&type=item&format=json&limit=5";
        var response = await _httpClient.GetFromJsonAsync<WikidataSearchResponse>(url, ct);

        return response?.Search.Select(r => r.Id).ToList() ?? [];
    }

    private async Task<Dictionary<string, WikidataPersonResult>> FetchFactsAsync(List<string> entityIds, CancellationToken ct)
    {
        var valuesClause = string.Join(" ", entityIds.Select(id => $"wd:{id}"));
        var query = $@"
            SELECT ?entity ?birthDate ?clubLabel WHERE {{
              VALUES ?entity {{ {valuesClause} }}
              ?entity wdt:P569 ?birthDate.
              ?entity wdt:P54 ?club.
              ?club rdfs:label ?clubLabel.
              FILTER(LANG(?clubLabel) = 'en')
            }}";

        var url = $"https://query.wikidata.org/sparql?query={Uri.EscapeDataString(query)}&format=json";
        var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, ct);

        var results = new Dictionary<string, WikidataPersonResult>();

        foreach (var binding in response.GetProperty("results").GetProperty("bindings").EnumerateArray())
        {
            var entityUri = binding.GetProperty("entity").GetProperty("value").GetString()!;
            var entityId = entityUri.Split('/').Last();
            var birthDate = DateTime.Parse(binding.GetProperty("birthDate").GetProperty("value").GetString()!);
            var clubLabel = binding.GetProperty("clubLabel").GetProperty("value").GetString()!;

            if (!results.TryGetValue(entityId, out var existing))
            {
                existing = new WikidataPersonResult { BirthDate = birthDate, Clubs = [] };
                results[entityId] = existing;
            }

            existing.Clubs.Add(clubLabel);
        }

        return results;
    }

    private record WikidataSearchResponse
    {
        public List<WikidataSearchResult> Search { get; init; } = [];
    }

    private record WikidataSearchResult
    {
        public string Id { get; init; } = string.Empty;
    }
}
```

- [ ] **Step 4: Build to confirm it compiles**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 5: Manually run the skipped test once to sanity-check real coverage**

Temporarily remove `Skip = "..."` from the `[Fact]` attribute, run:
`dotnet test tests/FootballIQ.Infrastructure.Tests --filter "FullyQualifiedName~WikidataClientTests"`
Expected: PASS (confirms real Wikidata data shape matches our parsing). Then put the `Skip` attribute back before committing — this test must not run in CI.

- [ ] **Step 6: Commit**

```bash
git add src/FootballIQ.Infrastructure/Enrichment/WikidataClient.cs tests/FootballIQ.Infrastructure.Tests/Enrichment/WikidataClientTests.cs
git commit -m "[2.8] Add real WikidataClient using search API + batched SPARQL fact lookup"
```

---

## Task 5: Background queue + `POST /api/admin/enrich-players` endpoint

**Files:**
- Create: `src/FootballIQ.Application/Interfaces/IEnrichmentQueue.cs`
- Create: `src/FootballIQ.Infrastructure/BackgroundServices/EnrichmentWorkQueue.cs`
- Create: `src/FootballIQ.Infrastructure/BackgroundServices/PlayerDemographicsBackgroundService.cs`
- Modify: `src/FootballIQ.WebAPI/Endpoints/AdminEndpoints.cs`
- Modify: `src/FootballIQ.WebAPI/Program.cs`

This task copies the existing `IIngestionQueue`/`IngestionWorkQueue`/`DataIngestionBackgroundService` pattern from Task 2.6. Read those three files first (they already exist in the codebase) to match their exact shape before writing these.

- [ ] **Step 1: Read the existing pattern**

Open and read:
- `src/FootballIQ.Application/Interfaces/IIngestionQueue.cs`
- `src/FootballIQ.Infrastructure/BackgroundServices/IngestionWorkQueue.cs`
- `src/FootballIQ.Infrastructure/BackgroundServices/DataIngestionBackgroundService.cs`
- `src/FootballIQ.WebAPI/Endpoints/AdminEndpoints.cs`
- `src/FootballIQ.WebAPI/Program.cs` (the DI registrations for the above)

- [ ] **Step 2: Write `IEnrichmentQueue`**

```csharp
namespace FootballIQ.Application.Interfaces;

/// <summary>Queue of pending enrichment jobs, dequeued by PlayerDemographicsBackgroundService. Mirrors IIngestionQueue.</summary>
public interface IEnrichmentQueue
{
    Task EnqueueAsync(CancellationToken ct);
    Task WaitForJobAsync(CancellationToken ct);
}
```

- [ ] **Step 3: Write `EnrichmentWorkQueue`**

```csharp
using System.Threading.Channels;
using FootballIQ.Application.Interfaces;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>Channel-backed implementation of IEnrichmentQueue. Mirrors IngestionWorkQueue.</summary>
public class EnrichmentWorkQueue : IEnrichmentQueue
{
    private readonly Channel<bool> _channel = Channel.CreateUnbounded<bool>();

    public async Task EnqueueAsync(CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(true, ct);
    }

    public async Task WaitForJobAsync(CancellationToken ct)
    {
        await _channel.Reader.ReadAsync(ct);
    }
}
```

- [ ] **Step 4: Write `PlayerDemographicsBackgroundService`**

```csharp
using FootballIQ.Application.Interfaces;
using Microsoft.Extensions.Hosting;

namespace FootballIQ.Infrastructure.BackgroundServices;

/// <summary>Dequeues enrichment jobs and runs IPlayerEnrichmentService in a fresh DI scope per job. Mirrors DataIngestionBackgroundService.</summary>
public class PlayerDemographicsBackgroundService : BackgroundService
{
    private readonly IEnrichmentQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public PlayerDemographicsBackgroundService(IEnrichmentQueue queue, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _queue.WaitForJobAsync(stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var enrichmentService = scope.ServiceProvider.GetRequiredService<IPlayerEnrichmentService>();
            await enrichmentService.EnrichPlayerDemographicsAsync(stoppingToken);
        }
    }
}
```

(Add `using Microsoft.Extensions.DependencyInjection;` if `GetRequiredService`/`IServiceScopeFactory` aren't already in scope via the namespaces above — match whatever `DataIngestionBackgroundService.cs` already does for its usings.)

- [ ] **Step 5: Add the endpoint to `AdminEndpoints.cs`**

Add alongside the existing `/api/admin/ingest` mapping, following its exact pattern:

```csharp
app.MapPost("/api/admin/enrich-players", async (IEnrichmentQueue queue, CancellationToken ct) =>
{
    await queue.EnqueueAsync(ct);
    return Results.Accepted();
});
```

- [ ] **Step 6: Register the new types in `Program.cs`**

Add registrations matching however `IIngestionQueue`/`IngestionWorkQueue`/`DataIngestionBackgroundService` and `IStatsBombIngestionService`/`StatsBombIngestionService` are currently registered:

```csharp
builder.Services.AddSingleton<IEnrichmentQueue, EnrichmentWorkQueue>();
builder.Services.AddHostedService<PlayerDemographicsBackgroundService>();
builder.Services.AddScoped<IPlayerEnrichmentService, WikidataEnrichmentService>();
builder.Services.AddHttpClient<IWikidataClient, WikidataClient>();
```

- [ ] **Step 7: Build and run all tests**

Run: `dotnet build && dotnet test`
Expected: `Build succeeded`, all tests pass (the real `WikidataClientTests` test stays skipped).

- [ ] **Step 8: Manual verification against the real local database**

1. Ensure `docker compose up -d` is running and La Liga 2020/21 is already ingested (from Task 2.6/2.7's verification).
2. Run the API: `dotnet run --project src/FootballIQ.WebAPI`
3. `POST http://localhost:5000/api/admin/enrich-players` via Swagger — expect `202 Accepted` immediately.
4. Wait a few seconds, then `GET http://localhost:5000/api/players` — confirm at least some players now have a non-null `dateOfBirth`.

- [ ] **Step 9: Commit**

```bash
git add src/FootballIQ.Application/Interfaces/IEnrichmentQueue.cs src/FootballIQ.Infrastructure/BackgroundServices/EnrichmentWorkQueue.cs src/FootballIQ.Infrastructure/BackgroundServices/PlayerDemographicsBackgroundService.cs src/FootballIQ.WebAPI/Endpoints/AdminEndpoints.cs src/FootballIQ.WebAPI/Program.cs
git commit -m "[2.8] Add POST /api/admin/enrich-players with background enrichment queue"
```

---

## Task 6: Update `domain-model.md`

**Files:**
- Modify: `docs/architecture/domain-model.md`

- [ ] **Step 1: Replace the "StatsBomb data-availability caveat" section**

Find the section starting `## StatsBomb data-availability caveat` (currently says these columns "will remain null... until a future enrichment source is added"). Replace its content to document the chosen source and the matching rule:

```markdown
## Player demographic enrichment (Task 2.8)

`Player.DateOfBirth` is populated by a separate enrichment step (`POST /api/admin/enrich-players`), run after StatsBomb ingestion. StatsBomb's open data doesn't include birth dates, so this queries **Wikidata**: a fuzzy name search finds candidate people, then a batched SPARQL query fetches their birth date and club history.

**Matching rule — disambiguate by name + club, never guess:** a candidate is only accepted if the club we already know the player by (via `PlayerSeasonStats` → `Club`) matches one of their Wikidata-recorded clubs. If a name search returns zero or multiple club-matching candidates, `DateOfBirth` is left `null` rather than guessed — a wrong birth date is worse than a missing one, since age feeds scouting queries directly. See `PlayerDemographicsMatcher` for the matching logic.

`Player.PreferredFoot` remains unpopulated — out of scope for this task; revisit if a future source provides reliable footedness data.

This step is idempotent by construction: it only ever targets players where `DateOfBirth IS NULL`, so re-running it just covers whoever's left from the previous run.
```

- [ ] **Step 2: Update the "Open items" line**

Find: `- **`DateOfBirth` / `PreferredFoot` enrichment — tracked as Task 2.8.**` and its full paragraph. Replace with:

```markdown
- **`PreferredFoot` enrichment** — not solved by Task 2.8 (which only tackled `DateOfBirth`). Revisit if a reliable data source for footedness is found.
```

- [ ] **Step 3: Commit**

```bash
git add docs/architecture/domain-model.md
git commit -m "[2.8] Document Wikidata enrichment source and matching rule in domain model"
```
