# SysAssist

SysAssist is a modular enterprise coordination platform for IT events from monitoring and infrastructure sources. It does not replace Zabbix, Grafana, Prometheus, CockroachDB, Redis, Docker, nginx, Linux hosts, or notification tools. It coordinates them: receives events, normalizes incidents, creates local recommendations, checks action risk, sends dangerous actions to approval, records audit history, and exports support bundles.

The project is production-shaped but demo-friendly. It runs locally without real external APIs through fallback mode, keeps CockroachDB as the database target, and documents Google Cloud Run plus CockroachDB Cloud as the production deployment option.

## Stack

- Backend: C# / .NET 9 / ASP.NET Core Web API
- Frontend: React + Vite + TypeScript
- Database: CockroachDB via EF Core and Npgsql
- Auth: JWT bearer wiring
- Logging: Serilog
- Docs: Swagger/OpenAPI
- UI: Tailwind CSS, shadcn-style primitives, Motion/Framer Motion, React Flow, Recharts, TanStack Query, Zustand, Lucide React

## Architecture

SysAssist follows a Clean Architecture layout:

- `SysAssist.Domain`: entities, enums, and core model.
- `SysAssist.Contracts`: DTOs shared across API/application boundaries.
- `SysAssist.Application`: service interfaces and use-case contracts.
- `SysAssist.Infrastructure`: EF Core, CockroachDB, auth, module catalog, adapters, fallback store, and workflow implementation.
- `SysAssist.Api`: ASP.NET Core endpoints, JWT auth, policies, CORS, Swagger, Serilog, correlation ids, and exception handling.
- `SysAssist.Web`: React enterprise dashboard.
- `tests/SysAssist.Tests`: xUnit coverage for hashing, modules, workflow, approvals, audit, and support bundle security.

See [docs/architecture.md](C:/Users/8-Bits/Desktop/Sys/SysAssist/docs/architecture.md:1).

## Why CockroachDB

CockroachDB gives SysAssist a PostgreSQL wire-compatible, horizontally scalable SQL database with transactions and JSONB-friendly storage for payloads, execution results, webhook payloads, logs, and support exports. EF Core uses Npgsql, UUID primary keys, string-backed enums, JSONB columns, and explicit indexes.

The local demo can use in-memory fallback data, but real/deployed mode keeps CockroachDB. SQLite is intentionally not used.

## Why React and Vite

The frontend is an operator console: dense dashboards, searchable event tables, approval queues, module forms, charts, and workflow diagrams. React/Vite keeps local development fast while supporting a production static build served by nginx or Cloud Run.

## Modules

The baseline module registry contains 13 modules:

1. Zabbix
2. Grafana
3. Prometheus Alertmanager
4. PostgreSQL
5. Redis
6. Docker
7. Nginx
8. Linux Host
9. HTTP Endpoint Checker
10. File System Monitor
11. SMTP Email
12. Telegram Bot
13. Local Rule Advisor

Module Registry shows state, health, fallback mode, SafeMode, polling/webhook/action capabilities, and manual operations. Module Settings exposes a dynamic settings form, masks secrets, replaces secrets through a dedicated endpoint, and lists module actions with risk badges.

## Run Backend

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
dotnet restore SysAssist.sln
dotnet run --project src/SysAssist.Api
```

The API uses demo data by default, so CockroachDB is optional for local startup. With the checked-in launch profile, Swagger is available at `http://localhost:5089/swagger` when running in Development, and health is available at `GET /health`.

To connect CockroachDB, copy `src/SysAssist.Api/appsettings.Development.example.json` to `src/SysAssist.Api/appsettings.Development.json`, set `SysAssist:UseDemoData` to `false`, and update `ConnectionStrings:SysAssistDb`.

For real integrations, disable fallback mode only after the required settings for a module are present. Each module validates its required real-mode fields before enabling.

## CockroachDB and EF Core

CockroachDB runs through Docker Compose:

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
docker compose up cockroachdb cockroach-init
```

When `SysAssist:UseDemoData=false` and `SysAssist:ApplyMigrationsOnStartup=true`, the backend applies EF Core migrations and seeds the database during Development startup. Seed data includes the `admin` user, five roles, 13 integration modules, baseline settings/actions, incidents, audit entries, and system logs. A fresh database requires `SysAssist:BootstrapAdminPassword` from `.env` or secret storage to create the initial admin password.

Manual migration commands:

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
dotnet tool restore
dotnet dotnet-ef database update --project src/SysAssist.Infrastructure --startup-project src/SysAssist.Api --context SysAssistDbContext
```

## Auth and Roles

The backend uses JWT Bearer auth. In demo mode and seeded CockroachDB mode, the bootstrap administrator is:

- Login: `admin`
- Password: value from `SysAssist:BootstrapAdminPassword` / `SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD`

Useful checks:

