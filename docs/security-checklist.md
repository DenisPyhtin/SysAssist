# Security Checklist

This checklist documents the current SysAssist security posture and the limits that still need production hardening.

## Implemented

- Password hashes are PBKDF2-SHA256 and are not returned by user APIs or support bundle exports.
- Secret module settings are masked as `********` and must be replaced through the secret replacement endpoint.
- Support bundle exports include module DTOs and user/role DTOs only; they do not include password hashes or raw module secret values.
- High and critical actions create approval requests instead of executing directly.
- Approval decisions are final; already decided approvals cannot be approved or rejected again.
- SafeMode defaults to `true` for modules and records simulations instead of running dangerous real actions.
- JWT bearer auth is enabled, with role policies for Admin, SeniorAdmin, Engineer, Operator, and Auditor paths.
- CORS origins are explicit and configurable through `Cors__AllowedOrigins__0`.
- Production startup rejects demo data, automatic migrations, wildcard hosts, local CORS origins, weak JWT keys, and missing license configuration.
- Security headers are applied to API responses: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, and CSP outside Swagger.
- Non-development API startup enables HSTS and HTTPS redirection.
- `/health/live` and `/health/ready` expose separate liveness and readiness checks. Readiness verifies database connectivity and license status.
- Diagnostics expose real adapter-health evidence, latest diagnostics timestamp, stale health-check count, and freshness status.
- Kestrel request body size is capped by `Security:MaxRequestBodyBytes`.
- Rate limiting is enabled for auth, webhook, and write/run/action endpoints.
- Login, settings, secret replacement, action execution, user writes, role writes, and custom module manifests have endpoint-level validation.
- Login attempts write sanitized security logs with correlation id, client key, login, and result reason. Passwords and tokens are not logged.
- Initial admin bootstrap uses `SysAssist:BootstrapAdminPassword`; the backend rejects missing, weak, or placeholder bootstrap values when creating a fresh admin.
- The database seeder does not reset an existing admin password by default. Local presentation/bootstrap recovery can opt in with `SysAssist:ResetBootstrapAdminPasswordOnStartup=true`; keep it `false` for production.
- Module secrets can be encrypted at rest with AES-GCM through `Security:SecretEncryptionKey` / `SYSASSIST_SECRET_ENCRYPTION_KEY`.
- License validation uses signed ECDSA P-256 license keys and blocks protected API routes when invalid.
- Required module settings are validated before real mode can be enabled.
- Approval reject comments are required in the frontend flow.
- The global exception handler hides internal exception details in Production and returns a generic error with a correlation id.
- Swagger uses bearer auth metadata and does not publish secret values directly.

## Covered By Tests

- Password hashing verifies correct passwords and rejects wrong passwords.
- Secret settings are masked.
- AES-GCM secret encryption decrypts valid ciphertext and rejects tampered ciphertext.
- Signed license validation accepts valid SysAssist licenses and rejects tampered or expired licenses.
- Required real-mode settings are validated.
- Fallback fetch normalizes demo events and creates recommendations.
- High/critical workflow creates approval requests.
- Approve/reject decisions write audit entries.
- Support bundle excludes password hashes and raw secret values.

## Current Limits

- Bootstrap credentials are an operator secret; rotate them before real deployment handover and move steady-state identity to named users.
- Production identity is JWT-based only; SSO/OIDC and refresh-token lifecycle are planned hardening items.
- Webhook signature checks are present at service level but should be expanded with provider-specific signature validation.
- Request DTO validation is intentionally light in this version. Production hardening should add stricter FluentValidation or endpoint filters for lengths, URL formats, and JSON schema validation.
- WAF/IAP controls remain deployment concerns for internet exposure.
- Integration adapters are conservative and fallback-friendly; several real destructive operations intentionally remain blocked or simulated.
- Audit entries are application-level records; immutable external audit storage is planned for production hardening.
- Background polling workers are represented in diagnostics/config and can be promoted to a hosted scheduler in a future iteration.
