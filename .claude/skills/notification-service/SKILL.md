---
name: notification-service
description: Use when working on NotificationService — the low ESG score notification rule, notification history, or its tests/migrations. Triggers on files under src/Services/NotificationService or tests/NotificationService.*.
---

# NotificationService

Konzumira `EsgCalculatedEvent`; ako je `overallScore < Notification.LowScoreThreshold`
(40), kreira i čuva obaveštenje (u osnovnoj verziji samo log + DB upis, bez pravog
slanja emaila). Detalji: [`docs/business-logic.md`](../../../docs/business-logic.md#notificationservice--obaveštenja).

## Layout

- `NotificationService.Domain` — `Notification` (factory
  `Notification.ForLowEsgScore(...)`, `LowScoreThreshold` konstanta,
  `Notification.ShouldNotify(overallScore)`), `NotificationDecisionService`
  (orkestrira odluku + upis, testabilan bez MassTransit-a),
  `INotificationRepository`.
- `NotificationService.Infrastructure` — `NotificationDbContext`,
  `NotificationRepository`, `Migrations/`.
- `NotificationService.Api` — `Consumers/EsgCalculatedEventConsumer.cs` (tanak —
  deleguje na `NotificationDecisionService`, loguje ishod),
  `Controllers/NotificationsController.cs` (`GET /notifications/company/{id}`).

## Konvencije

- Prag i poruka žive isključivo u `Notification` (Domain) — ne hardkoduj `40` ili
  tekst poruke na drugim mestima (konzumer, kontroler).
- `NotificationDecisionService.ProcessAsync(...)` vraća `null` kad notifikacija nije
  potrebna (score >= threshold) — konzumer to koristi da odluči da li da loguje, a
  ne baca izuzetak za "normalan" slučaj.
- Ako se doda pravo slanje (SMTP/email) ovo postaje bonus stavka van osnovne
  verzije — pre nego što se doda, proveri `docs/business-logic.md`/plan da li je to
  još uvek van obima.

## Testovi

```bash
dotnet test tests/NotificationService.UnitTests/NotificationService.UnitTests.csproj
dotnet test tests/NotificationService.IntegrationTests/NotificationService.IntegrationTests.csproj
```

## EF Core migracije

```bash
dotnet ef migrations add <Ime> \
  --project src/Services/NotificationService/NotificationService.Infrastructure \
  --startup-project src/Services/NotificationService/NotificationService.Api \
  -o Migrations
```