```powershell
$body = @{ login = "admin"; password = $env:SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD } | ConvertTo-Json
$login = Invoke-RestMethod http://localhost:5089/api/auth/login -Method Post -ContentType application/json -Body $body
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
Invoke-RestMethod http://localhost:5089/api/auth/me -Headers $headers
Invoke-RestMethod http://localhost:5089/api/dashboard -Headers $headers
```

Policies are configured as `RequireAdmin`, `RequireOperatorOrHigher`, `RequireEngineerOrHigher`, `RequireSeniorAdmin`, and `RequireAuditorOrAdmin`.

Role overview:

- Admin: platform administration, module enable/disable, users/roles, audit/support access.
- SeniorAdmin: approval authority.
- Engineer: module operations and diagnostics.
- Operator: dashboard, events, notifications, and safe event fetches.
- Auditor: audit/log/support-bundle access.

## Module Registry and Settings

The backend exposes the Stage 4 module registry through `/api/modules`. It returns the 13 baseline modules and supports enable/disable, health checks, manual fetch, settings update, and secret replacement.

Secret settings are never returned in plain text. A configured secret is returned as `value: "********"` with `hasValue: true`. Replace a secret with:

```powershell
Invoke-RestMethod "http://localhost:5089/api/modules/$moduleId/settings/ApiToken/secret" -Method Put -Headers $headers -ContentType application/json -Body '{"value":"new-secret"}'
```

Real mode validation is enforced: enabling a module with `UseFallbackMode=false` fails until all required settings for that module are present. Disabled modules keep old events/audit/logs, but manual fetch returns a warning result.

Fallback mode means an adapter returns deterministic local/demo health and events instead of calling real external systems. SafeMode means dangerous actions are simulated and audited instead of executed. SafeMode defaults to `true`.

## Integration Adapters

Stage 5 adds an adapter architecture behind the module APIs. `IIntegrationAdapterFactory` resolves the adapter by module key, and every adapter supports:

- health checks through `POST /api/modules/{id}/health`
- event fetch through `POST /api/modules/{id}/fetch-events`
- action execution through `POST /api/actions/{id}/execute`

All 13 module adapters are registered. In fallback mode they return demo health/events without external services. In real mode they read `IntegrationSettings`; missing required settings produce `NotConfigured` instead of crashing. Destructive actions require approval and `SafeMode=false`.

Webhook endpoints:

```text
POST /api/webhooks/zabbix
POST /api/webhooks/grafana
POST /api/webhooks/alertmanager
```

Webhook payloads are stored in `WebhookEvents`, normalized into `IncidentEvents`, passed through the local rule advisor, and audited/logged.

## Event Workflow, Approvals, and Support Bundle

Stage 6 completes the event lifecycle. Adapter fetches and webhooks create normalized incidents, the local rule advisor writes recommendations, and high-risk suggested actions move the incident to `PendingApproval`. Only Admin and SeniorAdmin users can approve or reject requests. Approval in `SafeMode=true` records an honest simulation; approval with safe mode disabled delegates execution to the module adapter. Rejected and already-decided approvals are blocked and audited.

The support bundle endpoint exports a JSON file for auditors and administrators:

```text
GET /api/support-bundle
```

The export includes product/version metadata, CockroachDB provider metadata, enabled modules without secrets, health history, incidents, recommendations, approvals, actions, audit, logs, notifications, users/roles without password hashes, and diagnostics for database connectivity, unhealthy modules, pending approvals, recent errors, polling status, webhook count, and security warnings.

## Real Integration Setup

General flow for real integrations:

1. Open Modules.
2. Select the module.
3. Fill required settings such as base URLs, hostnames, credentials, webhook secrets, or tokens.
4. Replace secrets through secret fields; secret values are never returned after save.
5. Set `UseFallbackMode=false`.
6. Keep `SafeMode=true` until the integration is verified.
7. Run Test Connection.
8. Fetch Events or send a webhook.
9. Review audit/logs before enabling real actions.

High and critical actions require approval even when SafeMode is off.

## Run Frontend

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist\src\SysAssist.Web
npm install
npm run dev
```

The frontend opens on `http://localhost:5173`. It calls the API configured by `VITE_API_BASE_URL`; local development defaults to `http://localhost:5089`, while Docker Compose sets `VITE_API_BASE_URL=http://localhost:5000`. If the API is offline, the UI uses deterministic local fallback data so the screens remain reviewable without CockroachDB.

## Build

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
dotnet build SysAssist.sln
dotnet test SysAssist.sln

cd C:\Users\8-Bits\Desktop\Sys\SysAssist\src\SysAssist.Web
npm run build
```

## Tests

The xUnit suite covers:

- password hashing verification;
- module enable/disable state changes;
- required module settings validation;
- fallback fetch events;
- event normalization;
- recommendation creation;
- high/critical approval creation;
- approve/reject audit behavior;
- support bundle secret/password-hash exclusion.

Run:

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
dotnet test SysAssist.sln
```

