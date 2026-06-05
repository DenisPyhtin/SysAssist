# Diploma Notes

## What Was Created

SysAssist is a modular enterprise platform for coordinating IT incidents from monitoring, infrastructure, database, notification, and local-check systems. The project includes a .NET 9 backend, React/Vite operator UI, CockroachDB schema and EF Core migrations, Docker Compose local lab, Google Cloud Run deployment examples, tests, security notes, demo script, and support-bundle export.

Core capabilities:

- JWT login and role-based access for Admin, SeniorAdmin, Engineer, Operator, and Auditor.
- Dashboard, incident events, approval queue, module registry, module settings, actions, audit, logs, notifications, users/roles, diagnostics, and support bundle screens.
- 13 integration modules seeded in the backend and exposed through the UI.
- Fallback mode for safe local demonstrations without real external services.
- SafeMode for dangerous operations, with high-risk actions routed through approval.
- Audit trail and support bundle export without passwords, password hashes, or raw tokens.

## Difference From Monitoring

SysAssist is not a replacement for Zabbix, Grafana, Prometheus, CockroachDB, Redis, Docker, nginx, Linux hosts, SMTP, or Telegram. Those systems observe and operate infrastructure. SysAssist coordinates the operational response:

- receives and normalizes events;
- groups them into a common incident model;
- creates local recommendations;
- checks action risk;
- asks for approval before dangerous actions;
- records audit evidence;
- exports a support bundle for administrators and auditors.

## Why CockroachDB

CockroachDB was selected as the main database because the system needs relational consistency, SQL querying, JSONB payload storage, and a production path toward horizontal scalability. It is PostgreSQL wire-compatible, so EF Core can use the mature Npgsql provider while keeping the database suitable for distributed enterprise deployments.

The local demo can run with in-memory fallback data, but the primary database design remains CockroachDB. SQLite and JSON files are not used as primary storage.

## Modular Architecture

The backend follows a clean architecture layout:

- `SysAssist.Domain`: entities and enums.
- `SysAssist.Contracts`: DTOs shared by API and frontend.
- `SysAssist.Application`: service and adapter contracts.
- `SysAssist.Infrastructure`: EF Core, CockroachDB, module catalog, auth, adapters, demo fallback store, workflow logic.
- `SysAssist.Api`: endpoints, auth policies, Swagger, health checks, logging, CORS.
- `SysAssist.Web`: React enterprise console.

Modules are defined through a central catalog and seeded into CockroachDB. Each module has real settings, common controls such as `UseFallbackMode` and `SafeMode`, and masked secret handling.

## Integrations

The baseline registry includes:

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

Adapters expose a common shape for health checks, event fetch, webhook processing, and actions. Real-mode settings are validated before use. If credentials or endpoints are not configured, modules report a clear configuration state instead of crashing.

## Fallback For Safety

Fallback mode returns deterministic local health/events and allows the project to be demonstrated without live Zabbix, Grafana, Prometheus, Redis, Docker, SMTP, Telegram, or Linux agents. This is important for a diploma defense because the UI and workflows remain testable even when external services are unavailable.

SafeMode defaults to `true`. Dangerous actions are simulated and audited until an administrator explicitly disables SafeMode and provides real integration settings.

## Approval Flow

High-risk and critical events create approval requests. Admin and SeniorAdmin users can approve or reject them. Approval in SafeMode records a simulation result. Rejection requires a comment and updates the incident status. All decisions are written to audit history.

## Limitations

- Bootstrap admin credentials must come from secret storage and be rotated before any real deployment.
- SSO/OIDC, refresh-token lifecycle, rate limiting, WAF/IAP controls, and immutable external audit storage are future hardening work.
- Several real destructive integration actions are intentionally conservative or simulated.
- Background polling is represented in settings and diagnostics; a production scheduler is future work.
- Provider-specific webhook signature validation should be expanded for production.

## Future Work

- Add scheduled background polling workers.
- Add SSO/OIDC and refresh-token rotation.
- Add provider-specific webhook signature verification.
- Add immutable audit export to external storage.
- Add richer action parameter schemas and approval templates.
- Add production dashboards for SLA/SLO analytics and incident trends.
- Add deployment automation for CockroachDB Cloud, Secret Manager, and Cloud Run revisions.
