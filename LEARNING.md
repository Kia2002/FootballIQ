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