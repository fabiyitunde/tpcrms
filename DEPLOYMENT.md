# Deployment — AWS EC2 via GitHub Actions

CRMS deploys **two .NET 9 apps as two Docker containers** to a single EC2 instance. A push to
`master` (or a manual run) builds both images **on the instance** and starts them:

| App | Container | Host port | URL |
|-----|-----------|-----------|-----|
| `CRMS.API` (REST API) | `crms-api` | **8980** | `http://<EC2_HOST>:8980` (Scalar docs at `/scalar/v1`) |
| `CRMS.Web.Intranet` (Blazor Server) | `crms-intranet` | **8981** | `http://<EC2_HOST>:8981` (redirects to `/login`) |

Both apps share **one MySQL database** and therefore **one connection string**, supplied as a single
GitHub secret.

| File | Role |
|------|------|
| `Dockerfile.api` | Multi-stage build of `CRMS.API` (SDK 9 → ASP.NET 9 runtime). Listens on `8080`. |
| `Dockerfile.intranet` | Multi-stage build of `CRMS.Web.Intranet`. Listens on `8080`. |
| `docker-compose.yml` | Two services (`8980`/`8981` → `8080`), shared `.env`, shared `crms-storage` volume. |
| `.github/workflows/deploy.yml` | Checkout → write `.env` from the secret → tar/scp to EC2 → install Docker if missing → `docker compose up -d --build` → health-check both ports. |
| `.dockerignore` | Keeps the build context lean; keeps env-specific appsettings and `global.json` out of the image. |

## Required GitHub secrets

Set these in **Settings → Secrets and variables → Actions**:

| Secret | Example / notes |
|--------|-----------------|
| `EC2_HOST` | Public IP or DNS of the instance, e.g. `13.51.x.x` |
| `EC2_USER` | SSH user, e.g. `ec2-user` (Amazon Linux) or `ubuntu` |
| `EC2_SSH_KEY` | The **private** key (full PEM contents) matching the instance's authorized key |
| `DEFAULTDB_CONNECTION` | The full **MySQL** connection string, shared by both apps (see below) |

### `DEFAULTDB_CONNECTION` format

This is the apps' `ConnectionStrings:DefaultConnection`. Use a Pomelo/MySQL connection string, e.g.:

```
Server=<db-host>;Database=crmsdb;Uid=<user>;Pwd=<password>;AllowLoadLocalInfile=True;
```

The workflow writes it **verbatim** into `.env` as `ConnectionStrings__DefaultConnection=...` and both
containers read it via `env_file`. An env var overrides any value in `appsettings.json`, so this is the
single source of truth for the DB in production. The MySQL server must be reachable **from the EC2
instance**.

> The connection string carries the DB password — it lives **only** in the GitHub secret, never in the
> repo. `appsettings.Production.json` is git-ignored and is deliberately **not** shipped into the image.

## One-time instance prerequisites (outside this workflow)

The SSH-based workflow can't change AWS infrastructure. Before the first deploy:

1. **Security group** — open inbound **TCP 8980 and 8981** from wherever you'll access the apps.
2. **DB reachability** — the MySQL server in `DEFAULTDB_CONNECTION` must be reachable from the EC2
   instance (security groups / VPC / firewall). The apps connect to the DB at startup
   (`ServerVersion.AutoDetect`) and again to migrate/seed, so an unreachable DB makes the containers
   crash-loop.
3. **SSH key** — the public half of `EC2_SSH_KEY` is in the instance's `~/.ssh/authorized_keys`.
4. **Instance size** — building two .NET images needs memory; use **≥ 2 GB RAM** (t3.small or larger).

Docker + the Compose plugin are installed automatically on first run if missing.

## How to deploy

- **Automatic:** push to `master`.
- **Manual:** Actions tab → *Deploy to EC2* → *Run workflow*.

### First login

In Production only roles, locations, and a **bootstrap admin** are seeded (loan products and committees
are configured by the admin via the UI — the app will look empty until then). Log in to the Intranet at
`http://<EC2_HOST>:8981/login` with:

```
Email:    admin@crms.ng
Password: Admin@CRMS2026!
```

**Change this password immediately after first login.**

## Behavioural notes / caveats

- **Startup migrations are serialized.** Both apps run EF migrations + seeding on boot against the same
  DB. The Intranet is gated (`depends_on: service_healthy`) on the API being ready, so the API
  migrates/seeds first and the Intranet's run is a no-op. Trade-off: if the API is unhealthy, the
  Intranet won't start. On the very first boot the API's health gate allows up to ~240s for seeding.
- **Background services run in both apps.** `NotificationProcessingService`, `CreditCheckBackgroundService`,
  and `CsMonitoringBackgroundService` are registered in shared Infrastructure, so each runs **twice**
  (once per app) against the one DB. If you observe duplicate notifications or double-processed credit
  checks, that's the cause — consider disabling these hosted services in one of the two apps.
- **Uploaded documents** persist in the shared `crms-storage` Docker volume (`/app/storage`) and survive
  redeploys (`docker compose down` without `-v` keeps it). Both apps read/write the same volume.
- The Intranet calls `UseHttpsRedirection()` unconditionally; with HTTP-only containers it logs one
  benign "Failed to determine the https port" warning and serves over HTTP normally.

## Logs / troubleshooting

```bash
cd /opt/crms
sudo docker compose logs -f crms-api        # API logs
sudo docker compose logs -f crms-intranet   # Intranet logs
sudo docker ps                              # container status (look for "healthy")
cat /tmp/crms-build.log                     # last build output
```

- `Connection string 'DefaultConnection' not found` → `.env` missing/empty (check the secret).
- Container restarts in a loop right after build → DB unreachable from EC2, or wrong credentials.
- Intranet never starts → the API never became healthy; check `crms-api` logs first.
- Can't reach a site at all → security group 8980/8981 not open, or the container isn't running.

### Verifying the connection string reached the container

```bash
cd /opt/crms
sudo docker compose exec crms-api printenv ConnectionStrings__DefaultConnection
```

If the value doesn't match the secret byte-for-byte (only a concern if the password contains `$` and the
installed Compose version interpolates `env_file`), supply it instead via a mounted
`appsettings.Production.json`.
