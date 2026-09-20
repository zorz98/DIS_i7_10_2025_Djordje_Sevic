---
name: reference-data-service
description: Use when working on ReferenceDataService — emission factor / category reference data, its seeded lookup endpoints, or its tests/migrations. Triggers on files under src/Services/ReferenceDataService or tests/ReferenceDataService.*.
---

# ReferenceDataService

Čuva emisijski faktor (kg CO2 po EUR-u) po kategoriji troška. Nema zavisnosti od
drugih servisa i ne konzumira/publikuje evente — čisto REST, čita ga samo ESGService
sinhrono. Seed podaci: [`docs/business-logic.md`](../../../docs/business-logic.md#referencedataservice--referentni-podaci).

## Layout

- `ReferenceDataService.Domain` — `EmissionFactor` entitet,
  `IEmissionFactorRepository`.
- `ReferenceDataService.Infrastructure` — `ReferenceDataDbContext` (seed podaci u
  `OnModelCreating` preko `HasData`), `EmissionFactorRepository`, `Migrations/`.
- `ReferenceDataService.Api` — `Controllers/CategoriesController.cs`
  (`GET /categories`, `GET /categories/{name}`),
  `Controllers/EmissionFactorsController.cs` (`GET /emission-factors`).

## Konvencije

- Ovo je namerno najjednostavniji servis (najpre se implementira, nema
  RabbitMQ zavisnosti) — čuvaj ga takvim; asinhrona logika ide u druge servise.
- Nova kategorija/faktor ide kroz EF Core migraciju sa `HasData` seed-om u
  `ReferenceDataDbContext.OnModelCreating`, ne runtime insert.
- Ako se doda nova kategorija ovde, proveri da `docs/business-logic.md` tabela
  ostane sinhronizovana.
- Servis se registruje u Consul (`GreenFinance.ServiceDiscovery`) i izlaže `/metrics`
  (`GreenFinance.Observability`) — nema sopstveni business meter, samo ugrađene
  ASP.NET Core/HTTP/runtime metrike.

## Keš (Redis)

`IEmissionFactorRepository` je registrovan kao `CachedEmissionFactorRepository`
(`ReferenceDataService.Infrastructure/CachedEmissionFactorRepository.cs`) —
cache-aside dekorator oko `EmissionFactorRepository`, backed by Redis
(`IDistributedCache`, `Microsoft.Extensions.Caching.StackExchangeRedis`). Ključevi:
`emission-factors:all`, `emission-factor:{category}`; TTL 24h (podaci se praktično
nikad ne menjaju u runtime-u — vidi seed napomenu iznad — konačan TTL samo
izbegava specijalan slučaj "kako se ovo ikad osvežava", ne štiti od stvarne
invalidacije). Svaki Redis poziv je u `try/catch` za
`RedisConnectionException`/`RedisTimeoutException`/`TimeoutException` — nedostupan
Redis nikad ne sme da obori `/categories` endpoint, samo pada nazad na bazu
(`ConfigurationOptions.AbortOnConnectFail = false` + kratki timeout-i u
`Program.cs`, da fallback bude brz umesto da blokira zahtev). Ako menjaš
`EmissionFactorRepository`, dekorator ostaje nepromenjen (zavisi od
`IEmissionFactorRepository`, ne od konkretne klase — bitno za testabilnost sa Moq).

## Testovi

```bash
dotnet test tests/ReferenceDataService.UnitTests/ReferenceDataService.UnitTests.csproj
dotnet test tests/ReferenceDataService.IntegrationTests/ReferenceDataService.IntegrationTests.csproj
```

## EF Core migracije

```bash
dotnet ef migrations add <Ime> \
  --project src/Services/ReferenceDataService/ReferenceDataService.Infrastructure \
  --startup-project src/Services/ReferenceDataService/ReferenceDataService.Api \
  -o Migrations
```
