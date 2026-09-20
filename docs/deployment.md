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

Ovo podiže: SQL Server, RabbitMQ, svih 5 mikroservisa i API Gateway. Nakon starta:

- Gateway: `http://localhost:8080`
- RabbitMQ management UI: `http://localhost:15672` (guest/guest)
- Pojedinačni servisi (za debug, mimo gateway-a): `8081`–`8085`

Primer end-to-end provere:

```bash
curl -X POST http://localhost:8080/transactions \
  -H "Content-Type: application/json" \
  -d '{"companyId":12,"category":"Fuel","amount":5000,"currency":"EUR","date":"2026-09-20"}'

curl http://localhost:8080/esg/transaction/<vraceni-id>
curl "http://localhost:8080/reports/company/12?month=9&year=2026"
```

Gašenje i čišćenje:

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml down
```

## 3. CI pipeline (`.github/workflows/ci.yml`)

Pokreće se na svaki Pull Request i push ka `main`/`develop`:

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

Pokreće se na push ka `develop` (DEV faza) i `main` (PROD faza):

1. **Build & Test** — isti koraci kao u CI (restore/build/unit/integration testovi)
2. **Build & Push Docker Images** — svaki servis se builduje i push-uje na GitHub
   Container Registry (`ghcr.io/<org>/<repo>/<servis>`), sa tagovima
   `dev-<sha>`/`prod-<sha>` i `develop-latest`/`main-latest`
3. **Deploy to DEV** (samo na `develop`) / **Deploy to PROD** (samo na `main`) —
   trenutno ispisuje instrukcije za deploy; da bi zaista deploy-ovao na server
   potrebno je:
   - dodati `DEV_HOST`/`DEV_SSH_KEY` (odn. `PROD_HOST`/`PROD_SSH_KEY`) GitHub Actions
     secrets za ciljni VPS/Azure Container Apps
   - dodati korak koji se (npr. preko `appleboy/ssh-action`) poveže na taj host i
     izvrši `docker compose -f deploy/docker-compose.yml pull && docker compose -f deploy/docker-compose.yml up -d`
     sa odgovarajućim image tagovima

Ovim je pipeline strukturiran po fazama (dev/prod) i spreman da se poveže na stvarnu
infrastrukturu bez menjanja build/test/push logike.
