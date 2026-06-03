# SysAssist Web

SysAssist Web is the Vite/React frontend for the enterprise operations console. It covers login, dashboard, incidents, approvals, module registry/settings, actions, audit, logs, notifications, users/roles, diagnostics, support bundle export, and workflow visualization.

## Local Development

```powershell
cd C:\Users\8-Bits\Desktop\Sys\SysAssist\src\SysAssist.Web
npm install
npm run dev
```

The local frontend runs on `http://localhost:5173`. By default it calls the API at `http://localhost:5089`, matching `src/SysAssist.Api/Properties/launchSettings.json`.

To override the API URL, copy `.env.example` to `.env.local` and set:

```text
VITE_API_BASE_URL=http://localhost:5089
```

Docker Compose builds the same frontend with `VITE_API_BASE_URL=http://localhost:5000`, because the compose file maps the backend container to host port `5000`.

## Verification

```powershell
npm run lint
npm run build
```

The production bundle is emitted to `dist/`.
