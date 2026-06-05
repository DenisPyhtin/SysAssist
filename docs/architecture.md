# Architecture

SysAssist follows a Clean Architecture layout:

- `SysAssist.Domain`: core entities, enums, value objects, and domain behavior.
- `SysAssist.Contracts`: API-facing DTOs shared across backend layers.
- `SysAssist.Application`: use cases, ports, services, and application abstractions.
- `SysAssist.Infrastructure`: EF Core, CockroachDB/Npgsql wiring, repositories, and external integrations.
- `SysAssist.Api`: HTTP surface, Swagger, auth, logging, middleware, and composition root.
- `SysAssist.Web`: React enterprise shell and dashboard experience.

The initial system runs without real infrastructure by default. `SysAssist:UseDemoData=true` registers demo repositories. Setting it to `false` activates EF Core through the configured CockroachDB-compatible PostgreSQL connection string.

## Database

CockroachDB is the primary database. EF Core uses the Npgsql provider, UUID primary keys, JSONB payload columns, string-backed enums, and explicit indexes for incident, audit, system log, module, and webhook lookup paths.

Development startup can apply migrations automatically when `SysAssist:UseDemoData=false` and `SysAssist:ApplyMigrationsOnStartup=true`. The seeder is idempotent and creates roles, the default admin account, 13 integration modules, settings, actions, incidents, audit entries, system logs, and a support bundle record.

## Modules

Modules are defined by a catalog in Infrastructure. The catalog owns the 13 baseline module definitions, common settings, module-specific settings, secret flags, and real-mode required settings. Runtime state lives in CockroachDB through `IntegrationModules` and `IntegrationSettings`; demo mode uses the same catalog with in-memory storage.

Settings APIs mask secrets and expose only whether a value exists. Write operations emit audit entries such as `MODULE_ENABLED`, `MODULE_DISABLED`, `MODULE_SETTINGS_UPDATED`, `MODULE_HEALTH_CHECK`, and `MODULE_FETCH_EVENTS`.

## Adapter Runtime

Integration adapters implement `IIntegrationAdapter` and are resolved through `IIntegrationAdapterFactory`. Adapters read module state and settings through `IModuleSettingsReader`, so the same implementation works in CockroachDB mode and demo mode.

Real integrations are deliberately conservative. HTTP and PostgreSQL adapters use real clients where settings are present; Redis, Docker, SMTP, Telegram, filesystem, Linux, and local advisor adapters keep destructive behavior behind explicit approval and `SafeMode=false`. Fallback mode always produces deterministic local results so unavailable external systems do not take down the API.

## Event and Approval Flow

Incoming events share one processing path whether they arrive from adapter polling, a webhook, or demo fallback data. The API records receipt, normalizes the payload into `IncidentEvent`, asks the local rule advisor for recommendations, and links the best matching `IntegrationModuleAction`. Low and medium risk actions complete in diagnostic/simulated mode. High and critical actions create `ApprovalRequest`, put the incident into `PendingApproval`, emit `APPROVAL_REQUESTED`, and notify SeniorAdmin recipients.

Approval decisions are final. Rejecting a request marks the approval rejected, moves the incident to `Rejected`, writes `ACTION_REJECTED`, and notifies operators/engineers. Approving a request executes through the module adapter when safe mode is off, or records an `ACTION_SIMULATED` result when safe mode is on. Executed actions update the approval, incident, audit trail, system logs, and local notification feed.

## Audit, Diagnostics, and Export

Stage 6 audit names use explicit workflow events such as `EVENT_RECEIVED`, `EVENT_NORMALIZED`, `RECOMMENDATION_CREATED`, `APPROVAL_REQUESTED`, `ACTION_APPROVED`, `ACTION_REJECTED`, `ACTION_EXECUTED`, `ACTION_SIMULATED`, and `SUPPORT_BUNDLE_EXPORTED`.

`GET /api/support-bundle` returns an `application/json` download containing generated metadata, module state without secrets, health history, incidents, recommendations, approvals, actions, audit entries, logs, notifications, users/roles without password hashes, and diagnostics. Diagnostics cover database connectivity, enabled unhealthy modules, pending approvals, recent errors, polling state, webhook volume, and security warnings such as fallback mode or disabled safe mode.
