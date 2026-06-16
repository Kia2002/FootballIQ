# FootballIQ Scout — Learning Log

This is my personal study notes while building FootballIQ Scout.
Updated at the end of every session. Written in plain language — no jargon.

---

## Task 1.1: Solution Scaffold
**What we built:** A .NET solution file with 8 projects — 4 source (Domain, Application, Infrastructure, WebAPI) and 4 matching test projects. Each project is connected to the others following the Clean Architecture dependency rule.

**Key concept:** Clean Architecture — inner layers (Domain, Application) don't know about outer layers (Infrastructure, WebAPI). Dependencies only flow inward. This means the business logic is completely independent of the database, HTTP framework, or any external library.

**Why it matters:** In production systems, you want to be able to swap your database from PostgreSQL to SQL Server without touching a line of business logic. You also want to unit-test your use cases without starting a real database. Clean Architecture makes both of these possible.

**Mistake I made:** None — but important to remember: if you ever try to reference `FootballIQ.Infrastructure` from `FootballIQ.Domain`, the compiler will stop you. That's the architecture enforcing itself.

Question: In our dependency graph, Application references Domain, but Domain does NOT reference Application. Why is this rule important? What would break if we allowed Domain to reference Application?

Take a moment to think about it — give me your best answer in your own words. There's no "wrong" attempt here, I just want to make sure the concept landed before we build on top of it.


❯ Okay so, we must not do that because all dependencies should go inward and domain layer containts domain layer rules and business logic that is crucial to our system and that is something that is rarely edited, application layer has all of the use cases and tells us what needds to happen so our system works, infrastructure is how it works, application orchestrates domain not the other way around

That's correct — and more importantly, you got the why, not just the rule.

The one thing to add: if Domain referenced Application, you'd create a circular dependency (Domain → Application → Domain). .NET won't even compile that. But beyond the compiler error, your most important, most stable layer would become dependent on a less stable one — every time a new use case gets added, Domain would have to change. Stability flows inward. You nailed the concept.

---

## Task 1.2: Docker Compose + PostgreSQL + pgvector
**What we built:** A `docker-compose.yml` that starts a PostgreSQL 16 database with the pgvector extension pre-installed. One command (`docker compose up -d`) spins up a fully configured, health-checked database container.

**Key concept:** Docker — software runs in a container, an isolated box that includes everything it needs. The container behaves identically on any machine regardless of what's installed on the host OS.

**Docker Compose** is the tool that reads `docker-compose.yml` and manages one or more containers together. Instead of typing a long `docker run` command with flags every time, you describe what you want in a file and run one command.

**Why it matters:** Without Docker, every developer on the team would have to manually install PostgreSQL, configure it, and hope it doesn't conflict with other things. With Docker Compose, anyone who clones the repo gets the exact same database — same version, same extensions, same config — in one command. This is standard practice in every real .NET backend team.

**Why pgvector is in the image from day one:** EF Core migrations are additive. Starting the database with pgvector available means we never have to worry about the extension existing when we add the vector column in Layer 3. It costs nothing to have it there unused.

---

## Task 1.3: EF Core + Npgsql + DbContext
**What we built:** Installed EF Core + the Npgsql PostgreSQL provider, created a `FootballIQDbContext`, wired up the connection string from configuration, and generated the first migration (`InitialCreate`).

**Key concept: EF Core (ORM)** — maps C# classes to database tables. Instead of writing raw SQL, you write LINQ queries against C# objects and EF Core generates the SQL. It also generates the database schema from your classes via **migrations**.

**Key concept: Npgsql** — the PostgreSQL driver/provider for .NET. EF Core is database-agnostic; Npgsql is the plug-in that speaks the PostgreSQL protocol. `Npgsql.EntityFrameworkCore.PostgreSQL` bundles both.

**Key concept: DbContext** — the central EF Core class. Every query and save goes through it. You subclass it and declare your tables as `DbSet<T>` properties.

**Key concept: Migrations** — auto-generated C# files that describe schema changes. `dotnet ef migrations add` generates them; `dotnet ef database update` applies them to the real database.

**Key concept: DesignTimeDbContextFactory** — a special class EF Core finds automatically when you run `dotnet ef` CLI commands. It provides a DbContext for tooling only (hard-coded local connection string), so migration commands don't need env vars set in your shell.

