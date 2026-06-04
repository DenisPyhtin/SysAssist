# Screenshot Capture Guide

Recommended captures for diploma defense and QA:

- Login screen with SysAssist branding.
- Dashboard with active events, pending approvals, health chart, workflow diagram, and recent audit.
- Events page with severity/status filtering.
- Approvals page with approve/reject modal.
- Modules page, `Module Registry` section.
- Modules page, `Module Settings` section with masked secrets.
- Support Bundle preview showing exported sections without passwords or tokens.
- Swagger at `http://localhost:5089/swagger` for `dotnet run`, or `http://localhost:5000/swagger` after Docker Compose startup.
- Docker Desktop or `docker compose ps` output from a local machine where Docker is available.

Docker may be unavailable in the Codex execution environment. Capture Docker screenshots on a local workstation with Docker Desktop after running:

```powershell
cd path\to\SysAssist
docker compose up -d --build
docker compose ps
```

Suggested file names:

- `01-login.png`
- `02-dashboard.png`
- `03-events.png`
- `04-approvals.png`
- `05-module-registry.png`
- `06-module-settings.png`
- `07-support-bundle.png`
- `08-swagger.png`
- `09-docker-compose.png`
