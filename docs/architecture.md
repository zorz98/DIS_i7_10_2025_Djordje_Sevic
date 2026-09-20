# Arhitektura — GreenFinance

## Mikroservisi

| Servis | Baza | Odgovornost |
| --- | --- | --- |
| ApiGateway (YARP) | – | reverse proxy ka svim servisima, dinamički otkriva destinacije preko Consul-a |
| TransactionService | `TransactionDb` | kreiranje/pregled transakcija, publikuje `TransactionCreatedEvent` |
| ESGService | `EsgDb` | računa CO2/ESG rezultat, publikuje `EsgCalculatedEvent` |
| ReferenceDataService | `ReferenceDataDb` | referentni podaci (kategorije, emisijski faktori) |
| ReportService | `ReportDb` | agregirani periodični izveštaji |
| NotificationService | `NotificationDb` | obaveštenja pri niskom ESG score-u |

Sve baze su logički odvojene (svaki servis ima svoju bazu i pristupa samo njoj), ali
radi jednostavnijeg lokalnog/dev okruženja hostovane su na jednoj SQL Server instanci
(vidi [`deployment.md`](deployment.md)).

## Dijagram sistema

```mermaid
flowchart TB
    Client(["Klijent / Frontend"])

    subgraph Gateway["API Gateway (YARP)"]
        GW[ApiGateway]
    end

    subgraph Services["Mikroservisi"]
        TS[TransactionService]
        ESG[ESGService]
        RDS[ReferenceDataService]
        RS[ReportService]
        NS[NotificationService]
    end

    subgraph Data["Baze (SQL Server)"]
        TDB[(TransactionDb)]
        EDB[(EsgDb)]
        RDB[(ReferenceDataDb)]
        RPDB[(ReportDb)]
        NDB[(NotificationDb)]
    end

    MQ{{RabbitMQ}}
    Consul{{Consul}}

    Client -->|REST| GW
    GW -->|REST, dinamička destinacija| TS
    GW -->|REST| ESG
    GW -->|REST| RDS
    GW -->|REST| RS
    GW -->|REST| NS

    TS --> TDB
    ESG --> EDB
    RDS --> RDB
    RS --> RPDB
    NS --> NDB

    ESG -->|REST: GET emission factor<br/>Polly retry + circuit breaker| RDS

    TS -->|publish TransactionCreatedEvent| MQ
    MQ -->|consume| ESG
    MQ -->|consume| RS

    ESG -->|publish EsgCalculatedEvent| MQ
    MQ -->|consume| NS
    MQ -->|consume| RS

    TS -.->|register + /health| Consul
    ESG -.->|register + /health| Consul
    RDS -.->|register + /health| Consul
    RS -.->|register + /health| Consul
    NS -.->|register + /health| Consul
    GW -.->|discover destinations every 10s| Consul

    subgraph Observability
        Prom{{Prometheus}}
        Graf[Grafana]
        Mail[MailHog]
        DD[Datadog Agent]
    end

    Prom -->|scrape /metrics every 15s| Services
    Prom -->|scrape /metrics| GW
    Graf -->|query| Prom
    Graf -->|circuit breaker alert email| Mail
    DD -.->|collect JSON logs via Docker socket| Services
    DD -.->|collect JSON logs| GW
```

## Sinhrona komunikacija

- Gateway rutira REST pozive klijenta ka odgovarajućem servisu (`/transactions/**` →
  TransactionService, `/esg/**` → ESGService, `/categories/**` i
  `/emission-factors/**` → ReferenceDataService, `/reports/**` → ReportService,
  `/notifications/**` → NotificationService) — destinacije se **ne** čitaju iz
  statičkog fajla, već se svakih 10s osvežavaju iz Consul-ovog health kataloga (vidi
  "Service discovery" ispod).
- ESGService sinhrono zove ReferenceDataService (`GET /categories/{category}`) da
  dobije CO2 faktor. Ovaj poziv je obmotan Polly retry (3 pokušaja, eksponencijalni
  back-off), circuit breaker i timeout politikom — kada je ReferenceDataService
  nedostupan, circuit se otvara i ESGService odmah vraća/upisuje status
  `temporarily_unavailable` umesto da blokira ili propagira grešku. Timeout je
  namerno implementiran kao Polly-jeva sopstvena strategija (`AddTimeout`), a ne kao
  `HttpClient.Timeout` — ovo poslednje baca običan `TaskCanceledException` koji
  Polly-jev podrazumevani predikat ne prepoznaje kao tranzijentnu grešku, pa bi
  retry/circuit breaker tiho nikad ne bili aktivirani.

## Asinhrona komunikacija (RabbitMQ, MassTransit)

| Event | Publisher | Consumer(i) |
| --- | --- | --- |
| `TransactionCreatedEvent` | TransactionService | ESGService, ReportService |
| `EsgCalculatedEvent` | ESGService | NotificationService, ReportService |

Event-driven pristup omogućava da ReportService gradi sopstveni read-model bez
sinhronih poziva ka drugim servisima (svaki servis ostaje vlasnik svojih podataka), a
NotificationService reaguje na ESG rezultate bez direktne zavisnosti od ESGService-a.

