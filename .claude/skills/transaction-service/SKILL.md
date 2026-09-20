---
name: transaction-service
description: Use when working on TransactionService — creating/viewing business transactions, publishing TransactionCreatedEvent, or its EF Core migrations/tests. Triggers on files under src/Services/TransactionService or tests/TransactionService.*.
---

# TransactionService

Kreira i pregleda poslovne transakcije. Nakon uspešnog upisa publikuje
`TransactionCreatedEvent` (konzumenti: ESGService, ReportService). Detalji poslovne
logike: [`docs/business-logic.md`](../../../docs/business-logic.md#transactionservice--poslovne-transakcije).

## Layout

- `src/Services/TransactionService/TransactionService.Domain` — `Transaction`
  entitet (factory `Transaction.Create(...)`), `TransactionStatus`,
  `ITransactionRepository`.
- `src/Services/TransactionService/TransactionService.Infrastructure` —
  `TransactionDbContext`, `TransactionRepository`, `Migrations/`.
- `src/Services/TransactionService/TransactionService.Api` —
  `Controllers/TransactionsController.cs` (POST/GET), `Program.cs` (DbContext +
  MassTransit/RabbitMQ setup), `Contracts/TransactionDtos.cs`.

## Konvencije

- Domain entitet se kreira isključivo preko `Transaction.Create(...)` (postavlja
  `Id`, `Status = Created`) — ne konstruiši `Transaction` direktno van repository/test
  koda.
- Event se publikuje u kontroleru preko `IPublishEndpoint.Publish(...)` **posle**
  uspešnog `repository.AddAsync(...)`, sa poljima 1:1 iz `TransactionCreatedEvent`
  (`src/BuildingBlocks/GreenFinance.Contracts/Events`). Ako menjaš polja transakcije,
  proveri da li event treba da ih nosi, i ako da — ažuriraj Contracts projekat i sve
  konzumente (vidi `pr-review` skill/subagent za punu checklistu).
- RabbitMQ/DB konfiguracija u `Program.cs` se čita lenjo, unutar
  `UsingRabbitMq((context, cfg) => {...})` odn. `AddDbContext` callback-a — ne u
  promenljivu pre `AddMassTransit`/`AddDbContext` (WebApplicationFactory testovi tada
  ne bi videli config override).
- Servis se registruje u Consul (`GreenFinance.ServiceDiscovery`) i izlaže `/metrics`
  (`GreenFinance.Observability`, uklj. `TransactionMetrics` brojač
  `transactions.created`, ažuriran u `TransactionsController.Create`) — vidi
  [`docs/architecture.md`](../../../docs/architecture.md#service-discovery-consul).

## Testovi

```bash
dotnet test tests/TransactionService.UnitTests/TransactionService.UnitTests.csproj
dotnet test tests/TransactionService.IntegrationTests/TransactionService.IntegrationTests.csproj
```

Integration testovi koriste Testcontainers (SQL Server + RabbitMQ) preko
`TransactionServiceApiFactory` — zahtevaju pokrenut Docker.

## EF Core migracije

```bash
dotnet ef migrations add <Ime> \
  --project src/Services/TransactionService/TransactionService.Infrastructure \
  --startup-project src/Services/TransactionService/TransactionService.Api \
  -o Migrations
```