**Why it matters:** Every production .NET API talks to a database through EF Core. Understanding the DbContext, migrations, and the design-time factory is the foundation for everything in Layers 1–4.

**Mistake we hit:** `Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4` and `Microsoft.AspNetCore.OpenApi 9.0.16` pulled in different EF Core patch versions, causing an MSB3277 version conflict warning in the test project. Fixed by explicitly pinning `Microsoft.EntityFrameworkCore 9.0.4` in the WebAPI.Tests project to force NuGet to resolve to the higher version.

---

## Task 1.4: Domain Entities + EF Migration
**What we built:** Four entities — `Club`, `Player`, `PlayerSeasonStats`, `Match` — plus two enums (`Position`, `Foot`). Wired them into `FootballIQDbContext` via one configuration class per entity, generated a real migration (`AddCoreDomainEntities`), and applied it — the database now has `clubs`, `players`, `player_season_stats`, and `matches` tables.

**Key concept: One-to-many relationships in EF Core.** A `Player` has many `PlayerSeasonStats` rows (one per club/season), and so does a `Club`. In code this is `ICollection<PlayerSeasonStats>` on the "one" side and a foreign key (`PlayerId`/`ClubId`) plus a reference navigation on the "many" side. This is *why* `PlayerSeasonStats` is its own table instead of columns on `Player` — a player can have stats for several clubs/seasons, not just one.

**Key concept: `IEntityTypeConfiguration<T>` (configuration class per entity).** Instead of one giant `OnModelCreating` method, each entity gets its own configuration file (`ClubConfiguration.cs`, etc.) describing its table name, keys, indexes, and relationships. `modelBuilder.ApplyConfigurationsFromAssembly(...)` finds all of them automatically — adding entity #5 later means adding one new file, not editing existing ones.

**Key concept: Enum storage choice (`HasConversion<string>()`).** By default EF stores enums as integers. We chose to store `Position` and `Foot` as text instead — `SELECT position FROM players` shows `"LeftBack"` instead of `2`. Costs a little extra space, much easier to debug by hand.

**Key concept: Delete behaviors (`Cascade` vs `Restrict`).** `PlayerSeasonStats` rows cascade-delete with their `Player`/`Club` (a stats row is meaningless without them). `Match` uses `Restrict` on both its `Club` foreign keys (`HomeClubId`/`AwayClubId`) — matches are historical records, so Postgres refuses to delete a `Club` that's still referenced by a match.

**Gotcha worth remembering:** `Match` has *two* foreign keys to the same table (`Club`), with no inverse navigation collections on `Club`. EF Core disambiguates the two relationships purely by which FK property each `HasOne<Club>().WithMany().HasForeignKey(...)` call targets — no special syntax needed beyond specifying the FK explicitly.

**Why it matters:** Every later layer builds on this schema — ingestion (Layer 2) writes into these tables, search (Layer 3/4) reads from them. Getting relationships, unique indexes (idempotency keys for ingestion), and delete behavior right now avoids painful migrations later.

**Data caveat documented:** `Player.DateOfBirth` and `Player.PreferredFoot` are nullable because StatsBomb's open data doesn't provide them — they'll stay `null` until a future enrichment source is added. Full write-up in `docs/architecture/domain-model.md`.

### Comprehension Check
**Q:** We made `PlayerSeasonStats` its own table instead of adding columns like `Goals`, `MinutesPlayed`, etc. directly onto `Player`. In your own words — why was that the right call, and what would break if we'd put those stats columns directly on `Player` instead?

**My answer:** One player plays for multiple clubs and has different stats every season, also it changes all the time.

**Verdict:** Correct — that's the core reason (one-to-many relationship enables club/season history). Added detail: if stats were columns on `Player`, each player could only ever have *one* row of values — ingesting a new season's stats would overwrite the previous season's, destroying history instead of accumulating it.

### Follow-up question (asked by the student)
**Q:** In `Match`, why did we use `HomeClubId`/`AwayClubId` (plain `Guid`) instead of navigation properties `HomeClub`/`AwayClub` (type `Club`)?

**Answer given:** Both are valid EF Core designs — FK-only vs. FK + navigation property produce the same database schema/constraints. We chose FK-only for `Match` because nothing currently needs `match.HomeClub.Name` via EF `Include()` (ingestion already has the `Club` objects in memory when creating a `Match`). `PlayerSeasonStats` got `Player`/`Club` navigation properties because traversing "this player's clubs" is a core scouting use case. Navigation properties can be added to `Match` later with little/no migration impact — this wasn't a permanent constraint, just a "don't add what we don't need yet" choice.

