# SysAssist Module SDK

`@sysassist/module-sdk` is the local TypeScript contract for building custom SysAssist modules. It defines the manifest format, adapter shape, health results, normalized events, and action results used by the SysAssist module loader.

## Minimum Requirements

- A `sysassist.module/v1` manifest.
- A stable lowercase module `key`.
- One HTTP/HTTPS runtime entrypoint. SysAssist calls this host with JSON requests.
- A real `checkHealth` implementation. It must perform actual I/O or return `NotConfigured`/`Error`; never return success without a check.
- Secrets must be declared with `secret: true`.
- Destructive actions must set `requiresApproval: true`.

## Manifest Fields

| Field | Required | Description |
| --- | --- | --- |
| `apiVersion` | yes | Must be `sysassist.module/v1`. |
| `key` | yes | Stable module id, lowercase letters/numbers/dash/underscore/dot. |
| `name` | yes | Human-readable module name. |
| `version` | yes | Semver package version. |
| `author` | yes | Vendor, team, or maintainer. |
| `category` | yes | `Monitoring`, `Infrastructure`, `Database`, `Notification`, `LocalCheck`, `Security`, etc. |
| `entrypoint` | yes | HTTP/HTTPS runtime base URL, for example `http://localhost:7077`. |
| `permissions` | yes | Runtime permissions such as `network:http`, `network:tcp`, `filesystem:read`. |
| `settings` | yes | Operator-editable configuration fields. |
| `actions` | yes | Available module actions. |

## Adapter Shape

```ts
export default defineSysAssistModule({
  manifest,
  async checkHealth(settings, signal) {
    // perform real network, filesystem, database, or process check
    return healthy('Endpoint responded.')
  },
  async fetchEvents(settings, signal) {
    return []
  },
  async executeAction(actionKey, settings, parameters, signal) {
    return actionSuccess('Action executed.')
  }
})
```

The adapter shape is the authoring contract. For SysAssist runtime execution, expose it through an HTTP host.

## Runtime HTTP Contract

SysAssist sends POST requests to the module `entrypoint`:

| Endpoint | Request | Successful response |
| --- | --- | --- |
| `/health` | `{ moduleKey, settings }` | `{ "status": "Healthy", "message": "..." }` |
| `/events` | `{ moduleKey, settings }` | `{ "success": true, "message": "...", "events": [] }` |
| `/actions/{actionKey}` | `{ moduleKey, settings, target, parametersJson }` | `{ "success": true, "message": "..." }` |

Use `templates/runtime-host.mjs` for a minimal no-dependency host:

```bash
node templates/runtime-host.mjs
```

## Packaging

1. Build your module with TypeScript.
2. Keep `module.manifest.json` at package root.
3. Point `entrypoint` to the runtime host URL.
4. Upload the manifest from SysAssist `Modules -> Upload custom module`.

## Safety Rules

- Health checks must not fake success.
- Actions must not return success until the external system accepts the operation.
- Never log raw secrets.
- Use approval for restarts, deletes, reloads, remediation, or any irreversible operation.
- Keep event payloads structured JSON and include source identifiers for deduplication.

See `templates/module.manifest.json` and `examples/http-latency-adapter.ts`.
