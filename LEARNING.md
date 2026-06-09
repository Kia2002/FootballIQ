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