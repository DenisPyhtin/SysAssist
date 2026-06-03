# Building SysAssist Modules

The module SDK lives in `packages/sysassist-module-sdk`.

## Development Flow

1. Copy `packages/sysassist-module-sdk/templates/module.manifest.json`.
2. Implement an adapter with `defineSysAssistModule`.
3. Expose the adapter through an HTTP runtime host.
4. Upload the manifest in the SysAssist Modules page.
5. Run `Test` from the user module card.

## Required Runtime Behavior

- `checkHealth` must perform a real check.
- `fetchEvents` must return normalized events only from real input.
- `/actions/{actionKey}` must return success only after the target system confirms the action.
- Missing configuration should return `NotConfigured`, not `Healthy`.

## Recommended Folder Layout

```text
my-module/
  module.manifest.json
  package.json
  src/
    index.ts
  runtime-host.mjs
  dist/
    index.js
```

## Runtime Host Contract

SysAssist calls the manifest `entrypoint` as an HTTP base URL:

- `POST /health`
- `POST /events`
- `POST /actions/{actionKey}`

Every response must be JSON. Health succeeds only when `status` is `Healthy` or `success` is `true`; events and actions never receive fake success from SysAssist.

## Release Checklist

- Manifest validates.
- Health check fails honestly when endpoint is down.
- Secrets are marked as secret.
- Actions declare risk and approval posture.
- Event ids are stable enough for deduplication.
- Support bundle output contains no credentials.
