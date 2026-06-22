# Task 2.8: Player Demographic Enrichment — Design

## Goal

Populate `Player.DateOfBirth` (priority) and `Player.PreferredFoot` (best-effort, low priority) for the majority of ingested players. StatsBomb's open data has neither field.

## Decisions made

1. **DateOfBirth is the priority.** PreferredFoot can stay mostly null — most data sources have weak coverage for it anyway.
2. **Runs as a separate step, not inline during ingestion.** A new admin endpoint (`POST /api/admin/enrich-players`) triggered after StatsBomb ingestion finishes, not coupled to `StatsBombIngestionService`.
3. **Data source: Wikidata, queried via SPARQL.** Free, no API key, no meaningful rate limit at our volume, and stores club history as structured facts — which is what makes confident matching possible (see below). Rejected alternatives: football-data.org squads (only current rosters, misses transferred/retired 2020/21 players), curated CSV (doesn't scale to hundreds of players).
4. **Ambiguity rule: skip rather than guess.** If a name lookup returns zero or multiple plausible matches, leave `DateOfBirth` null. Wrong birth dates are worse than missing ones, since age feeds scouting queries directly.
5. **Disambiguate by name + club, not name alone.** We already know each player's club for a season via `PlayerSeasonStats` → `Club`. Two different real footballers essentially never share both an identical full name and the same club. Match logic: normalize (lowercase, strip diacritics) both our club name and each Wikidata candidate's club list, keep candidates with a substring match, and require exactly one survivor.
6. **Batch the Wikidata queries.** SPARQL's `VALUES` clause lets one query ask about ~50 player names at once, so ~383 players costs ~8 requests instead of 383.
7. **Idempotent by construction.** The query only ever targets players where `DateOfBirth IS NULL` — no ledger table needed, re-running just covers whoever's left.

## Components

| Layer | File | Role |
|---|---|---|
| Application | `Interfaces/IPlayerEnrichmentService.cs` | `Task<int> EnrichPlayerDemographicsAsync(ct)` |
| Infrastructure | `Enrichment/IWikidataClient.cs` (infra-internal, like `IStatsBombReader`) | `Task<List<WikidataPersonResult>> SearchByNamesAsync(names, ct)` |
| Infrastructure | `Enrichment/WikidataClient.cs` | Builds batched SPARQL query, POSTs to `query.wikidata.org/sparql` |
| Infrastructure | `Enrichment/PlayerDemographicsMatcher.cs` | Pure function: candidates + our club name → confident match or null |
| Infrastructure | `Enrichment/WikidataEnrichmentService.cs` | Orchestration: loads null-DOB players, batches names, calls matcher, saves |
| Infrastructure | `BackgroundServices/EnrichmentWorkQueue.cs` + `PlayerDemographicsBackgroundService.cs` | Same `Channel<T>` + `BackgroundService` pattern as 2.6's ingestion queue |
| WebAPI | `POST /api/admin/enrich-players` | Returns `202 Accepted`, mirrors `/api/admin/ingest` |

## Error handling

- One failed batch (network blip, Wikidata down) doesn't fail the whole run — caught per-batch, logged, continue to the next batch. Unenriched players are retryable on the next run.
- Only confident matcher results get written; one `SaveChangesAsync` per batch.

## Testing

- `PlayerDemographicsMatcherTests` — pure unit tests, bulk of coverage (accented club names, zero candidates, multiple ambiguous candidates, etc.)
- `WikidataEnrichmentServiceTests` — Testcontainers + `FakeWikidataClient`, proving orchestration end-to-end without real network calls
- No automated test hits the real Wikidata endpoint (flaky/slow externals don't belong in CI). One manual exploratory run during development to sanity-check real-world coverage, same spirit as 2.4's spot-checked stats.

## Build order (simplest, most isolated piece first)

1. `PlayerDemographicsMatcher` (pure function, no I/O) — build and test in isolation first
2. `IWikidataClient` + real `WikidataClient` (SPARQL query + JSON parsing)
3. `WikidataEnrichmentService` (orchestration) + `FakeWikidataClient` for its tests
4. Background queue + endpoint (copy-paste-adapt from 2.6's ingestion queue)
5. `docs/architecture/domain-model.md` update documenting the chosen source