---

## Task 1.5: IPlayerRepository + PlayerRepository
**What we built:** `PlayerNotFoundException` (Domain/Exceptions), the `IPlayerRepository` interface (Application/Interfaces) with five methods (`GetByIdAsync`, `GetAllAsync`, `GetByStatsBombIdAsync`, `AddAsync`, `SaveChangesAsync`), and `PlayerRepository` — its EF Core implementation in Infrastructure/Persistence. Registered it in `Program.cs` with `AddScoped<IPlayerRepository, PlayerRepository>()`.

**Key concept: Repository pattern + Dependency Inversion.** The *Application* layer defines `IPlayerRepository` — what it needs ("give me a player by ID"). The *Infrastructure* layer provides `PlayerRepository` — how it's actually done (EF Core + Postgres). Application never references EF Core. Later, query/command handlers depend only on the interface, so they can be unit-tested by mocking `IPlayerRepository` instead of needing a real database.

**Key concept: "throw vs nullable" as a deliberate convention, not an accident.** `GetByIdAsync` throws `PlayerNotFoundException` if the ID doesn't exist — the caller asked for a *specific known* player, so "missing" is an error (→ 404 later). `GetByStatsBombIdAsync` returns `Player?` — during ingestion, "this player hasn't been ingested yet" is a normal, expected outcome, not an error. Same "not found" situation, different meaning depending on what the caller is asking.

**Key concept: DI service lifetime — `AddScoped`.** `PlayerRepository` wraps `FootballIQDbContext`, which EF Core registers as *scoped* (one instance per HTTP request). The repository must share that lifetime — `AddScoped` means one `PlayerRepository` (and one `DbContext`) per request, so all repository calls within a single request share the same tracked entities and the same pending changes.

**Why it matters:** This is the seam that makes Clean Architecture real rather than theoretical. From here on, Application-layer code (query handlers, validators) talks to `IPlayerRepository` and has zero idea Postgres exists.

### Comprehension Check
**Q:** `IPlayerRepository` lives in `FootballIQ.Application/Interfaces/`, but `PlayerRepository` lives in `FootballIQ.Infrastructure/Persistence/`. If we instead put the interface in `Infrastructure` (next to its implementation), what would break later — and why does putting it in `Application` fix it?

**My answer:** In the application layer we define use cases and what our system needs, in infrastructure it's about how it's done. It's more flexible this way since we can swap databases without touching business logic, and it's testable — we can mock the data without needing a real database.

**Verdict:** Partially correct — the "why this design is good" reasoning (flexibility, testability) is right, but missed the concrete "what breaks" mechanism the question was after: a future query handler in `Application` needs `IPlayerRepository` as a constructor parameter type, which requires a project reference to wherever the interface lives. If that were `Infrastructure`, `Application` would need to reference `Infrastructure` — but `Infrastructure` already references `Application` (to implement the interface). Two projects referencing each other is a **circular dependency**, and .NET refuses to compile that — same rule from the Task 1.1 check, one layer further out. Putting the interface in `Application` keeps the reference graph one-directional.

---

## Task 1.6: football-data.org Typed HTTP Client + Polly Retry
**What we built:** `FootballDataClient` — a small wrapper around `HttpClient` that calls football-data.org's `/v4/competitions/{id}/matches` endpoint and deserializes the JSON into C# record DTOs (`CompetitionMatchesResponse`, `MatchDto`, `TeamDto`, `ScoreDto`, etc.). Registered it in `Program.cs` via `AddHttpClient<FootballDataClient>(...)`, configured with the base URL and `X-Auth-Token` header, chained with a Polly retry policy. Verified it with a real integration test that hits the live API.

**Key concept: Typed HTTP Client (`IHttpClientFactory`).** Instead of `new HttpClient()` everywhere (which can cause socket exhaustion under load), `AddHttpClient<FootballDataClient>(...)` registers `FootballDataClient` in DI and configures the `HttpClient` it receives — base address, default headers — all in one place. The class itself never hardcodes the URL or API key.

**Key concept: Polly retry policy (`AddTransientHttpErrorPolicy` + `WaitAndRetryAsync`).** Wraps every request from `FootballDataClient` with automatic retries — but *only* for **transient** failures (network exceptions, `5xx`, `408`). `WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))` retries up to 3 times with **exponential backoff** (2s, 4s, 8s) — spacing retries out so a struggling server gets room to recover instead of being hammered.

