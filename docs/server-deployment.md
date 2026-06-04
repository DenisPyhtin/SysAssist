# SysAssist Server Deployment

This guide prepares SysAssist for a production-style server release with Docker Compose, an external CockroachDB-compatible database, encrypted module secrets, signed licensing, and a same-origin web/API deployment.

## Target Layout

```text
Internet
  -> TLS reverse proxy, for example nginx or a cloud load balancer
  -> sysassist-web container on port 4173
      -> static React assets
      -> /api and /health/ready proxied to sysassist-api
  -> sysassist-api container on internal port 8080
  -> external CockroachDB / Cockroach Cloud
```

The API container is not published directly in `docker-compose.prod.yml`; keep it internal unless you have a separate private network or ingress policy.

## Server Prerequisites

- Linux VPS or VM with Docker Engine and Docker Compose v2.
- Domain name pointed to the server, for example `sysassist.example.com`.
- TLS certificate from Let's Encrypt, a cloud load balancer, or another trusted issuer.
- CockroachDB/Cockroach Cloud connection string with SSL enabled.
- Signed SysAssist license values:
  - `Licensing__PublicKey`
  - `Licensing__LicenseKey`

## Prepare Files

On the server:

```bash
sudo mkdir -p /opt/sysassist
sudo chown "$USER":"$USER" /opt/sysassist
cd /opt/sysassist
git clone <your-repository-url> .
cp .env.production.example .env.production
```

Generate local strong secrets on Windows:

```powershell
scripts\generate-prod-secrets.ps1
```

Or on Linux:

```bash
JWT_SECRET="$(openssl rand -base64 48)"
SECRET_KEY="$(openssl rand -base64 32)"
BOOTSTRAP_PASSWORD="$(openssl rand -base64 24 | tr -d '=+/ ' | cut -c1-28)@9aA"
printf "Auth__JwtSecret=%s\nSecurity__SecretEncryptionKey=%s\nSysAssist__BootstrapAdminPassword=%s\nSYSASSIST_SMOKE_ADMIN_PASSWORD=%s\n" "$JWT_SECRET" "$SECRET_KEY" "$BOOTSTRAP_PASSWORD" "$BOOTSTRAP_PASSWORD"
```

Edit `.env.production` and replace every `replace-*` value. Minimum production values:

```env
ASPNETCORE_ENVIRONMENT=Production
AllowedHosts=sysassist.example.com
Cors__AllowedOrigins__0=https://sysassist.example.com
ReverseProxy__TrustForwardedHeaders=true
SysAssist__UseDemoData=false
SysAssist__ApplyMigrationsOnStartup=false
ConnectionStrings__SysAssistDb=Host=...;Port=26257;Database=sysassist;Username=...;Password=...;SSL Mode=Require
Auth__JwtSecret=...
Security__SecretEncryptionKey=...
Licensing__PublicKey=...
Licensing__LicenseKey=...
```

Keep `.env.production` outside Git. The repository ignores it.

## Build And Apply Migrations

Production startup does not apply EF migrations automatically. Run the migrator first:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml build sysassist-migrate sysassist-api sysassist-web
docker compose --env-file .env.production -f docker-compose.prod.yml run --rm sysassist-migrate
```

The migrator uses the same EF Core model and Npgsql/CockroachDB path as the API.

## Start Production Stack

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d sysassist-api sysassist-web
docker compose --env-file .env.production -f docker-compose.prod.yml ps
```

Open the internal web port while testing:

```text
http://SERVER_IP:8080
```

For public release, place TLS nginx/Caddy/cloud ingress in front of `127.0.0.1:8080` or the host port chosen by `SYSASSIST_WEB_PORT`.

Example nginx config:

```bash
sudo cp deploy/nginx/sysassist.reverse-proxy.conf /etc/nginx/sites-available/sysassist.conf
sudo ln -s /etc/nginx/sites-available/sysassist.conf /etc/nginx/sites-enabled/sysassist.conf
sudo nginx -t
sudo systemctl reload nginx
```

Replace `sysassist.example.com` and certificate paths before enabling it.

## Smoke Check

From the server or an operator workstation:

```powershell
$env:SYSASSIST_SMOKE_ADMIN_PASSWORD = "<bootstrap-or-current-admin-password>"
scripts\prod-smoke.ps1 -ApiUrl https://sysassist.example.com -RequireProductionReadiness
```

Expected result:

```json
{
  "Ready": "Ready",
  "Diagnostics": "Healthy",
  "ProductionReadiness": "Ready"
}
```

If `ProductionReadiness` is `Blocked`, open `/api/production-readiness` through the UI or run the smoke script without `-RequireProductionReadiness` to see the failed gates.

## Autostart With systemd

```bash
sudo cp deploy/systemd/sysassist.service /etc/systemd/system/sysassist.service
sudo systemctl daemon-reload
sudo systemctl enable sysassist
sudo systemctl start sysassist
sudo systemctl status sysassist
```

The unit expects the repository at `/opt/sysassist`.

## Release Update

```bash
cd /opt/sysassist
git pull
docker compose --env-file .env.production -f docker-compose.prod.yml build
docker compose --env-file .env.production -f docker-compose.prod.yml run --rm sysassist-migrate
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
scripts/prod-smoke.ps1 -ApiUrl https://sysassist.example.com -RequireProductionReadiness
```

## Backup Checklist

- Enable CockroachDB automated backups or scheduled `cockroach dump`/cloud snapshots.
- Store `.env.production` in a password manager or secret manager.
- Export support bundles only to trusted storage.
- Document license expiry and rotation procedures.
- Keep API container private; expose only the web/reverse-proxy entrypoint.
