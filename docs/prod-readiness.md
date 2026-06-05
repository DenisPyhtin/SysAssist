# Production Readiness

This checklist is the release gate for running SysAssist as a real product, not as a demo.

## Automated Gate

- `GET /api/production-readiness` returns `Ready` only when all required production gates pass.
- The endpoint is admin-only and intentionally remains reachable when the license is invalid so operators can see why the product is blocked.
- Run `scripts/prod-smoke.ps1 -RequireProductionReadiness` in the deployment pipeline after the API is started with production configuration.
- Local development can run `scripts/prod-smoke.ps1` without the switch; it can read `SysAssist__BootstrapAdminPassword` from `.env` and still verifies readiness, auth, dashboard counters, module health, diagnostics, logs, and support bundle export.
- Server deployment uses `docker-compose.prod.yml`: run `sysassist-migrate` first, then `sysassist-api` and `sysassist-web`.
- When the API is behind nginx or another TLS reverse proxy, enable `ReverseProxy:TrustForwardedHeaders=true` only while keeping the API container on a private/internal network.

## Release Gates

- `SysAssist:UseDemoData=false`.
- `SysAssist:ApplyMigrationsOnStartup=false` in Production.
- `AllowedHosts` is explicit and is not `*`.
- `Cors:AllowedOrigins` contains only deployed HTTPS frontend origins.
- `Jwt:SigningKey` or `Auth:JwtSecret` comes from secret storage and is at least 32 characters.
- `Security:SecretEncryptionKey` or `SYSASSIST_SECRET_ENCRYPTION_KEY` is present.
- `Licensing:PublicKey` and `Licensing:LicenseKey` are present.
- A fresh database uses `SysAssist:BootstrapAdminPassword` from secret storage; no default admin password is embedded in source.
- `/health/live` returns `Healthy`.
- `/health/ready` returns `Ready` and includes healthy database and valid license components.
- `/api/diagnostics` reports `diagnosticsEvidence=real-adapter-health`, `lastDiagnosticsRunAt`, and `diagnosticsFreshness=fresh` after a real diagnostics run.
- Request body limit is configured with `Security:MaxRequestBodyBytes`.
- Rate limits are configured for auth, webhooks, and write/run/action endpoints.
- Mutating request DTOs reject blank keys, oversized settings, invalid action JSON, weak user passwords, and oversized custom module manifests.
- `dotnet test SysAssist.sln --no-restore` passes.
- `npm run build` passes in `src/SysAssist.Web`.
- Fresh browser reload has no console errors or warnings.

## Database

- CockroachDB is the production database target.
- EF Core uses the Npgsql provider against the PostgreSQL-compatible CockroachDB wire protocol.
- Migrations must be applied by the deployment pipeline or an operator-run migration job.
- Production API startup must not auto-apply migrations.
- Backup and restore must be tested before go-live.
- Retention policy must be configured for audit, logs, support bundles, and health checks.

## Secrets

- Do not commit `.env`, `.env.local`, generated license tokens, JWT secrets, DB passwords, or module credentials.
- Runtime secrets should come from a secret manager or vault in production.
- Module secrets are stored encrypted with AES-GCM and returned only as `********`.
- Support bundle exports must not include password hashes, license tokens, JWT signing keys, encryption keys, or raw module secrets.
- Encryption key rotation requires a planned re-encryption job before changing the active key.

## Licensing

- License is verified with ECDSA P-256 signatures.
- Production API blocks protected `/api/*` routes when the license is invalid.
- `/api/auth/*` and `/api/license` remain reachable so operators can recover.
- License status changes should be audited and monitored.

## Integrations

- Every enabled module must have either real connection settings or a deliberate `NotConfigured` state.
- Do not mark module health as `Healthy` without a real adapter check.
- Destructive actions must require approval unless explicitly exempted and reviewed.
- `SafeMode=true` is the default. Disabling SafeMode is a privileged production action.

## Operations

- Deploy behind HTTPS and a production reverse proxy or managed ingress.
- Keep the API private and expose only the web/reverse-proxy entrypoint.
- Keep security headers enabled.
- Keep API rate limiting enabled for login, webhooks, diagnostics runs, module tests, settings writes, user writes, and action execution.
- Tune `RateLimiting:*` values against expected customer traffic before go-live.
- Monitor API latency, adapter failures, DB connectivity, license expiry, failed logins, and diagnostics status.
- Store audit events in an immutable external sink for regulated environments.
- Add WAF/IAP, SSO/OIDC, and rate limiting before internet exposure.

## Current Product Gaps

- SSO/OIDC is not implemented.
- Refresh-token lifecycle is not implemented.
- Immutable external audit storage is not implemented.
- Provider-specific webhook signature schemes need deeper implementation.
- Background polling should become a hosted scheduler with leases and retry policy.
- Module connection errors remain real and must be fixed by providing working endpoints/secrets or disabling those modules.
