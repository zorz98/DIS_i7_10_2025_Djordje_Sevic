# Deployment i CI/CD pipeline — GreenFinance

## Preduslovi

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (za lokalni razvoj/testove)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Docker Engine +
  Docker Compose v2)

## 1. Lokalni razvoj (bez Dockera)

Pokretanje pojedinačnog servisa direktno preko .NET SDK-a (npr. za debug u IDE-u),
uz pretpostavku da SQL Server i RabbitMQ već rade lokalno ili u kontejnerima:

```bash
dotnet run --project src/Services/TransactionService/TransactionService.Api
```

Connection string i RabbitMQ podešavanja čitaju se iz `appsettings.json` /
`appsettings.Development.json` svakog servisa (podrazumevano `localhost`).

Build i testovi celog rešenja:

```bash
dotnet build GreenFinance.slnx
dotnet test GreenFinance.slnx --filter "FullyQualifiedName~UnitTests"        # unit testovi
dotnet test GreenFinance.slnx --filter "FullyQualifiedName~IntegrationTests" # integracioni testovi (zahtevaju pokrenut Docker za Testcontainers)
```

## 2. Pokretanje celog sistema (Docker Compose)

```bash
cp deploy/.env.example deploy/.env
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up --build
```

Ovo podiže: SQL Server, RabbitMQ, Consul, svih 5 mikroservisa, API Gateway, i
observability stack (Prometheus, Grafana, MailHog, Datadog Agent). Nakon starta:

- Gateway: `http://localhost:8080`
- RabbitMQ management UI: `http://localhost:15672` (guest/guest)
- Pojedinačni servisi (za debug, mimo gateway-a): ReferenceDataService `8081`,
  ReportService `8084`, NotificationService `8085`. TransactionService i ESGService
  **nemaju** fiksni host port (namerno — vidi "Skaliranje servisa" ispod) i dostupni
  su samo preko gateway-a ili `docker compose exec`.
- Consul UI (service discovery katalog/health): `http://localhost:8500`
- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000` (admin/admin) — dashboard "GreenFinance Overview"
- MailHog (inbox za alert email-ove): `http://localhost:8025`

Za centralizovano logovanje preko Datadog-a, dodaj svoj pravi API ključ u
**lokalni** `deploy/.env` (nikad u `.env.example`, koji se prati git-om):

```
DD_API_KEY=<tvoj-datadog-api-key>
DD_SITE=datadoghq.com   # ili eu/us3/us5/ap1 zavisno od regiona naloga
```

Bez validnog ključa, Datadog Agent i dalje normalno starta (i lokalno je vidljiv
preko `docker logs greenfinance-datadog-agent`), samo neće uspeti da isporuči
telemetriju ka Datadog-u.

Primer end-to-end provere:

```bash
curl -X POST http://localhost:8080/transactions \
  -H "Content-Type: application/json" \
  -d '{"companyId":12,"category":"Fuel","amount":5000,"currency":"EUR","date":"2026-09-20"}'

curl http://localhost:8080/esg/transaction/<vraceni-id>
curl "http://localhost:8080/reports/company/12?month=9&year=2026"
```

Provera circuit breaker alarma (Grafana → email preko MailHog):

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml stop reference-data-service
# napravi par POST /transactions zahteva (kao gore) da ESGService pokuša da
# pozove ReferenceDataService i circuit se otvori (esg_referencedata_circuit_state=1)
# posle ~1 min proveri http://localhost:8025 (MailHog) — treba da stigne "FIRING" email
docker compose --env-file deploy/.env -f deploy/docker-compose.yml start reference-data-service
```

### Skaliranje servisa (load balancing preko gateway-a)

`TransactionService` (ulazna tačka, najviše pisanja) i `ESGService` (sinhroni
hot-path servis) su podešeni da rade kao više instanci iza YARP gateway-a — svaka
instanca se registruje u Consul sa sopstvenom, jedinstvenom adresom
(kontejnerov hostname), a gateway ih otkriva preko istog dinamičkog
Consul-catalog mehanizma iz DIS-25 i raspoređuje saobraćaj round-robin politikom.
Mehanizam je generički i radi identično za bilo koji od 5 servisa — ova dva su
izabrana kao konkretna demonstracija.

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d --build \
  --scale transaction-service=3 --scale esg-service=2
```

Skalirani servisi nisu dostupni na fiksnom host portu (samo preko gateway-a,
`http://localhost:8080`) — direktan debug pristup pojedinačnoj instanci ide preko
`docker compose exec transaction-service curl -s localhost:8080/health` ili
`docker port <container>`.

Provera load balancing-a:

