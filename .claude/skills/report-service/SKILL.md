---
name: report-service
description: Use when working on ReportService — the event-sourced read-model, company report aggregation, or its tests/migrations. Triggers on files under src/Services/ReportService or tests/ReportService.*.
---

# ReportService

Ne poziva druge servise sinhrono — gradi sopstveni lokalni read-model konzumujući i
`TransactionCreatedEvent` i `EsgCalculatedEvent`, pa agregira na zahtev preko
`GET /reports/company/{id}?month=&year=`. Detalji:
[`docs/business-logic.md`](../../../docs/business-logic.md#reportservice--periodični-izveštaji).

## Layout

- `ReportService.Domain` — `TransactionRecord` (denormalizovani read-model red),
  `ITransactionRecordRepository`, `ReportAggregator` (čista klasa — sabira
  expenses/CO2, prosek `overallScore`, glavni kandidat za unit testove),
  `CompanyReport`.
- `ReportService.Infrastructure` — `ReportDbContext`, `TransactionRecordRepository`
  (vidi napomenu ispod), `Migrations/`.
- `ReportService.Api` — `Consumers/TransactionCreatedEventConsumer.cs`,
  `Consumers/EsgCalculatedEventConsumer.cs`, `Controllers/ReportsController.cs`.

## KRITIČNA konvencija — race condition između dva konzumera

`TransactionCreatedEvent` i `EsgCalculatedEvent` stižu sa **različitih RabbitMQ
redova** — nema garancije da prvi stigne pre drugog, čak i kada u praksi
TransactionService objavljuje pre ESGService-a. `TransactionRecordRepository` mora
ostati pravi upsert u oba smera:

- Ako `EsgCalculatedEvent` stigne prvi, kreira placeholder red (samo CompanyId/
  Category/Co2Kg/OverallScore); `TransactionCreatedEvent` ga kasnije dopunjuje
  (Amount/Date/Category), **ne** dodaje novi red.
- Concurrent insert (oba konzumera istovremeno vide "ne postoji" pa oba pokušaju
  INSERT) mora biti uhvaćen (`DbUpdateException`) i pretvoren u UPDATE — vidi
  `UpsertAsync` helper u `TransactionRecordRepository`. Ovo je stvarni bag pronađen
  integracionim testom u DIS-10 (ne pojednostavljuj nazad na "ako ne postoji, samo
  insert").

## Testovi

```bash
dotnet test tests/ReportService.UnitTests/ReportService.UnitTests.csproj
dotnet test tests/ReportService.IntegrationTests/ReportService.IntegrationTests.csproj
```

`ReportsApiTests` namerno publikuje oba eventa nazad-nazad da uhvati regresiju te
race-condition — ne "popravljaj" test dodavanjem veštačkog delay-a između publish
poziva.

## EF Core migracije

```bash
dotnet ef migrations add <Ime> \
  --project src/Services/ReportService/ReportService.Infrastructure \
  --startup-project src/Services/ReportService/ReportService.Api \
  -o Migrations
```