**Key concept: `JsonSerializerDefaults.Web` for DTO deserialization.** football-data.org returns camelCase JSON (`utcDate`, `homeTeam`); C# DTOs use PascalCase (`UtcDate`, `HomeTeam`). `JsonSerializerDefaults.Web` enables case-insensitive property matching, so no `[JsonPropertyName]` attributes are needed when the only difference is the first letter's case.

**Why it matters:** Every external API call in production needs both of these — connection pooling (typed clients) and resilience to transient blips (Polly). This is the standard .NET pattern for *any* outbound HTTP call, and we'll reuse the same shape later for other external services.

**Mistake I made:** Tried to set the env var for the test using PowerShell syntax (`$env:FOOTBALLDATA_API_KEY="..."`) inside a Git Bash command. Bash doesn't understand that syntax — it errored on the `$env:...` part, then ran `dotnet test` anyway (because `;` doesn't stop on a failed command), so the test ran *without* the env var set and passed vacuously via its early-return skip path. Correct Git Bash syntax for a one-off env var is `VAR=value command` (no `$`, no `;`).

### Comprehension Check
**Q:** Suppose football-data.org returns a `401 Unauthorized` (e.g. bad/expired API key). Would the Polly retry policy retry that request? Why or why not — and what would the developer actually see happen?

**My answer:** It wouldn't retry the request since we don't have authorization and if we did retry it wouldn't change a single thing. Not sure what we'd see — probably an error or exception.

**Verdict:** Correct on the "why" — `401` isn't transient (it's about the request itself, not a passing condition), so `AddTransientHttpErrorPolicy` (which only watches `5xx`/`408`/network exceptions) ignores it. Added detail on "what you'd see": `EnsureSuccessStatusCode()` throws `HttpRequestException` for any non-2xx status. With no error-handling middleware yet, an unhandled exception during a request becomes a raw `500 Internal Server Error` (full stack trace visible in Development mode) — this is exactly the "leaky error" problem that Task 6.2 (Problem Details middleware) will fix later.

---

## Task 1.7: GET /api/health + GET /api/players
**What we built:** Two endpoints. `GET /api/health` via ASP.NET Core's built-in Health Checks middleware (`AddHealthChecks().AddDbContextCheck<FootballIQDbContext>()` + `MapHealthChecks("/api/health")`) — checks real DB connectivity, not just a hardcoded "OK". `GET /api/players` via a new `Endpoints/PlayerEndpoints.cs` file (extension method `MapPlayerEndpoints`), calling `IPlayerRepository.GetAllAsync()` and mapping each `Player` to a new `PlayerDto`.

