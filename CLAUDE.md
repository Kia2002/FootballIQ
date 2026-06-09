# FootballIQ Scout — Project CLAUDE.md

## How to Start Every Session

Read this file completely. Then open the session exactly like this:

> "We are in **Layer [N]: [Name]**. Last completed task: [task id + description].
> Up next: **Task [X.Y]** — [one-line description].
> This task will teach you: [concept or pattern].
> Here is what we will do today…"

Find the last ✅ in the Build Status section to determine current position. If no tasks are complete, start with Task 1.1.

---

## Project Overview

**FootballIQ Scout** is a production-quality ASP.NET Core 9 natural language player scouting API.

It answers football scouting queries like _"find a left-back under 23 with 85%+ pass accuracy and high pressing intensity"_ using a hybrid RAG pipeline over StatsBomb event data (3000+ real matches).

**The problem it solves:** Small clubs and amateur scouts cannot afford enterprise scouting tools. FootballIQ Scout makes player discovery accessible through natural language.

**What this demonstrates (portfolio goals):**
- Production .NET backend skills: Clean Architecture, EF Core, async patterns, proper testing
- Genuine AI engineering depth: hybrid search (BM25 + semantic + RRF), query transformation, RAG evaluation metrics
- A live deployed URL with a README containing real accuracy numbers

**Live URL:** _(add after Layer 6 deploy)_
**GitHub repo:** _(add after Layer 1 setup)_

---

## Tech Stack

| Component | Choice | Why this choice |
|-----------|--------|-----------------|
| Framework | ASP.NET Core 9 + Minimal APIs | Latest .NET, native OpenTelemetry, clean endpoint registration |
| ORM | EF Core 9 + Npgsql | Standard .NET ORM. Raw SQL for vector queries. |
| Database | PostgreSQL 16 + pgvector (HNSW index) | Single DB for relational + vector. No second service to manage. |
| AI Orchestration | Semantic Kernel 1.x | Microsoft's .NET AI library. Stable, well-documented. Abstracts LLM providers. |
| CQRS | MediatR | Each use case = one testable class. Clean separation from HTTP layer. |
| Validation | FluentValidation | Declarative validation. Integrates with MediatR pipeline. |
| Testing | xUnit + Testcontainers | xUnit = .NET standard. Testcontainers = real PostgreSQL, no fakes. |
| Football Data | StatsBomb Open Data | Free. 3000+ matches with event-level data (xG, xA, passes, pressure). |
| Live Data | football-data.org (free tier) | Current fixtures, standings. 10 req/min. |
| Local LLM | Ollama | Free local models during development. |
| Production LLM | Azure OpenAI or OpenAI API | Switched via env var. Negligible cost at demo scale. |
| Containers | Docker Compose | Local PostgreSQL + pgvector setup. |
| Deployment | Railway (~$5/month) | Simplest path to a live URL. Azure upgrade path in docs/. |
| CI/CD | GitHub Actions | Build + test on every PR. Deploy on merge to main. |

---

## Architecture

### Dependency Rule

```
Domain ← Application ← Infrastructure
         Application ← WebAPI
```

Inner layers (Domain, Application) never reference outer layers. This means:
- Domain has zero NuGet packages
- Application interfaces define what Infrastructure must implement
- WebAPI is a thin host — it wires DI and routes HTTP to MediatR

### Solution Structure

