# Final Polish Checklist

## Architecture

- CockroachDB is the primary database target through EF Core and Npgsql.
- SQLite is not used as primary storage.
- JSON is used for payload/support/export fields, not as the application database.
- Backend reads the database connection from environment/configuration through `ConnectionStrings__SysAssistDb`.
- EF Core migrations are present for the CockroachDB schema.

## Modules

- The module catalog contains all 13 baseline modules.
- Database seeding creates all 13 modules from the catalog.
- The frontend Modules page has `Module Registry` and `Module Settings` sections.
- Each module has real settings plus common controls for enablement, fallback mode, SafeMode, polling, timeouts, retries, notes, and tags.
- Secret settings are masked and replaced through the secret endpoint.

## Safety

- Fallback mode works without external services.
- SafeMode defaults to `true`.
- Dangerous/high-risk actions require approval or simulation.
- Support bundle exports exclude passwords, password hashes, and raw tokens.
- Dashboard is built from empty-safe collection queries and demo fallback data.

## Verification Scope

Available in Codex:

- `dotnet build`
- `dotnet test`
- `npm run build`
- `npm run lint`
- static Docker/Compose configuration review
- local API demo-mode smoke checks

Requires a local machine with Docker Desktop:

- `docker compose up -d --build`
- CockroachDB-backed EF Core migration execution
- container health checks
- frontend and Swagger checks through Docker ports
