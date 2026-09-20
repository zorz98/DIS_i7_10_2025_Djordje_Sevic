---
name: esg-service
description: Use when working on ESGService — CO2/ESG score calculation, the ReferenceDataService HTTP client with Polly resilience, consuming TransactionCreatedEvent, or its tests/migrations. Triggers on files under src/Services/ESGService or tests/ESGService.*.
---

# ESGService

Konzumira `TransactionCreatedEvent`, sinhrono (REST) pita ReferenceDataService za CO2
faktor kategorije, računa CO2/ESG skorove, čuva rezultat i publikuje
`EsgCalculatedEvent` (konzumenti: NotificationService, ReportService). Formule i
primer: [`docs/business-logic.md`](../../../docs/business-logic.md#esgservice--izračunavanje-esg-rezultata).

## Layout

- `ESGService.Domain` — `Co2Calculator`, `EsgScoreCalculator` (čiste, bez zavisnosti
  — glavni kandidati za unit testove), `EsgCalculationService` (orkestrira lookup +
  kalkulaciju + upsert, testabilan bez MassTransit-a), `EsgResult`/`EsgResultStatus`,
  `IReferenceDataClient`, `IEsgResultRepository`.
- `ESGService.Infrastructure` — `EsgDbContext`, `EsgResultRepository`,
  `ReferenceDataClient` (HttpClient impl, hvata `BrokenCircuitException` →
  `ReferenceDataLookupResult(false, null)`), `Migrations/`.
- `ESGService.Api` — `Consumers/TransactionCreatedEventConsumer.cs` (tanak — deleguje
  na `EsgCalculationService`), `Program.cs` (Polly `AddResilienceHandler` na
  `IReferenceDataClient` HttpClient-u, MassTransit setup).

## Konvencije

- Nova poslovna logika (kalkulacija, pravila) ide u `EsgCalculationService` ili nove
  čiste klase u `Domain`, **ne** direktno u konzumer — konzumer ostaje tanak da bi
  ostatak bio testabilan bez MassTransit test harness-a.
- Kad ReferenceDataService nije dostupan (circuit otvoren), rezultat se upisuje sa
  `EsgResultStatus.TemporarilyUnavailable` i `EsgCalculatedEvent` se **ne** publikuje
  — ne menjaj ovo ponašanje bez ažuriranja `docs/business-logic.md`.
- RabbitMQ/HTTP baza adresa se čita lenjo unutar `UsingRabbitMq`/`AddHttpClient`
  callback-a u `Program.cs`, ne eager u promenljivu pre registracije servisa.
- **NIKAD ne postavljaj `HttpClient.Timeout`** na `IReferenceDataClient`-ovom
  HttpClient-u. Baca običan `TaskCanceledException` koji Polly-jev podrazumevani
  predikat ne prepoznaje kao tranzijentnu grešku, pa Retry/CircuitBreaker tiho nikad
  ne bi bili aktivirani (stvaran bag, otkriven tek preko `esg_referencedata_circuit_state`
  metrike u DIS-26 — gauge je ostajao "closed" i pored ponovljenih pada
  ReferenceDataService-a). Timeout ide isključivo kroz Polly-jevu `pipeline.AddTimeout(...)`
  strategiju (innermost, posle Retry i CircuitBreaker u `AddResilienceHandler`), čiji
  `TimeoutRejectedException` Polly prepoznaje kao tranzijentan.
- `EsgMetrics` (Domain) drži circuit breaker gauge (`esg.referencedata.circuit_state`)
  i brojače `esg.results.calculated`/`esg.results.unavailable` — ažuriraj ga preko
  `SetCircuitOpen/Closed/HalfOpen()` iz Polly `OnOpened`/`OnClosed`/`OnHalfOpened`
  callback-ova u `Program.cs`, ne dodaji paralelni mehanizam za praćenje stanja.

## Testovi

```bash
dotnet test tests/ESGService.UnitTests/ESGService.UnitTests.csproj
dotnet test tests/ESGService.IntegrationTests/ESGService.IntegrationTests.csproj
```

`ESGServiceApiFactory` (integration testovi) postavlja `ReferenceDataService:BaseUrl`
na neruting adresu da bi realno vežbao `temporarily_unavailable` putanju bez pravog
ReferenceDataService kontejnera.

## EF Core migracije

```bash
dotnet ef migrations add <Ime> \
  --project src/Services/ESGService/ESGService.Infrastructure \
  --startup-project src/Services/ESGService/ESGService.Api \
  -o Migrations
```
