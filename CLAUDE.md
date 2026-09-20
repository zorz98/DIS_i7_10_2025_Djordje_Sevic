# GreenFinance

ESG procena finansijskih transakcija — mikroservisni sistem u .NET-u.

## Tehnologije

- **.NET 10**, ASP.NET Core Web API (Controllers)
- **EF Core 10 + SQL Server** — jedna instanca, logički odvojena baza po servisu
- **MassTransit + RabbitMQ** — asinhrona komunikacija (events)
- **YARP** — API Gateway / reverse proxy, sa dinamičkim destinacijama iz Consul-a
- **Polly** — retry + circuit breaker + timeout (ESGService → ReferenceDataService)
- **Consul** — service discovery (registracija + health check po servisu)
- **OpenTelemetry + Prometheus + Grafana** — metrike i alarmiranje (email preko MailHog)
- **Datadog Agent** — centralizovano logovanje (JSON logovi po kontejneru)
- **Docker + Docker Compose** — kontejnerizacija i lokalna orkestracija
- **xUnit + FluentAssertions + Moq** — unit testovi; **Testcontainers** — integration testovi
- **GitHub Actions** (CI/CD) + **GitHub Container Registry** (ghcr.io)

## Arhitektura

5 mikroservisa, svaki sa `Domain`/`Infrastructure`/`Api` slojevima i sopstvenom bazom:

| Servis | Baza | Odgovornost | Event |
| --- | --- | --- | --- |
| TransactionService | TransactionDb | kreira/pregleda transakcije | publikuje `TransactionCreatedEvent` |
| ESGService | EsgDb | računa CO2/ESG rezultat (poziva ReferenceDataService preko REST + Polly) | konzumira `TransactionCreatedEvent`, publikuje `EsgCalculatedEvent` |
| ReferenceDataService | ReferenceDataDb | emisijski faktori po kategoriji | – |
| ReportService | ReportDb | read-model / periodični izveštaji | konzumira oba eventa |
| NotificationService | NotificationDb | obaveštenja pri niskom ESG score-u | konzumira `EsgCalculatedEvent` |

`ApiGateway` (YARP) rutira REST pozive ka svim servisima. Deljeni event contracts su u
`src/BuildingBlocks/GreenFinance.Contracts`.

Detalji: [`docs/architecture.md`](docs/architecture.md),
[`docs/business-logic.md`](docs/business-logic.md),
[`docs/deployment.md`](docs/deployment.md). Za rad na pojedinačnom servisu koristi
odgovarajući skill iz `.claude/skills/`.