```
FootballIQ/
├── FootballIQ.sln
├── src/
│   ├── FootballIQ.Domain/
│   │   ├── Entities/           Player.cs, Match.cs, PlayerStats.cs
│   │   ├── ValueObjects/       Position.cs, Season.cs
│   │   └── Exceptions/         PlayerNotFoundException.cs
│   ├── FootballIQ.Application/
│   │   ├── Scouting/Queries/   ScoutPlayersQuery.cs, ScoutPlayersQueryHandler.cs
│   │   ├── Players/Queries/    GetPlayersQuery.cs, GetPlayersQueryHandler.cs
│   │   ├── Ingestion/Commands/ TriggerIngestionCommand.cs
│   │   ├── Evaluation/Queries/ RunEvaluationQuery.cs
│   │   ├── Interfaces/         IPlayerRepository.cs, IVectorSearchService.cs,
│   │   │                       IEmbeddingService.cs, ILlmService.cs, IStatsBombReader.cs
│   │   └── DTOs/               ScoutingResultDto.cs, PlayerMatchDto.cs, EvalReportDto.cs
│   ├── FootballIQ.Infrastructure/
│   │   ├── Persistence/        FootballIQDbContext.cs, Migrations/, PlayerRepository.cs
│   │   ├── StatsBomb/          StatsBombReader.cs, StatsBombIngestionService.cs, Models/
│   │   ├── FootballData/       FootballDataClient.cs
│   │   ├── VectorSearch/       PgVectorSearchService.cs, BM25SearchService.cs,
│   │   │                       HybridSearchService.cs, RrfFusionService.cs
│   │   ├── Llm/                SemanticKernelService.cs, EmbeddingService.cs,
│   │   │                       QueryTransformationService.cs, ScoutingResponseGenerator.cs
│   │   ├── Evaluation/         EvaluationService.cs
│   │   └── BackgroundServices/ DataIngestionBackgroundService.cs
│   └── FootballIQ.WebAPI/
│       ├── Endpoints/          ScoutEndpoints.cs, PlayerEndpoints.cs,
│       │                       EvaluationEndpoints.cs, AdminEndpoints.cs
│       ├── Middleware/         ExceptionMiddleware.cs
│       └── Program.cs
├── tests/
│   ├── FootballIQ.Domain.Tests/
│   ├── FootballIQ.Application.Tests/
│   ├── FootballIQ.Infrastructure.Tests/
│   └── FootballIQ.WebAPI.Tests/
├── data/
│   ├── statsbomb/              ← gitignored. Clone separately.
│   └── eval/                   ← scouting-eval.json, results-*.json
├── docs/architecture/
├── .github/workflows/ci.yml
├── docker-compose.yml
├── .env.example
├── CLAUDE.md
├── LEARNING.md
└── README.md
```

---

## Build Status

> At session start: find the last ✅ to know where we are. Start the next 🔲 task.

### Layer 1: Backend Foundation
_What we build: Working API + database + CI_
_What you learn: Clean Architecture structure, EF Core, Testcontainers, GitHub Actions_

| # | Task | Done when | Status |
|---|------|-----------|--------|
| 1.1 | Create solution + 8 projects + correct project references | `dotnet build FootballIQ.sln` succeeds. Dependency graph correct. | ✅ |
| 1.2 | Docker Compose + PostgreSQL 16 + pgvector | `docker compose up -d` → postgres shows healthy | 🔲 |
| 1.3 | EF Core + Npgsql + DbContext + connection string from env | `dotnet ef migrations add InitialCreate` runs without error | 🔲 |
| 1.4 | Domain entities + EF migration (Player, Match, PlayerStats) | Migration applied. Tables exist in DB. | 🔲 |
| 1.5 | IPlayerRepository interface + PlayerRepository | Interface defined. Implementation compiles. | 🔲 |
| 1.6 | football-data.org typed HTTP client + Polly retry | Can fetch and deserialize a fixture list from real API | 🔲 |
| 1.7 | GET /api/health + GET /api/players endpoints | Both return 200 OK in Swagger UI | 🔲 |
| 1.8 | Domain unit tests (5+ assertions) | `dotnet test FootballIQ.Domain.Tests` all pass | 🔲 |
| 1.9 | Testcontainers integration test: create + read player | Test creates real DB, passes, container stops cleanly | 🔲 |
| 1.10 | GitHub repo + Actions CI (build + test on push) | Push triggers green CI workflow | 🔲 |

### Layer 2: StatsBomb Data Ingestion
_What we build: Real player data in the database_
_What you learn: JSON parsing, IHostedService background workers, data aggregation, idempotency_