## Service discovery (Consul)

Svaki od 5 business servisa se registruje u Consul pri startu
(`GreenFinance.ServiceDiscovery` building block — `ConsulRegistrationHostedService`),
sa HTTP health check-om ka svom `/health` endpoint-u (interval 10s). Registracija je
namerno **ne-fatalna**: ako Consul privremeno nije dostupan, servis i dalje starta
normalno (samo loguje upozorenje i pokušava ponovo) — ovo drži integracione testove
(koji ne pokreću Consul) jednostavnim (`Consul:Enabled=false`). Adresa pod kojom se
servis registruje je kontejnerov hostname (`Dns.GetHostName()`), jedinstven po
instanci — ovo je ono što omogućava da više replika istog servisa (vidi
"Skaliranje" ispod) registruje odvojene, razlikovane unose u Consul katalogu umesto
da se prepisuju jedna preko druge.

ApiGateway ne koristi statičku `appsettings.json` `ReverseProxy` konfiguraciju — umesto
toga, custom `IProxyConfigProvider` (`ConsulProxyConfigProvider` +
`ConsulProxyRefreshHostedService`) svakih 10s upita Consul-ov `/v1/health/service/{name}`
za svaki od 5 servisa i dinamički gradi YARP cluster destinacije samo od instanci koje
su trenutno "passing". Kad se servis ugasi, njegova ruta u gateway-u vraća 503 u roku
od jednog refresh ciklusa; kad se vrati, saobraćaj se automatski nastavlja. Svaki
klaster ima eksplicitno postavljenu `RoundRobin` load balancing politiku, tako da se
saobraćaj ravnomerno raspoređuje kad klaster ima više od jedne zdrave destinacije.

Consul UI: `http://localhost:8500`.

### Skaliranje (horizontalno, load balancing)

`TransactionService` i `ESGService` su podešeni da rade kao više instanci
(`docker compose --scale`) — mehanizam je generički (radi za bilo koji od 5
servisa), demonstriran konkretno na ova dva kao najzahtevnijim putanjama (ulazna
tačka sistema i sinhroni hot-path servis). Skalirane instance nemaju fiksni
host-port mapping niti `container_name` u `deploy/docker-compose.yml` (Compose to
zahteva za `--scale`) — dostupne su isključivo preko gateway-a. Detalji i primeri
komandi: [`deployment.md`](deployment.md).

## Monitoring i alarmiranje (Prometheus, Grafana)

Svih 5 servisa + ApiGateway izlažu `/metrics` (OpenTelemetry + Prometheus exporter,
`GreenFinance.Observability` building block): ugrađene ASP.NET Core/HttpClient/runtime
metrike, MassTransit metrike, i custom business brojači
(`transactions.created`, `esg.results.calculated`/`esg.results.unavailable`,
`notifications.sent`) i gauge za stanje ESGService → ReferenceDataService circuit
breaker-a (`esg.referencedata.circuit_state`: 0=zatvoren, 1=otvoren, 2=poluotvoren),
ažuriran preko Polly `OnOpened`/`OnClosed`/`OnHalfOpened` callback-ova.

Prometheus (`http://localhost:9090`) skrejpuje svih 6 `/metrics` endpoint-a na 15s.
Grafana (`http://localhost:3000`, admin/admin) je provisioned sa Prometheus
datasource-om, starter dashboard-om ("GreenFinance Overview") i jednim alert
pravilom: kad `esg_referencedata_circuit_state` pređe 0 (otvoren), Grafana šalje email
alert preko SMTP-a ka MailHog-u (`http://localhost:8025` — lokalni SMTP catcher, ne
šalje prave mejlove). Ovo je konkretna realizacija "mail bazirani notifikacioni
kanali" + "alarm na circuit breaker-om" bonus stavki iz predloga.

## Centralizovano logovanje (Datadog)

Svi servisi loguju strukturisani JSON na stdout (`builder.Logging.AddJsonConsole()`).
Datadog Agent kontejner (montira Docker socket) kupi logove svih kontejnera
(`DD_LOGS_CONFIG_CONTAINER_COLLECT_ALL`) i taguje ih po servisu preko
`com.datadoghq.ad.logs` Docker labela. Zahteva pravi `DD_API_KEY` u lokalnom
`deploy/.env` (vidi [`deployment.md`](deployment.md)) — bez njega agent starta
normalno ali ne uspeva da isporuči telemetriju Datadog-u.

## Kontejnerizacija

Svaki servis ima svoj `Dockerfile` (multi-stage build: SDK image za restore/publish,
ASP.NET runtime image za pokretanje). `deploy/docker-compose.yml` orkestrira:

- 5 mikroservisa + ApiGateway
- 1 SQL Server 2022 kontejner (5 logičkih baza)
- 1 RabbitMQ (management) kontejner
- Consul (service discovery)
- Prometheus + Grafana + MailHog (monitoring i alarmiranje)
- Datadog Agent (centralizovano logovanje)

Detalji pokretanja i CI/CD pipeline-a nalaze se u [`deployment.md`](deployment.md).
