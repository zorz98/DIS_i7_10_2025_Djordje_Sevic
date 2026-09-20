# Poslovna logika — GreenFinance

## Pregled

GreenFinance je mikroservisna aplikacija namenjena kompanijama koje žele da prate ESG
(Environmental, Social, Governance) uticaj svojih poslovnih troškova. Korisnik unosi
finansijsku transakciju (npr. "Firma je potrošila 5.000 EUR na gorivo"), a sistem na
osnovu kategorije troška i odgovarajućeg emisijskog faktora izračunava procenjeni CO2
uticaj i ESG score. Na osnovu prikupljenih podataka generišu se periodični ESG
izveštaji, a kompanija dobija obaveštenje kada njen ESG score padne ispod praga.

Primer toka: *Firma je potrošila 5.000 EUR na gorivo → sistem izračuna procenjeni
CO2 → transakcija dobije ESG kategoriju/score → korisnik može da vidi mesečni ESG
izveštaj → ako je score nizak, dobija obaveštenje.*

## Domenski entiteti po servisu

### TransactionService — poslovne transakcije

| Polje | Opis |
| --- | --- |
| `Id` | identifikator transakcije (GUID) |
| `CompanyId` | kompanija koja je izvršila trošak |
| `Category` | kategorija troška (`Fuel`, `Electricity`, `Flights`, `PublicTransport`, `OfficeSupplies`) |
| `Amount` / `Currency` | iznos i valuta |
| `Date` | datum transakcije |
| `Status` | `Created` nakon uspešnog upisa |

`POST /transactions` kreira transakciju, upisuje je u `TransactionDb` i publikuje
`TransactionCreatedEvent` na RabbitMQ.

### ReferenceDataService — referentni podaci

Čuva emisijski faktor (kg CO2 po potrošenom EUR-u) po kategoriji troška, seed-ovan pri
pokretanju:

| Kategorija | CO2 faktor (kg/EUR) |
| --- | --- |
| Fuel | 2.31 |
| Electricity | 0.45 |
| Flights | 2.50 |
| PublicTransport | 0.10 |
| OfficeSupplies | 0.20 |

`GET /categories`, `GET /categories/{name}`, `GET /emission-factors` omogućavaju
ESGService-u (i drugim klijentima) da pročitaju ove faktore.

### ESGService — izračunavanje ESG rezultata

Kada stigne `TransactionCreatedEvent`, ESGService:

1. Sinhrono (REST, preko `HttpClient` zaštićenog Polly retry + circuit breaker
   politikom) pita ReferenceDataService za emisijski faktor kategorije.
2. Računa `co2Kg = amount * emissionFactor` (primer iz specifikacije: 5.000 EUR na
   Fuel → `5000 * 2.31 = 11550` kg CO2).
3. Računa dva skora:
   - **environmentalScore** — koliko je "čista" kategorija troška
     (`100 - emissionFactor * 20`, ograničeno na 0–100). Manji emisijski faktor →
     viši score.
   - **overallScore** — `environmentalScore` dodatno umanjen srazmerno apsolutnom CO2
     otisku transakcije (`environmentalScore - co2Kg / 500`, ograničeno na 0–100), jer
     velika transakcija ima veći ukupni uticaj čak i u "čistoj" kategoriji.
4. Upisuje `EsgResult` u `EsgDb` i publikuje `EsgCalculatedEvent`.

Ako ReferenceDataService privremeno ne radi, circuit breaker se otvara nakon
ponovljenih neuspešnih pokušaja i ESGService upisuje rezultat sa statusom
`temporarily_unavailable` umesto da propagira grešku — klijent koji čita
`GET /esg/transaction/{id}` vidi taj status i može kasnije ponovo da proveri.

### NotificationService — obaveštenja

Konzumira `EsgCalculatedEvent`. Ako je `overallScore < 40`, kreira obaveštenje
("Your ESG score requires attention"), loguje ga i čuva u `NotificationDb`.
`GET /notifications/company/{id}` prikazuje istoriju obaveštenja kompanije.

### ReportService — periodični izveštaji

Ne poziva druge servise sinhrono — umesto toga sluša i `TransactionCreatedEvent` i
`EsgCalculatedEvent` i gradi sopstveni lokalni read-model (`ReportDb`) po transakciji.
`GET /reports/company/{id}?month=&year=` agregira taj read-model za traženi period:

- `totalExpenses` — suma iznosa transakcija
- `totalCo2` — suma CO2 (0 za transakcije koje još nemaju ESG rezultat)
- `esgScore` — prosek `overallScore` transakcija koje su već izračunate (`null` ako
  nijedna još nije)
- `transactions` — broj transakcija u periodu

Kako `TransactionCreatedEvent` i `EsgCalculatedEvent` stižu sa različitih RabbitMQ
redova, ne postoji garancija redosleda između njih; ReportService tretira upis kao
pravi upsert u oba smera (koji god event stigne prvi kreira red, drugi ga dopunjuje),
tako da je read-model ispravan bez obzira na redosled dostave.

## Komunikacija

- **Sinhrona (REST)**: Gateway → svi servisi; ESGService → ReferenceDataService.
- **Asinhrona (RabbitMQ, preko MassTransit)**:
  - `TransactionCreatedEvent`: TransactionService → ESGService, ReportService.
  - `EsgCalculatedEvent`: ESGService → NotificationService, ReportService.

Detaljan dijagram se nalazi u [`architecture.md`](architecture.md).