| # | Task | Done when | Status |
|---|------|-----------|--------|
| 2.1 | Clone StatsBomb Open Data → data/statsbomb/ (gitignored) | Folder exists with JSON. `git status` does not show it. | 🔲 |
| 2.2 | StatsBomb JSON models (Match, Event, Lineup) | One real match JSON deserializes with zero errors | 🔲 |
| 2.3 | StatsBombReader service (reads JSON from disk) | Can read full events + lineup for 1 match by ID | 🔲 |
| 2.4 | Player aggregate stats computation (pass%, xG, xA, press%) | Known player stats match expected values (spot-checked) | 🔲 |
| 2.5 | Idempotent ingestion + IngestionLog table | Running twice → identical row counts, no duplicates | 🔲 |
| 2.6 | POST /api/admin/ingest endpoint (async background) | POST → wait → GET /api/players returns real players | 🔲 |
| 2.7 | Integration test: ingest 1 match, assert player stats | Test green, stats in expected range | 🔲 |

### Layer 3: Semantic Search
_What we build: Semantic player search (meaning-based, not keyword)_
_What you learn: Vector embeddings, pgvector, HNSW index, Semantic Kernel basics_

| # | Task | Done when | Status |
|---|------|-----------|--------|
| 3.1 | pgvector extension + Embedding column + HNSW index migration | Column exists in DB. HNSW index visible. | 🔲 |
| 3.2 | PlayerProfileBuilder: stats → readable descriptive text | 5 players produce differentiated, readable profile strings | 🔲 |
| 3.3 | EmbeddingService via Semantic Kernel (Ollama / Azure) | Generates float[] of correct dimension for one profile | 🔲 |
| 3.4 | Embed all players during ingestion | After ingestion, all players have non-null embeddings | 🔲 |
| 3.5 | Cosine similarity search + GET /api/search endpoint | "creative midfielder" returns midfielders in top results | 🔲 |
| 3.6 | Integration tests for semantic search | "fast winger" returns wide attackers in top-5 | 🔲 |

### Layer 4: Hybrid RAG Pipeline
_What we build: Full natural language scouting (the portfolio centrepiece)_
_What you learn: BM25, Reciprocal Rank Fusion, query transformation, prompt engineering, token tracking_

| # | Task | Done when | Status |
|---|------|-----------|--------|
| 4.1 | PostgreSQL full-text search (tsvector/tsquery = BM25 equivalent) | Keyword query for known stat returns correct player at top | 🔲 |
| 4.2 | Reciprocal Rank Fusion service | Unit tests: two ranked lists merge correctly | 🔲 |
| 4.3 | QueryTransformationService (LLM expands to 2-3 variants) | "fast winger" → 2-3 reasonable alternatives, all non-empty | 🔲 |
| 4.4 | HybridSearchService (transform → BM25+semantic → RRF → top-K) | Manual check: hybrid outperforms semantic-only for keyword queries | 🔲 |
| 4.5 | ScoutingResponseGenerator (top-K context + LLM explanation) | POST /api/scout returns coherent, factually accurate explanation | 🔲 |
| 4.6 | Token usage tracking in every scouting response | Response includes tokensUsed and estimatedCostUsd | 🔲 |
| 4.7 | POST /api/scout endpoint wired end-to-end | Full pipeline returns target response format | 🔲 |
| 4.8 | Integration tests: 3 query types (position, stat, tactical) | All 3 return expected players in top results | 🔲 |

### Layer 5: Evaluation & Metrics
_What we build: Proof the system works — with real numbers_
_What you learn: RAGAS metrics, LLM-as-judge, holdout evaluation, baseline comparison_

| # | Task | Done when | Status |
|---|------|-----------|--------|
| 5.1 | Eval dataset: data/eval/scouting-eval.json (20 queries + expected results) | File committed. All 20 queries have verifiable expected results. | 🔲 |
| 5.2 | Context precision metric | Returns float 0-1 per query. Average computed. | 🔲 |
| 5.3 | Faithfulness metric (LLM-as-judge) | Returns float 0-1. Average ≥ 0.70. | 🔲 |
| 5.4 | Answer relevancy metric (embedding cosine sim) | Returns float 0-1. Average ≥ 0.75. | 🔲 |
| 5.5 | Baseline vs hybrid comparison (key README numbers) | Both result files committed. Hybrid outperforms baseline on ≥ 2/3 metrics. | 🔲 |
| 5.6 | POST /api/eval/run endpoint | Returns full metrics report for all 20 queries | 🔲 |
| 5.7 | Real metrics documented in README | README has real numbers with before/after comparison | 🔲 |

