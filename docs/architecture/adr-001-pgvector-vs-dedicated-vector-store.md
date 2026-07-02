# ADR-001: pgvector umesto dedikovanog vector store-a (Pinecone/Qdrant)

**Status:** Accepted  
**Date:** Jun 2026  
**Deciders:** Aleksa Vlaski

## Context

FootballIQ Scout treba da čuva i pretražuje vector embeddings za profile igrača (float[] dimenzije 768 ili 1536). Potrebna je cosine similarity pretraga nad desetinama hiljada vektora. Projekat već koristi PostgreSQL 16 za relacione podatke.

## Decision

Koristimo pgvector ekstenziju unutar postojećeg PostgreSQL instance-a, sa HNSW indeksom.

## Options Considered

### Option A: pgvector (odabrano)
| Dimension | Assessment |
|-----------|------------|
| Kompleksnost infra | Low — ista baza, jedan servis |
| Cena | Besplatno (open-source ekstenzija) |
| Skalabilnost | Odlična do ~1M vektora sa HNSW |
| Maintainability | High — jedan backup, jedan connection string |

**Pros:**
- Nema dodatnog servisa za deploy i maintain
- JOIN između relacionih podataka i vektora u jednom SQL upitu — nema network hop-a
- Jedna backup strategija pokriva sve
- Railway deployment: jedan addon umesto dva

**Cons:**
- Ne skalira na milijarde vektora (ali football players dataset je ~50k max)
- Nema managed cloud tier kao Pinecone

### Option B: Pinecone
| Dimension | Assessment |
|-----------|------------|
| Kompleksnost infra | High — drugi cloud servis, drugi API key |
| Cena | Besplatni tier ograničen, paid od $70/mesečno |
| Skalabilnost | Odlična na ogromnoj skali |
| Maintainability | Low — dva servisa, dva failpoint-a |

**Pros:** Managed, skalira do milijardi vektora

**Cons:** Cena, network latency između PostgreSQL i Pinecone, kompleksniji deploy

### Option C: Qdrant (self-hosted)
Sličan Pinecone-u po arhitekturi, ali self-hosted. Treba Docker Compose servis + persistent storage + zasebna backup strategija. Nema prednost nad pgvector na ovoj skali.

## Trade-off Analysis

Ključno pitanje je: opravdava li veličina dataseta drugi servis? Player embeddings u StatsBomb open data su max ~50,000 vektora. HNSW u pgvector ima odličan recall i latenciju na ovoj veličini — benchmark u dokumentaciji pokazuje <10ms similarity search na 1M vektora. Dedicated vector DB ima smisla na stotinama miliona vektora, što ovaj projekat nikad neće dostići.

## Consequences

- JOIN između player stats i embeddings je jedan SQL upit — nema round-trip između dva servisa
- Railway deploy: jedan PostgreSQL addon pokriva sve
- Ako projekat ikad naraste na enterprise skalu, migracija na Qdrant/Pinecone je moguća bez promene application code-a (samo `IVectorSearchService` implementacija se menja)
