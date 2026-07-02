# ADR-002: Testcontainers umesto in-memory EF Core providera

**Status:** Accepted  
**Date:** Jun 2026  
**Deciders:** Aleksa Vlaski

## Context

FootballIQ Scout infrastructure layer koristi PostgreSQL-specifične feature-e: pgvector za similarity search, tsvector/tsquery za full-text search, JSON operatore za StatsBomb event data. Integration testovi moraju da verifikuju da repository i ingestion logika rade ispravno.

## Decision

Koristimo Testcontainers (xUnit `IAsyncLifetime`) da pokrenemo pravi `postgres:16-alpine` container za svaki test class, sa pravim migrationima.

## Options Considered

### Option A: Testcontainers (odabrano)
| Dimension | Assessment |
|-----------|------------|
| Realizam | High — identičan engine kao produkcija |
| Brzina | Med — ~3-5s za container startup per test class |
| Kompleksnost setup-a | Low — 5 linija boilerplate u `IAsyncLifetime` |
| PostgreSQL features | Sve podržano |

**Pros:**
- pgvector, tsvector, HNSW indeksi rade identično kao prod
- Migracije se primenjuju na pravi engine — schema problemi se hvataju u testovima
- "Test passes locally, fails on prod" scenario je eliminisan

**Cons:**
- Docker mora biti instaliran na CI mašini (GitHub Actions: dostupan by default na ubuntu-latest)
- Sporiji od in-memory (~3-5s startup per test class)

### Option B: EF Core In-Memory provider
| Dimension | Assessment |
|-----------|------------|
| Realizam | Low — nije relaciona baza |
| Brzina | Very High — instant startup |
| Kompleksnost setup-a | Very Low |
| PostgreSQL features | Ne podržava ništa PostgreSQL-specifično |

**Pros:** Brz, bez Docker zavisnosti

**Cons:**
- Ne podržava pgvector, tsvector, JSON operatore — sve što FootballIQ koristi od Layer 3 nadalje
- Testovi prolaze lokalno, pucaju na pravoj bazi
- Microsoft eksplicitno ne preporučuje in-memory provider za integration testove

### Option C: SQLite umesto PostgreSQL za testove
Isto kao in-memory — ne podržava pgvector. Eliminisano iz razmatranja.

## Trade-off Analysis

In-memory provider je brži ali testira pogrešnu stvar — testira da li EF Core može da prevede LINQ u memorijske operacije, ne da li SQL koji se generiše radi na PostgreSQL-u. Svaki PostgreSQL-specifičan feature (koji FootballIQ počinje da koristi od Layer 3) bi napravio false-positive testove: zeleno lokalno, crveno u produkciji. Testcontainers rešava ovaj problem fundamentalno.

3-5 sekundi startup po test class-u je prihvatljiv trade-off za garanciju da testovi testiraju pravu stvar.

## Consequences

- CI/CD (GitHub Actions) ne zahteva posebnu konfiguraciju — Docker je dostupan by default na ubuntu-latest runner-ima
- Svaki novi PostgreSQL feature koji se doda (pgvector u Layer 3, tsvector u Layer 4) je automatski pokriven testovima bez promene test infrastrukture
- Developer mora imati Docker Desktop instaliran lokalno — dokumentovano u README quickstart sekciji