### Layer 6: Production & Polish
_What we build: Live deployed URL + excellent README_
_What you learn: OpenTelemetry, Problem Details (RFC 7807), Railway deployment, technical writing_

| # | Task | Done when | Status |
|---|------|-----------|--------|
| 6.1 | OpenTelemetry: traces + metrics + /metrics endpoint | /metrics returns Prometheus-format latency histograms | 🔲 |
| 6.2 | Problem Details middleware (RFC 7807) | All errors return `{type, title, status, detail}`. No stack traces. | 🔲 |
| 6.3 | OpenAPI + Swagger UI | /swagger shows all endpoints with examples | 🔲 |
| 6.4 | Rate limiting on /api/scout (10 req/min per IP) | 11th request returns 429 with Retry-After | 🔲 |
| 6.5 | Deploy to Railway + live URL | Live HTTPS URL returns 200 on /api/health | 🔲 |
| 6.6 | GitHub Actions CI/CD (auto-deploy on merge to main) | Merge to main triggers Railway deploy. Status badge in README. | 🔲 |
| 6.7 | Architecture diagram (Mermaid in README) | Diagram renders in GitHub, accurately shows Layer 4 pipeline | 🔲 |
| 6.8 | Excellent README (problem → architecture → metrics → quickstart) | A stranger can read it, understand it, and run it in < 10 minutes | 🔲 |

---

## Git Workflow Rules

**Branch naming:** `feature/[task-id]-[short-description]`
- Examples: `feature/1.1-solution-scaffold`, `feature/4.2-rrf-fusion`, `feature/6.5-railway-deploy`

**When to commit:** When each task's "Done when" criteria is met. One task = one commit minimum.

**Commit message format:** `[task-id] Short imperative description`
- `[1.1] Scaffold Clean Architecture solution with 8 projects`
- `[2.4] Compute player aggregate stats from StatsBomb events`
- `[4.2] Implement Reciprocal Rank Fusion score merger`

**Pull Request rules:**
- Open one PR per completed Layer
- PR title: `Layer N: [Layer Name]` — e.g., `Layer 1: Backend Foundation`
- PR description: list all tasks completed, key decisions made, what was learned
- PR requires green CI before merge
- Never merge directly to main

**Never commit:**
- `appsettings.json` with real API keys
- `.env` file with real secrets
- `data/statsbomb/` folder
- Docker volumes or database files

---

## Testing Rules

**Rule:** Write tests alongside the task, not after. Tests are part of the task definition.

| Test type | Project | Tests what | Tools |
|-----------|---------|------------|-------|
| Unit | Domain.Tests | Entity logic, value objects, pure functions | xUnit |
| Unit | Application.Tests | Query/command handlers (mock interfaces) | xUnit + Moq |
| Integration | Infrastructure.Tests | Repositories, ingestion, vector search (real DB) | xUnit + Testcontainers |
| E2E/API | WebAPI.Tests | HTTP endpoints, middleware, response shapes | xUnit + WebApplicationFactory |

**Never mock the database in infrastructure integration tests.** Testcontainers spins up real PostgreSQL. This is the industry standard.

**Test naming convention:** `[MethodOrClass]_[Scenario]_[ExpectedResult]`
- `ScoutPlayers_WithPositionQuery_ReturnsMatchingPlayers`
- `PlayerRepository_WhenPlayerDoesNotExist_ThrowsNotFoundException`
- `RrfFusion_WithTwoRankedLists_MergesAndDeduplicatesCorrectly`

---

## Progress Tracking Rules

**Session start:**
1. Read CLAUDE.md
2. Find last ✅ in Build Status
3. Say: "We are in Layer N. Last completed: [task]. Up next: [task X.Y] — [description]. This teaches you: [concept]."

**Session end:**
1. Update Build Status: ✅ = complete, 🔄 = in progress, 🔲 = not started
2. Update LEARNING.md with entry for what was done
3. Note any blockers, decisions, or questions for next session

---

## Learning Log Rules

**File:** `LEARNING.md` in project root. This is personal study notes — plain language only.

**Entry format (add after each completed task or new concept):**

