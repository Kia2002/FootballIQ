# FootballIQ Scout — Learning Log

This is my personal study notes while building FootballIQ Scout.
Updated at the end of every session. Written in plain language — no jargon.

---

## Task 1.1: Solution Scaffold
**What we built:** A .NET solution file with 8 projects — 4 source (Domain, Application, Infrastructure, WebAPI) and 4 matching test projects. Each project is connected to the others following the Clean Architecture dependency rule.

**Key concept:** Clean Architecture — inner layers (Domain, Application) don't know about outer layers (Infrastructure, WebAPI). Dependencies only flow inward. This means the business logic is completely independent of the database, HTTP framework, or any external library.

**Why it matters:** In production systems, you want to be able to swap your database from PostgreSQL to SQL Server without touching a line of business logic. You also want to unit-test your use cases without starting a real database. Clean Architecture makes both of these possible.

**Mistake I made:** None — but important to remember: if you ever try to reference `FootballIQ.Infrastructure` from `FootballIQ.Domain`, the compiler will stop you. That's the architecture enforcing itself.