**Key concept: ASP.NET Core Health Checks.** `AddHealthChecks()` registers a system of pluggable checks; `AddDbContextCheck<T>()` adds one that runs a trivial query against the DB. `MapHealthChecks("/api/health")` exposes an endpoint returning `200 Healthy` or `503 Unhealthy` based on all registered checks — actually verifies the system works, not just that the process is alive. Needed an extra package (`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, pinned to `9.0.4` — the `10.0.9` latest only targets net10.0).

**Key concept: Endpoint extension methods.** Instead of growing `Program.cs` forever, each group of related routes lives in its own `static class` with an extension method on `IEndpointRouteBuilder` (e.g. `MapPlayerEndpoints`). `Program.cs` stays a short list of `app.MapXEndpoints()` calls as more layers add more routes.

**Key concept: DTO mapping at the API boundary (entity → DTO).** Created `PlayerDto` (Id, Name, Position, Nationality, DateOfBirth, PreferredFoot) and mapped `Player` → `PlayerDto` with `.Select(...)` before returning. Two reasons: (1) don't leak the DB schema as the API contract — consumers shouldn't break when internal entities change; (2) avoid circular-reference serialization crashes — `Player.SeasonStats` is a list of `PlayerSeasonStats`, and each `PlayerSeasonStats.Player` points back to the parent, so serializing `Player` directly with that collection populated would throw `JsonException: A possible object cycle was detected`. Not a problem *yet* (DB is empty, `GetAllAsync` doesn't `.Include()` the collection), but would become one the moment Layer 2 ingestion populates `SeasonStats`.

**Decision: deferred Swagger UI to Task 6.3.** `AddOpenApi()`/`MapOpenApi()` (the .NET 9 default) only serves the raw OpenAPI JSON at `/openapi/v1.json` — no interactive UI (Swashbuckle/Swagger UI isn't included by default anymore). Verified both endpoints return `200 OK` via curl instead. Also noted: `/api/health` doesn't appear in the OpenAPI document at all — `MapHealthChecks()` doesn't add OpenAPI metadata by default, and health endpoints are conventionally excluded from API docs anyway (they're for infra tooling, not API consumers).

**Why it matters:** Health checks are what load balancers/orchestrators poll to decide if your instance should receive traffic. The endpoint-extension-method pattern keeps the API host file readable as the project grows across 4+ layers. The entity/DTO separation is a foundational boundary that gets more important every time a new relationship is added to the domain model.

### Comprehension Check
**Q:** We mapped `Player` → `PlayerDto` inside the `/api/players` handler instead of returning `players` directly from `Results.Ok(players)`. In your own words, what are the two problems this avoids?

**My answer:** We choose what we want to show to the user — they don't need to see all the data we persist in the DB. (Second reason: didn't know.)

**Verdict:** First reason correct (control the API contract, decouple from DB schema). Second reason added: circular reference serialization crash — `Player.SeasonStats` → `PlayerSeasonStats.Player` → back to the same `Player` forms a cycle that `System.Text.Json` detects and throws on (`JsonException: A possible object cycle was detected`). `PlayerDto` has only scalar fields, so there's no graph to traverse. This isn't triggered yet (empty DB, no `.Include()`), but would break the moment Layer 2 ingestion populates `SeasonStats`.

---

## Task 1.8: Domain Unit Tests
**What we built:** Added a `PassAccuracy` computed property to `PlayerSeasonStats` and wrote 8 unit tests across 3 files (`PlayerNotFoundExceptionTests.cs`, `PlayerSeasonStatsTests.cs`, `PlayerTests.cs`). Deleted the placeholder `UnitTest1.cs`. All 8 tests pass.

**Key concept: Unit testing.** A unit test calls your real code with known inputs and checks that the output is what you expected — like a quality control machine. The word "unit" means one small thing in isolation: one class, one method, no database, no network.

**Key concept: xUnit test structure.** Every test method gets a `[Fact]` attribute (marks it for xUnit to find and run). Inside the method, the pattern is: *Arrange* (set up data), *Act* (call the thing), *Assert* (check the result). The `Assert` class provides helpers: `Assert.Equal`, `Assert.NotNull`, `Assert.Empty`, `Assert.Contains`, `Assert.ThrowsAsync`, etc.

**Key concept: Test naming convention (`Method_Scenario_Expected`).** When a test fails in CI you only see the name — not the code. `PassAccuracy_WhenNoPassesAttempted_ReturnsZero` tells you what broke, under what condition, and what was supposed to happen. `Test3` tells you nothing.

**Key concept: One test = one failure reason.** Each edge case gets its own test method. If you put two scenarios in one test and it fails, you don't know which scenario caused it. Separate tests mean a failure is always unambiguous — and each test name documents an intentional decision about the code's behaviour.

**Why `PassAccuracy` belongs in the domain:** It's a calculation derived entirely from domain data (`PassesCompleted / PassesAttempted`). The domain should own this — not a handler, not an endpoint. If a caller needs pass accuracy, the entity should expose it. The `> 0` guard is an edge case the domain is responsible for handling cleanly.

**Why it matters:** Unit tests are the safety net for the domain layer. They catch regressions — if someone accidentally deletes the division-by-zero guard in six months, the `PassAccuracy_WhenNoPassesAttempted_ReturnsZero` test will fail immediately and point directly at the bug.

### Comprehension Check
**Q:** In `Player_SeasonStats_DefaultsToEmptyCollection` we assert both `Assert.NotNull` and `Assert.Empty`. Why do we need both — and what different bug would each one catch?

**My answer:** It's not the same if the list is empty or null. If it's null it doesn't exist, if it's empty he didn't play that season maybe.

**Verdict:** Correct — the domain intuition ("didn't play that season") is right for the empty case. Sharpened for code: `Assert.NotNull` catches a coding bug — if `SeasonStats` were `null`, any code that loops over it would throw a `NullReferenceException` (a crash). The property is initialized to `new List<PlayerSeasonStats>()` specifically to prevent this; the test proves that initialization is still in place. `Assert.Empty` catches the wrong default state — a freshly created `Player` should have no seasons recorded yet; if it came pre-filled with data, something in the initialization logic is wrong. Two assertions, two completely different categories of bug.

---

## Task 1.9: Testcontainers Integration Test
**What we built:** Added `Testcontainers.PostgreSql` package to `FootballIQ.Infrastructure.Tests`, wrote `PlayerRepositoryTests.cs` with two tests: one that saves and retrieves a player (round-trip test), and one that confirms `PlayerNotFoundException` is thrown for a non-existent ID. Both tests run against a real PostgreSQL container that starts and stops automatically.

**Key concept: Integration test vs unit test.** Unit tests test isolated logic (no DB, no network). Integration tests verify that two or more components work *together* — in this case, `PlayerRepository` + EF Core + a real PostgreSQL database. The distinction matters because EF Core can silently behave differently against real Postgres vs. a fake (pgvector queries, tsvector, JSON operators all fail against the in-memory provider but pass compilation).

**Key concept: Testcontainers.** A library that starts a real Docker container (any image — PostgreSQL, Redis, etc.) from inside a test, runs the test against it, then stops and removes the container when the test ends. No manual setup needed, no shared dev database that can have leftover data from a previous run.

**Key concept: `IAsyncLifetime` (xUnit).** An interface with two async methods: `InitializeAsync` runs before the first test in the class, `DisposeAsync` runs after the last. This is how you do async setup/teardown — start the container and call `MigrateAsync()` in `InitializeAsync`, stop and dispose the container in `DisposeAsync`.

**Key concept: `MigrateAsync()`.** Applies all pending EF Core migrations to the fresh test database before the tests run. Without this, the tables don't exist and every test fails with a "relation does not exist" error.

**What Testcontainers does under the hood:** It starts a `ryuk` helper container first (a cleanup watchdog — if your test process crashes mid-run, ryuk removes any orphaned containers). Then it starts the PostgreSQL container, runs `pg_isready` on a loop until the database accepts connections, and signals that setup is complete. After `DisposeAsync`, the container is deleted immediately.

**Why it matters:** This is the industry standard for .NET integration tests. The alternative — mocking the database or using `UseInMemoryDatabase()` — lets tests pass while the real code is broken (a real incident pattern on production teams). Testcontainers gives you a real database with zero permanent infrastructure: every test run starts clean.

---

## Task 1.10: GitHub Repo + Actions CI
**What we built:** Created the GitHub repository at `github.com/Kia2002/FootballIQ`, cleaned Co-Authored-By lines from all existing commits using `git filter-branch`, and wrote `.github/workflows/ci.yml` — a GitHub Actions workflow that runs `dotnet restore`, `dotnet build`, and `dotnet test` automatically on every push. First CI run passed green in 50 seconds, including the Testcontainers tests running against real Docker PostgreSQL on GitHub's servers.

**Key concept: GitHub Actions.** A CI/CD system built into GitHub. You write a YAML file describing a *workflow*: what event triggers it (e.g. a push to any branch), what machine to run it on (`ubuntu-latest`), and what steps to run. GitHub executes it on their infrastructure — you don't configure any servers.

**Key concept: CI (Continuous Integration).** Every push triggers a full build and test run automatically. If something breaks, GitHub shows a red X on the commit before anyone can merge it. This is the safety net that makes a team confident when merging — broken code is caught immediately, not discovered days later in production.

**Key concept: `git filter-branch --msg-filter`.** Rewrites every commit's message by piping it through a shell command. We used a Perl one-liner to strip the `Co-Authored-By:` trailer from all commits before the first push, since rewriting history is safe when no remote exists yet. After a push, rewriting history requires a force-push and causes problems for anyone who already cloned — so this is a now-or-never operation.

**Why the workflow has these exact steps in order:** `restore` must happen before `build` (downloads packages); `build` must happen before `test` (`--no-build` skips recompiling in the test step, saving time). Running `--configuration Release` in both build and test ensures the code being tested matches what would be deployed.

**Why Testcontainers worked in CI without extra setup:** GitHub's `ubuntu-latest` runners have Docker pre-installed. Testcontainers just needs a Docker daemon — it doesn't matter whether it's local or on a CI VM. The PostgreSQL container starts, runs, and is cleaned up exactly the same way as on your local machine.