```
## Task [X.Y]: [Short name]
**What we built:** [1-2 sentences, plain language]
**Key concept:** [The pattern or technology this introduced]
**Why it matters:** [Why this exists in real production systems]
**Mistake I made:** [Optional: what went wrong and how it was fixed]
```

**Comprehension checks:** After significant milestones, a question is asked to verify understanding. Record every Q&A in LEARNING.md under the relevant task entry:

```
### Comprehension Check
**Q:** [The question asked]
**My answer:** [What the user said, verbatim or close to it]
**Verdict:** [Correct / Partially correct / Needs revisiting — plus the key addition or correction]
```

Update this at the end of every session.

---

## Infrastructure Rules

| Command | Rule |
|---------|------|
| `dotnet build` | ✅ Run freely |
| `dotnet test` | ✅ Run freely |
| `dotnet run` | ✅ Run freely |
| `docker compose up -d` | ⚠️ Explain what it starts and why, wait for confirmation |
| `docker compose down` | ⚠️ Explain it stops and removes containers, wait |
| `dotnet ef migrations add` | ⚠️ Show migration name + explain schema change, wait |
| `dotnet ef database update` | ⚠️ Explain DB changes that will be applied, wait |
| Any Azure CLI command | ❌ Never run autonomously — always explain and wait |
| `git push --force` | ❌ Never — ask for explicit confirmation first |

When infrastructure is needed: explain the command, what it does, what it changes, then say: _"When you're ready, type `! <command>` in the prompt to run it."_

---

## Conventions

**Naming:**
```
Classes, Records:    PascalCase                  PlayerRepository.cs
Interfaces:          I + PascalCase              IPlayerRepository.cs
Async methods:       Verb + Async                GetPlayerByIdAsync()
Local variables:     camelCase                   var playerStats = ...
Database tables:     snake_case                  players, match_events, ingestion_log
API routes:          kebab-case                  /api/scout, /api/eval/run
Test classes:        [Subject]Tests              PlayerRepositoryTests.cs
Test methods:        Method_Scenario_Expected    ScoutPlayers_WithValidQuery_ReturnsPlayers
```

**Error handling:**
- Throw domain exceptions (`PlayerNotFoundException`) in Domain layer
- Catch all exceptions in `ExceptionMiddleware`, convert to Problem Details — never leak stack traces
- Validation: FluentValidation → 400 with field-level errors
- Never return `null` from repository methods — throw `NotFoundException` or return `Result<T>`

**Async rules (non-negotiable):**
- Every method touching I/O is `async Task<T>`
- Never use `.Result` or `.Wait()` anywhere in the codebase
- Always accept and forward `CancellationToken ct` from HTTP request context

**XML doc comments:**
- Required on every `public` method and class in Domain and Application layers
- One line only: `/// <summary>Returns players ranked by match score for the given scouting query.</summary>`
- Do not write multi-paragraph comments

---

## Environment Variables

| Variable | Used by | Example value |
|----------|---------|---------------|
| `POSTGRES_CONNECTION_STRING` | EF Core, Testcontainers | `Host=localhost;Database=footballiq;Username=postgres;Password=postgres` |
| `FOOTBALLDATA_API_KEY` | FootballDataClient | `abc123...` |
| `LLM_PROVIDER` | EmbeddingService, LlmService | `ollama` or `azureopenai` or `openai` |
| `OLLAMA_BASE_URL` | Ollama (local dev) | `http://localhost:11434` |
| `OLLAMA_EMBEDDING_MODEL` | EmbeddingService | `nomic-embed-text` |
| `OLLAMA_CHAT_MODEL` | LlmService | `llama3.2` |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI (prod) | `https://[name].openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI | `abc123...` |
| `AZURE_OPENAI_CHAT_DEPLOYMENT` | LlmService | `gpt-4o` |
| `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` | EmbeddingService | `text-embedding-3-small` |
| `EMBEDDING_DIMENSIONS` | pgvector column size | `1536` (OpenAI) or `768` (Ollama nomic) |

Store locally in `.env` (gitignored). Copy structure from `.env.example`. Never commit real values.

---

## Run Commands (correct order)

```powershell
# Step 1: Start the database (required before running API or tests)
docker compose up -d
# Verify: docker compose ps  → postgres shows "healthy"

