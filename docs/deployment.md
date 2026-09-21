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

Ovo podiže: SQL Server, RabbitMQ, Consul, Redis, svih 5 mikroservisa, API Gateway, i
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

Provera Redis keša (emisijski faktori, ReferenceDataService):

```bash
curl http://localhost:8081/categories/Fuel   # prvi poziv puni keš
docker exec greenfinance-redis redis-cli KEYS '*'
docker exec greenfinance-redis redis-cli HGETALL "emission-factor:Fuel"
```

### Service mesh (Consul Connect) — ESGService → ReferenceDataService

Scoped na jedini sinhroni servis-servis poziv u sistemu — vidi
[`architecture.md`](architecture.md#service-mesh-consul-connect--scoped-na-esgservice--referencedataservice)
za obim i mehanizam. Provera da je saobraćaj stvarno mTLS (SPIFFE identitet
izdat od Consul-ove Connect CA, ne plain HTTP passthrough preko sidecar-a):

```bash
docker run --rm --network container:greenfinance-reference-data-service curlimages/curl:latest \
  -s http://localhost:19000/certs   # pokazuje SPIFFE URI cert_chain-a (svc/reference-data-service)

# generiši malo saobraćaja pa proveri da handshake brojač raste
curl -X POST http://localhost:8080/transactions -H "Content-Type: application/json" \
  -d '{"companyId":1,"category":"Fuel","amount":100,"currency":"EUR","date":"2026-09-20"}'
docker run --rm --network container:greenfinance-reference-data-service curlimages/curl:latest \
  -s http://localhost:19000/stats | grep ssl.handshake
```

Provera da su intentions stvarno primenjene (default-deny + eksplicitni allow,
`deploy/consul/intentions/*.hcl`):

```bash
# privremeno promeni Action u 01-esg-to-referencedata.hcl na "deny", pa:
docker compose --env-file deploy/.env -f deploy/docker-compose.yml run --rm consul-intentions-init

# POST /transactions (kao gore) → GET /esg/transaction/{id} sada vraća
# "temporarily_unavailable" (Polly circuit breaker se otvara —
# esg_referencedata_circuit_state metrika ide na 1)

# vrati Action na "allow", ponovo pokreni consul-intentions-init, potvrdi oporavak
```

**Napomena**: `esg-service`/`esg-service-sidecar` dele network namespace preko
`network_mode: "service:esg-service"` — restartovanje `esg-service` kontejnera
samostalno (npr. `docker restart`) može privremeno prekinuti sidecar-ovu DNS
rezoluciju ka `consul`; ako se to desi, restartuj i `esg-service-sidecar`. Ovo je
poznata Docker specifičnost deljenih network namespace-ova, ne bag u mesh
konfiguraciji — normalan `docker compose up`/`restart` na oba servisa zajedno
(ili ceo stack) ovo ne pogađa.

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

## 6. Kubernetes (Helm) — lokalni klaster

Alternativna, **pojednostavljena** putanja za pokretanje sistema — namenjena
lokalnom razvoju/demo-u (minikube ili kind), ne pravom cloud klasteru niti CI/CD
automatizaciji. `docker-compose.yml` ostaje primarni put za pun bonus stack
(Consul service discovery + Connect mesh, skaliranje preko `--scale`, Datadog);
ovaj Helm chart namerno **ne koristi Consul** — servisi se pronalaze preko
nativnih Kubernetes Service DNS imena (npr. `http://reference-data-service:8080/`
direktno), a Datadog centralizovano logovanje je van obima (Docker-socket
pattern ne mapira se čisto na Kubernetes — pravo rešenje tamo bi bio zvaničan
Datadog Helm chart + Cluster Agent, drugačiji mehanizam). Vidi
[`architecture.md`](architecture.md#kubernetes-deployment-pojednostavljena-putanja)
za punu sliku razlika.

### Preduslovi

- Lokalni Kubernetes klaster: [minikube](https://minikube.sigs.k8s.io/) ili
  [kind](https://kind.sigs.k8s.io/) (sa podrazumevanim StorageClass-om
  omogućenim — minikube: `minikube addons enable storage-provisioner`; kind ga
  ima uključenog po difoltu preko `rancher.io/local-path`).
- [Helm 3+](https://helm.sh/) i `kubectl`.

### Instalacija

```bash
helm install greenfinance deploy/helm/greenfinance -n greenfinance --create-namespace
```

Ovo po difoltu koristi već objavljene GHCR image-e (`develop-latest`, isti koje
`cd.yml` push-uje na svaki push ka `develop`) — najmanje trenja za brzo
probanje. **Bitno**: `develop-latest` sadrži samo kod koji je stvarno
merge-ovan u `develop` — ako testiraš chart sa neke `feature/DIS-XX-...`
grane koja *još nije* tamo (npr. ceo Kubernetes rad je namerno ostao na
sopstvenoj grani dok se ne merge-uje ručno), gateway/servisi će pokrenuti
**stariji** kod bez tvojih K8s-specifičnih izmena (npr. static YARP
ReverseProxy grananje) i ponašaće se pogrešno (npr. gateway pokušava Consul
koji u K8s putanji ne postoji → 503) — ne zato što je chart pogrešan, nego
zato što image ne odgovara trenutnom source-u. Za testiranje svog trenutnog
(necommit-ovanog ili ne-merge-ovanog) koda, izgradi image-e lokalno i uputi
Helm da ih koristi:

```bash
REPO=ghcr.io/zorz98/dis_i7_10_2025_djordje_sevic
docker build -t $REPO/reference-data-service:local -f src/Services/ReferenceDataService/ReferenceDataService.Api/Dockerfile .
docker build -t $REPO/transaction-service:local -f src/Services/TransactionService/TransactionService.Api/Dockerfile .
docker build -t $REPO/esg-service:local -f src/Services/ESGService/ESGService.Api/Dockerfile .
docker build -t $REPO/report-service:local -f src/Services/ReportService/ReportService.Api/Dockerfile .
docker build -t $REPO/notification-service:local -f src/Services/NotificationService/NotificationService.Api/Dockerfile .
docker build -t $REPO/api-gateway:local -f src/ApiGateway/Dockerfile .

helm upgrade --install greenfinance deploy/helm/greenfinance -n greenfinance --create-namespace \
  --set image.tag=local --set image.pullPolicy=Never
```

(Svaki `docker build` gore koristi repo koren kao build context — pokreni ih iz
korenskog direktorijuma projekta, isto kao `docker compose build`.)

(Docker Desktop-ov Kubernetes deli isti Docker daemon kao `docker build`, pa
nije potreban poseban "load" korak kao za kind/minikube — za te alate koristi
`kind load docker-image <image>:<tag> --name <cluster>` odn. `minikube image
load` posle build-a.)

Provera statusa:

```bash
kubectl get pods -n greenfinance
```

Prva instalacija na potpuno praznu bazu može potrajati do ~2 minuta pre nego
što business servisi postanu `Ready` (`Database.Migrate()` se izvršava inline
pri startu — `startupProbe` u chart-u ovo pokriva; ako tvoj lokalni klaster je
resursno slab pa prvi boot premaši ~2min, podigni
`probes.startup.failureThreshold` preko `--set`).

### Pristup gateway-u

```bash
kubectl port-forward -n greenfinance svc/gateway 8080:8080
```

Ovo tačno reprodukuje `http://localhost:8080` iz docker-compose sveta, bez
ikakvog dodatnog podešavanja klastera (radi identično na minikube i kind).
Alternative (pomenute, ne default): `NodePort` servis (`minikube service
gateway`, radi samo ako je kind pokrenut sa `extraPortMappings`), ili Ingress
kontroler (zahteva dodatni addon — nepotrebna komplikacija za jednu rutu).

Primer end-to-end provere (identičan primer kao za docker-compose, §2):

```bash
curl -X POST http://localhost:8080/transactions \
  -H "Content-Type: application/json" \
  -d '{"companyId":12,"category":"Fuel","amount":5000,"currency":"EUR","date":"2026-09-20"}'

curl http://localhost:8080/esg/transaction/<vraceni-id>
curl "http://localhost:8080/reports/company/12?month=9&year=2026"
```

### Faze (dev/uat/prod)

`values-dev.yaml`/`values-uat.yaml`/`values-prod.yaml` postoje radi
konzistentnosti sa dev/uat/prod fazu-po-granu pričom iz §4, ali pošto ovaj
chart cilja **jedan lokalni klaster** (ne tri odvojena okruženja), razlika je
namerno tanka — samo `image.tag` (`develop-latest`/`uat-latest`/`main-latest`):

```bash
helm install greenfinance deploy/helm/greenfinance -f deploy/helm/greenfinance/values-uat.yaml \
  -n greenfinance --create-namespace
```

Ovo samo preusmerava tvoj jedan lokalni klaster da pokrene image-e objavljene
za `uat` granu — ne simulira stvarno odvojenu UAT infrastrukturu.

### Skaliranje (opciono, tek posle DIS-48 fix-a)

Svi business servisi imaju `replicas: 1` po difoltu — namerno, da se izbegne
poznata EF Core migration rasa kad više replika istog servisa startuje
istovremeno na praznoj bazi (rešeno u `feature/DIS-48-migration-retry-on-scale`
kroz retry sa backoff-om na `Database.Migrate()`). Kad je taj fix potvrđeno na
`develop`-u, bezbedno je povećati replike preko `--set
services.transaction-service.replicas=3` (K8s Service već radi load balancing
preko kube-proxy-a, YARP-u nije potrebna sopstvena dinamička LB logika za ovu
putanju).

### Šta NIJE deo ovog chart-a (namerne odluke)

- **Consul service discovery/mesh** — nativni K8s Service/DNS umesto toga.
- **Datadog centralizovano logovanje** — drugačiji mehanizam bi bio potreban
  na K8s-u (zvaničan Datadog Helm chart + Cluster Agent), van obima.
- **CI/CD automatizacija** — `cd.yml`-ovi "Deploy to DEV/UAT/PROD" koraci
  ostaju placeholder-i; zamena tih echo koraka sa `helm upgrade --install`
  pozivima bi bio prirodan sledeći korak, ali nije implementiran ovde.

### Struktura chart-a

`deploy/helm/greenfinance/` — jedan parametrizovan chart: generički
`app-deployment.yaml`/`app-service.yaml` par (range nad `.Values.services`) za
5 business servisa, sopstveni template-i za gateway (static YARP
`ReverseProxy` konfiguracija umesto Consul-a — vidi
`src/ApiGateway/appsettings.Kubernetes.json`), `StatefulSet` za SQL Server (PVC
za stvarne aplikacione podatke), plain `Deployment`-i za RabbitMQ/Redis/
Prometheus/Grafana/MailHog. Prometheus/Grafana provisioning fajlovi su ručno
mirror-ovani u `deploy/helm/greenfinance/files/` iz `deploy/prometheus.yml` i
`deploy/grafana/**` (Helm ne može referencirati fajlove van chart
direktorijuma) — komentari na oba mesta upućuju jedno na drugo; ažuriraj obe
kopije ako menjaš scrape target-e ili dashboard-e.
