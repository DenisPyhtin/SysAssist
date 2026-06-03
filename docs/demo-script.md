# Demo Script

Use this script for a local demonstration with the frontend and API running.

## 1. Login

Open the frontend at `http://localhost:5173`.

Use:

- Login: `admin`
- Password: the local value configured in `SysAssist:BootstrapAdminPassword`

## 2. Dashboard

Open Dashboard and point out:

- Active Events
- Pending Approvals
- Enabled Modules
- Critical Alerts
- Event trend
- Module health chart
- Workflow diagram: Event -> Recommendation -> Risk Gate -> Approval -> Audit

## 3. Modules

Open Modules.

In Module Registry:

1. Search for `HTTP Endpoint Checker`.
2. Open Settings.
3. Confirm the module is enabled.
4. Note that fallback mode and SafeMode are on by default.

## 4. Module Settings

In Module Settings for HTTP Endpoint Checker:

1. Set `EndpointUrl` to a test URL such as `https://example.com`.
2. Keep `UseFallbackMode=true` for offline demo behavior.
3. Click Save.
4. Click Test Connection.
5. Click Fetch Events from the registry or module action area.

## 5. Events

Open Events.

1. Filter by severity or status.
2. Open an event detail drawer.
3. Explain that fetch/webhook/demo events are normalized into `IncidentEvent`.

## 6. Pending Approval

Create a pending approval by fetching events from a high/critical source such as Prometheus Alertmanager or by posting a Grafana critical webhook to the API.

Example:

```powershell
Invoke-RestMethod "http://localhost:5089/api/webhooks/grafana" `
  -Method Post `
  -ContentType application/json `
  -Body '{"ruleName":"Demo critical","title":"Critical demo event","state":"alerting","severity":"critical","target":"demo-host","message":"Critical demo workflow"}'
```

Open Approvals and show the new pending request.

## 7. Approve

Approve the pending action with a comment.

Point out:

- SafeMode simulates execution.
- Approval status moves to `Executed`.
- The incident moves to `Completed`.
- Audit records `ACTION_APPROVED` and `ACTION_SIMULATED`.

## 8. Audit

Open Audit and show:

- `EVENT_RECEIVED`
- `EVENT_NORMALIZED`
- `RECOMMENDATION_CREATED`
- `APPROVAL_REQUESTED`
- `ACTION_APPROVED`
- `ACTION_SIMULATED`

## 9. Support Bundle

Open Support Bundle.

1. Click Preview.
2. Click Download.
3. Explain that the JSON includes product/version, CockroachDB metadata, active modules without secrets, health history, incidents, recommendations, approvals, actions, audit, logs, notifications, users/roles without password hashes, and diagnostics.

## 10. Close

Summarize:

- SysAssist coordinates events; it does not replace monitoring tools.
- Dangerous actions require approval.
- SafeMode protects local/demo environments.
- CockroachDB is the production database target.
- Google Cloud deployment is documented as the production option.
