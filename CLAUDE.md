# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build (the solution file is CRMS.slnx, not .sln)
dotnet build CRMS.slnx

# Tests. 2 ComprehensiveDataSeederTests fail on a clean checkout — a known, pre-existing
# failure, not something you broke.
dotnet test CRMS.slnx --filter "FullyQualifiedName!~LiveIntegration"

# Everything, including tests that need live AWS / SmartComply / Core Banking credentials
# and fail without them (~26 more failures).
dotnet test CRMS.slnx

# One test or class
dotnet test CRMS.slnx --filter "FullyQualifiedName~RhshfOfferTests"

# Run the apps (intranet 5292, API 5230)
dotnet run --project src/CRMS.Web.Intranet --urls http://localhost:5292
dotnet run --project src/CRMS.API --urls http://localhost:5230
```

A running app locks the build output. Stop both before building, or the build fails with
MSB3021 "being used by another process".

### EF Core migrations

The global `dotnet-ef` is 8.x and will not work against this 9.0.2 model. Use the local tool
(`.config/dotnet-tools.json`), with `CRMS.API` as the startup project:

```bash
dotnet dotnet-ef migrations add <Name> -p src/CRMS.Infrastructure -s src/CRMS.API
dotnet build CRMS.slnx          # REQUIRED between scaffold and update — see below
dotnet dotnet-ef database update -p src/CRMS.Infrastructure -s src/CRMS.API
```

**`database update --no-build` after scaffolding silently applies nothing and still prints
"Done."** The command reads migrations from the compiled assembly, which does not yet contain
the file you just generated. Always build in between, and confirm an "Applying migration" line
actually appears rather than trusting the exit code.

**Migrations must be additive.** A migration once wiped production data (incident 2026-06-15);
`scripts/check-destructive-migrations.sh` now runs in CI and fails the build on `DELETE FROM`,
`TRUNCATE`, `DROP TABLE` or `DropTable(` in an `Up()`. Note it deliberately does *not* catch
`DropColumn` — and un-mapping a property makes EF emit exactly that, so review generated
migrations by eye rather than relying on the scanner.

A hand-written migration needs a companion `.Designer.cs`, or the `[Migration]` attribute is
missing and `MigrateAsync()` skips it without a word.

## Architecture

Four layers — `CRMS.Domain` → `CRMS.Application` → `CRMS.Infrastructure` → hosts
(`CRMS.Web.Intranet`, `CRMS.API`). `CRMS.Web.Portal` is an unused scaffold; it is not deployed
and still contains template pages.

### Three loan tracks, deliberately separate

The system runs three products that look similar and share almost nothing:

| Track | Aggregate | Staff UI | Route |
|---|---|---|---|
| Corporate | `LoanApplication` | `Pages/Applications/` | `/applications/{id}` |
| NAMP (equipment) | `NampApplication` | `Pages/Namp/` | `/namp/{id}` |
| RH-SHF (dry-season inputs) | `RhshfCreditProfile` | `Pages/Rhshf/` | `/rhshf/detail/{reference}` |

Before generalising across them, check whether the behaviour really is shared. The committee is
the one genuinely shared piece — and only between Corporate and NAMP. **NAMP creates its
`CommitteeReview` rows with `LoanApplicationId = Guid.Empty`**, because a NAMP application is not
a `LoanApplication`; the application *number* is the only link back. Anything reading a committee
review has to handle both. RH-SHF is not involved at all: it has its own `RhshfCommitteeReview`.

### Request handlers

`IRequestHandler` is hand-rolled (`Application/Common/IRequestHandler.cs`) — there is no MediatR
and no dispatcher. Handlers are resolved by concrete type from the service provider and invoked
directly. **Every handler must be registered by hand in `Infrastructure/DependencyInjection.cs`**
(~470 registrations, no assembly scanning). A missing registration surfaces as a swallowed
exception inside `ApplicationService`, not a startup error.

### The intranet host

`CRMS.Web.Intranet` serves two different audiences from one app:

- **Staff**: Blazor Server, interactive, under `Components/Pages/`.
- **FACs** (RH-SHF applicants): Razor Pages under `Pages/Rhshf/`, token-gated, no Blazor circuit.
  This is the credit-profiling form external users fill in.

`Services/ApplicationService.cs` (~8,000 lines) is the facade every Blazor page calls. It resolves
handlers from `IServiceProvider` and wraps each call in try/catch. Two consequences:

- **Never `Task.WhenAll` over `ApplicationService` calls.** They share one scoped `DbContext` per
  circuit; concurrent use returns silently empty results. Load sequentially (see the comment in
  `Rhshf/Detail.razor`).
- Case pages load their data once in `OnInitializedAsync` and are not told when anything changes
  underneath them. Anything that *acts* on loaded data — importing a document, say — must re-fetch
  first, or it operates on a snapshot from whenever the page was opened.

Auth lives in the Blazor circuit, not a server cookie. A hard navigation to a page carrying
`[Authorize]` therefore 302s to `/login` and lands on the dashboard, losing the destination;
in-app navigation works normally. When testing, click through the menu — do not paste URLs, and do
not conclude from a `curl` 200 that a page renders.

### Persistence traps

`CRMSDbContext.SaveChangesAsync` contains a compensating block that flips append-only child
entities from `Modified` to `Added`. It exists because `Entity` assigns a non-default GUID in its
constructor, so EF treats a child added to a loaded aggregate's collection as an update to a row
that does not exist — failing with *"expected to affect 1 row(s), but actually affected 0"*.
**Every new append-only child type must be added to that block**, or appending to it breaks at
runtime with no compile-time hint.

Repositories eager-load their aggregates and must use `AsSplitQuery()`. `RhshfCreditProfile` has
13 collection navigations; in a single statement those multiply into a cartesian product that EF
de-duplicates client-side, which was the dominant cause of slow page loads.

Enums are persisted **by name** (`HasConversion<string>()`). Renaming a member orphans existing
rows, and converting a free-text column to an enum throws on read of any legacy value.

### Seeding

`SeedData.SeedAsync` runs at startup and calls the NAMP and RH-SHF workflow seeders, which create
stage/routing/checklist configuration. These must stay idempotent and add-missing rather than
all-or-nothing: an environment seeded last month still needs today's new rows. Back-fills follow
the same rule — only touch rows that have no value, so an administrator's choice is never
overwritten by a deploy.

Migrations run at startup too, gated by `Database:AutoMigrate` (default true, settable as
`Database__AutoMigrate`).

## Configuration

`appsettings.json` points at the shared non-prod database and **does not** set the core-banking
services to mock. `appsettings.Development.json` overrides the connection string to localhost but
not the integrations — so a local run hits the live sandbox for Fineract/Core Banking calls. Treat
booking and product-catalogue calls as real.

## Deploy

Pushing to `master` triggers `.github/workflows/deploy.yml` → migration-safety scan → EC2 over
SSH, docker compose, health check. Production is published separately and manually.

The deploy clears `/opt/crms` before extracting (keeping `.env`), because it previously extracted
over the old checkout and files deleted in git lingered on the server and broke the Docker build.
If a deploy fails to produce containers, the build log on the box is the place to look — the
build step pipes through `tee`, so a compile failure does not surface as a failed step.