```bash
# 1) Consul UI (http://localhost:8500) treba da pokaže 3 odvojena healthy unosa
#    za transaction-service i 2 za esg-service, svaki sa različitom adresom.

# 2) Ponovljeni zahtevi preko gateway-a treba da se raspodele na sve instance —
#    proveriti preko docker compose logs transaction-service (ili logova
#    pojedinačnih replika, npr. greenfinance-transaction-service-1/2/3).
for i in 1 2 3 4 5 6 7 8 9 10; do
  curl -s -o /dev/null -X POST http://localhost:8080/transactions \
    -H "Content-Type: application/json" \
    -d "{\"companyId\":$i,\"category\":\"Fuel\",\"amount\":100,\"currency\":\"EUR\",\"date\":\"2026-09-20\"}"
done

# 3) Gašenje jedne instance usred saobraćaja — Consul je izbacuje iz healthy liste
#    za ~10s (jedan refresh ciklus), gateway nastavlja da radi bez vidljivih grešaka.
docker stop greenfinance-transaction-service-2
```

Gašenje i čišćenje:

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml down
```

## 3. CI pipeline (`.github/workflows/ci.yml`)

Pokreće se na svaki Pull Request i push ka `main`/`develop`/`uat`:

1. **Checkout** koda
2. **Restore** NuGet paketa (`dotnet restore`)
3. **Build** celog rešenja u `Release` konfiguraciji
4. **Unit testovi** (`*.UnitTests` projekti — xUnit + FluentAssertions + Moq)
5. **Integracioni testovi** (`*.IntegrationTests` projekti — xUnit + Testcontainers;
   GitHub Actions `ubuntu-latest` runner ima Docker preinstaliran, pa Testcontainers
   radi bez dodatnog podešavanja)
6. **Docker build** — svaki servis se builduje kao Docker image (bez push-a), da bi
   se PR validirao i sa te strane pre merge-a

PR se ne treba merge-ovati dok ovaj workflow nije zelen.

## 4. CD pipeline (`.github/workflows/cd.yml`)

Pokreće se na push ka `develop` (DEV faza), `uat` (UAT faza) i `main` (PROD faza):

1. **Build & Test** — isti koraci kao u CI (restore/build/unit/integration testovi)
2. **Build & Push Docker Images** — svaki servis se builduje i push-uje na GitHub
   Container Registry (`ghcr.io/<org>/<repo>/<servis>`), sa tagovima
   `dev-<sha>`/`uat-<sha>`/`prod-<sha>` i `develop-latest`/`uat-latest`/`main-latest`
3. **Deploy to DEV** (samo na `develop`) / **Deploy to UAT** (samo na `uat`) /
   **Deploy to PROD** (samo na `main`) — trenutno ispisuje instrukcije za deploy; da
   bi zaista deploy-ovao na server potrebno je:
   - dodati `DEV_HOST`/`DEV_SSH_KEY` (odn. `UAT_HOST`/`UAT_SSH_KEY`,
     `PROD_HOST`/`PROD_SSH_KEY`) GitHub Actions secrets za ciljni VPS/Azure Container
     Apps po fazi
   - dodati korak koji se (npr. preko `appleboy/ssh-action`) poveže na taj host i
     izvrši `docker compose -f deploy/docker-compose.yml pull && docker compose -f deploy/docker-compose.yml up -d`
     sa odgovarajućim image tagovima

`uat` grana služi za prihvatno testiranje pre nego što se promene puste u `main`
(produkciju) — tipičan tok je `develop → uat → main`.

Ovim je pipeline strukturiran po fazama (dev/uat/prod) i spreman da se poveže na
stvarnu infrastrukturu bez menjanja build/test/push logike.

## 5. PR review pipeline (`.github/workflows/pr-review.yml`)

Pokreće se na svaki otvoren/ažuriran Pull Request ka `main`/`develop`/`uat`, preko
zvanične [Claude Code GitHub Action](https://github.com/anthropics/claude-code-action)
(`anthropics/claude-code-action@v1`):

1. Checkout-uje PR (pun git history, `fetch-depth: 0`, da bi `git diff` prema
   base branch-u radio ispravno).
2. Pokreće Claude sa promptom koji dispatch-uje na projekat-specifičan
   `pr-reviewer` subagent (`.claude/agents/pr-reviewer.md`) — checklist prilagođen
   GreenFinance konvencijama (event contracts, EF migracije, MassTransit wiring,
   Docker/compose sinhronizacija, test coverage).
3. Nalaze posta kao **jedan sumarni "sticky" komentar** na PR-u (`use_sticky_comment:
   true`) — svaki novi push ažurira isti komentar umesto da kreira nov.

Zahteva GitHub Actions secret `ANTHROPIC_API_KEY` (Settings → Secrets and variables
→ Actions) sa validnim Anthropic API ključem. Svaki pokrenuti review je pravi API
poziv i ima trošak — po potrebi suziti `types:`/`branches:` filter u workflow-u.