## Security Notes

Security checklist and known limits are in [docs/security-checklist.md](C:/Users/8-Bits/Desktop/Sys/SysAssist/docs/security-checklist.md:1).

Highlights:

- Password hashes are not returned by API DTOs or support bundles.
- Secret settings are masked.
- Approval and audit protect high-risk actions.
- SafeMode defaults to `true`.
- CORS is explicit and configurable.
- Production exception responses hide stack traces.
- Bootstrap admin credentials must come from secret storage and must be rotated before any real deployment handover.
- SSO/OIDC, rate limiting, immutable external audit storage, and production WAF/IAP controls are future hardening items.

## Demo Script

A guided commission/demo script is available in [docs/demo-script.md](C:/Users/8-Bits/Desktop/Sys/SysAssist/docs/demo-script.md:1). It walks through login, dashboard, module settings, HTTP Endpoint Checker setup, test connection, fetch events, pending approval, approve, audit, and support bundle download.

Diploma defense notes are available in [docs/DIPLOMA_NOTES.md](C:/Users/8-Bits/Desktop/Sys/SysAssist/docs/DIPLOMA_NOTES.md:1). Screenshot capture guidance is in [docs/screenshots/README.md](C:/Users/8-Bits/Desktop/Sys/SysAssist/docs/screenshots/README.md:1).

## Docker Compose

Docker may be unavailable inside the Codex execution environment, so do not treat a failed Docker command here as proof that the lab is broken. The compose files are intended to be run on a local machine with Docker Desktop or a compatible Docker Engine.

The local lab keeps CockroachDB as the database. It does not use SQLite.

Services:

- `cockroachdb`: single-node insecure CockroachDB for local demo work.
- `cockroach-init`: creates the `sysassist` database.
- `redis`: Redis demo dependency.
- `nginx-demo`: simple HTTP endpoint/log source for adapter testing.
- `sysassist-api`: ASP.NET Core API.
- `sysassist-web`: React build served by nginx.
- optional `monitoring` profile: Prometheus, Grafana, and Alertmanager.

Start CockroachDB first:

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
docker compose up -d cockroachdb cockroach-init
docker compose ps
```

The API reads the CockroachDB connection from environment variables. `docker-compose.yml` sets both supported keys:

```text
ConnectionStrings__SysAssistDb=Host=cockroachdb;Port=26257;Database=sysassist;Username=root;Password=;SSL Mode=Disable
ConnectionStrings__DefaultConnection=Host=cockroachdb;Port=26257;Database=sysassist;Username=root;Password=;SSL Mode=Disable
```

`ConnectionStrings__SysAssistDb` is the primary key used by the app. `ConnectionStrings__DefaultConnection` is accepted as a compatibility alias for Docker/local lab scripts. JWT signing also supports both `Jwt__SigningKey` and the `Auth__JwtSecret` alias.

Run migrations on a machine where CockroachDB is available:

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
dotnet tool restore
$env:ConnectionStrings__SysAssistDb="Host=localhost;Port=26257;Database=sysassist;Username=root;Password=;SSL Mode=Disable"
dotnet dotnet-ef database update --project src/SysAssist.Infrastructure --startup-project src/SysAssist.Api --context SysAssistDbContext
```

Alternatively, the API container can apply migrations automatically because compose sets:

```text
SysAssist__UseDemoData=false
SysAssist__ApplyMigrationsOnStartup=true
```

Start the full local lab:

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist
docker compose up -d --build
```

Check containers and logs:

```powershell
docker compose ps
docker compose logs -f sysassist-api
docker compose logs -f sysassist-web
```

Open:

- API health: `http://localhost:5000/health`
- API Swagger: `http://localhost:5000/swagger`
- Frontend: `http://localhost:5173`
- CockroachDB SQL UI: `http://localhost:8080`
- nginx demo health: `http://localhost:8088/health`

Login with the bootstrap credential configured in `.env` or secret storage:

- Login: `admin`
- Password: value from `SysAssist:BootstrapAdminPassword` / `SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD`

Optional monitoring profile:

```powershell
docker compose --profile monitoring up -d
```

Monitoring URLs:

- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000`
- Alertmanager: `http://localhost:9093`

## Google Cloud Production Option

Google Cloud deployment examples live in `deploy/google-cloud`. They cover:

- Cloud Run services for `sysassist-api` and `sysassist-web`.
- Artifact Registry image builds through Cloud Build.
- CockroachDB Cloud connection through Secret Manager.
- Secret Manager entries for JWT and integration tokens.
- CORS update steps after the frontend URL is known.

Start with [README_DEPLOY_GCP.md](C:/Users/8-Bits/Desktop/Sys/SysAssist/deploy/google-cloud/README_DEPLOY_GCP.md:1). The local Docker Compose lab remains the primary demo path; Google Cloud is the production deployment option.
