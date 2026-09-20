# GreenFinance – ESG procena finansijskih transakcija

GreenFinance je mikroservisna aplikacija koja kompanijama omogućava da unesu svoje
poslovne transakcije i dobiju procenu ESG (Environmental, Social, Governance) uticaja
na osnovu kategorije troška, iznosa i CO2 faktora.

> Status: u razvoju. Ovaj README se dopunjava kroz razvojne korake (DIS-01 ... DIS-13).

## Sadržaj

- [`docs/business-logic.md`](docs/business-logic.md) – opis poslovne logike sistema
- [`docs/architecture.md`](docs/architecture.md) – arhitektura i dijagram sistema
- [`docs/deployment.md`](docs/deployment.md) – uputstvo za CI/CD pipeline

## Mikroservisi

| Servis | Odgovornost |
| --- | --- |
| TransactionService | kreiranje i pregled poslovnih transakcija |
| ESGService | izračunavanje CO2/ESG rezultata po transakciji |
| ReferenceDataService | referentni podaci o kategorijama i emisionim faktorima |
| ReportService | mesečni/godišnji ESG izveštaji po kompaniji |
| NotificationService | obaveštenja kada ESG score padne ispod praga |
| ApiGateway | YARP reverse proxy ka svim servisima |

## Pokretanje (razvojno okruženje)

```bash
cp deploy/.env.example deploy/.env
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up --build
```

Gateway je dostupan na `http://localhost:8080`, RabbitMQ management UI na
`http://localhost:15672` (guest/guest).

Detaljno uputstvo za build/test/deploy po fazama nalazi se u
[`docs/deployment.md`](docs/deployment.md).