# Step 2: Apply pending migrations (only needed after a new migration is added)
# Always explain the migration change before running this
dotnet ef database update --project src/FootballIQ.Infrastructure --startup-project src/FootballIQ.WebAPI

# Step 3: Run all tests
dotnet test

# Step 4: Run the API locally
dotnet run --project src/FootballIQ.WebAPI
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger

# Step 5: Ingest StatsBomb data (first-time setup, after database is running)
# Use Swagger UI or:
# POST http://localhost:5000/api/admin/ingest
# Body: { "competitionId": 11, "seasonId": 90 }
# (La Liga 2020/21 — good starting dataset)

# Step 6: Test the scouting endpoint
# POST http://localhost:5000/api/scout
# Body: { "query": "creative midfielder under 23 with high press" }

# Step 7: Run evaluation suite
# POST http://localhost:5000/api/eval/run
# Returns all RAGAS-equivalent metrics
```

---

## Key Technical Decisions (Interview Talking Points)

These are decisions made deliberately — expect to be asked about them:

1. **PostgreSQL + pgvector instead of Pinecone/Qdrant:**
   Single infrastructure. One backup strategy. No network hop between relational and vector data. At the scale of player embeddings (tens of thousands), pgvector HNSW has excellent performance. A dedicated vector DB is justified at billions of vectors.

2. **HNSW over IVFFlat:**
   IVFFlat centroids go stale as you insert more data — recall degrades silently. HNSW builds a navigable graph structure that maintains accuracy across incremental insertions. For a project where data is ingested in batches, HNSW is the correct default.

3. **Hybrid search (BM25 + semantic) over pure semantic:**
   Pure semantic search fails on exact attribute queries ("90% pass accuracy", "Bundesliga", "age 21"). BM25 handles these precisely. Semantic search handles conceptual queries ("creative under-pressure", "high press midfielder"). RRF combines both without needing to tune weights manually. The comparison in Layer 5 proves this with real numbers.

4. **Semantic Kernel over direct OpenAI SDK:**
   Semantic Kernel abstracts LLM providers — identical code runs against Ollama locally and Azure OpenAI in production, switched by an environment variable. It also handles retries, token counting, and streaming. Provider-agnostic design is considered better practice.

5. **Testcontainers over in-memory EF Core for integration tests:**
   EF Core's in-memory provider does not support PostgreSQL-specific features (pgvector queries, tsvector, JSON operators). Tests against in-memory pass while tests against real Postgres fail. Testcontainers is the industry standard for this reason.

---

## StatsBomb Data Reference

**How to download (one-time, from project root):**
```powershell
git clone https://github.com/statsbomb/open-data.git data/statsbomb
```

**Folder structure:**
```
data/statsbomb/data/
├── competitions.json           ← list of all competitions and seasons
├── matches/{competition_id}/
│   └── {season_id}.json        ← list of matches for that competition/season
├── events/
│   └── {match_id}.json         ← all events for a specific match
└── lineups/
    └── {match_id}.json         ← player lineups for a specific match
```

**Recommended starting dataset:** La Liga 2020/21 (competitionId=11, seasonId=90) — Messi's last Barcelona season, very rich event data, ~35 matches.

**Data is gitignored.** It is large and must be re-cloned on each new machine.

---

## Deployment Reference

**Platform:** Railway (railway.app, ~$5/month)

**Services needed in Railway:**
1. PostgreSQL service (Railway-managed, provides `DATABASE_URL` automatically)
2. Web service (connected to GitHub repo, deploys on push)

**Railway setup summary (detailed steps in Layer 6.5):**
1. Create Railway account and project
2. Add managed PostgreSQL service
3. Connect GitHub repository to web service
4. Set environment variables in Railway dashboard (see env var table above)
5. Enable pgvector: connect to Railway Postgres console, run `CREATE EXTENSION IF NOT EXISTS vector;`
6. Trigger first deploy

**Azure upgrade path (if needed after launch):**
All infrastructure can be migrated to Azure with zero application code changes:
- Azure Container Apps (replaces Railway web service)
- Azure Database for PostgreSQL Flexible Server (enable pgvector extension in Azure Portal)
- Azure OpenAI Service (replaces OpenAI API)
Only environment variables change.
