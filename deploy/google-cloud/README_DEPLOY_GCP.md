# Deploy SysAssist to Google Cloud

This runbook deploys SysAssist as an enterprise-ready Google Cloud option:

- API: Google Cloud Run.
- Frontend: Google Cloud Run running nginx.
- Database: CockroachDB Cloud.
- Secrets: Google Secret Manager.
- Images: Artifact Registry.
- Builds: Cloud Build.

The local Docker Compose lab remains the primary demo path. Use this guide for production-style deployment.

## 1. Create a Google Cloud Project

```powershell
gcloud projects create YOUR_PROJECT_ID --name="SysAssist"
gcloud config set project YOUR_PROJECT_ID
```

If the project already exists:

```powershell
gcloud config set project YOUR_PROJECT_ID
```

## 2. Enable Required APIs

```powershell
gcloud services enable run.googleapis.com
gcloud services enable artifactregistry.googleapis.com
gcloud services enable secretmanager.googleapis.com
gcloud services enable cloudbuild.googleapis.com
```

## 3. Create Artifact Registry

```powershell
gcloud artifacts repositories create sysassist `
  --repository-format=docker `
  --location=us-central1 `
  --description="SysAssist container images"
```

If you use another region, pass the same region to all later commands.

## 4. Create CockroachDB Cloud Cluster

Create a CockroachDB Cloud cluster from the CockroachDB Cloud console:

1. Create a production cluster.
2. Create a SQL user for SysAssist.
3. Create or select database `sysassist`.
4. Copy the PostgreSQL-compatible connection string.
5. Keep SSL enabled as required by CockroachDB Cloud.

The app uses EF Core with the Npgsql provider, so keep the connection string PostgreSQL wire-compatible.

## 5. Store Secrets

Never commit real secrets. Store them in Secret Manager:

```powershell
gcloud secrets create COCKROACH_CONNECTION_STRING --replication-policy=automatic
gcloud secrets versions add COCKROACH_CONNECTION_STRING --data-file=-
```

Paste the CockroachDB Cloud connection string, then finish stdin.

Create the remaining secrets:

```powershell
gcloud secrets create JWT_SECRET --replication-policy=automatic
gcloud secrets versions add JWT_SECRET --data-file=-

gcloud secrets create ZABBIX_API_TOKEN --replication-policy=automatic
gcloud secrets versions add ZABBIX_API_TOKEN --data-file=-

gcloud secrets create GRAFANA_API_TOKEN --replication-policy=automatic
gcloud secrets versions add GRAFANA_API_TOKEN --data-file=-

gcloud secrets create TELEGRAM_BOT_TOKEN --replication-policy=automatic
gcloud secrets versions add TELEGRAM_BOT_TOKEN --data-file=-

gcloud secrets create SMTP_PASSWORD --replication-policy=automatic
gcloud secrets versions add SMTP_PASSWORD --data-file=-
```

`JWT_SECRET` must be long and random, at least 32 characters.

## 6. Grant Cloud Run Secret Access

Find the default compute service account:

```powershell
$projectNumber = gcloud projects describe YOUR_PROJECT_ID --format="value(projectNumber)"
$runServiceAccount = "$projectNumber-compute@developer.gserviceaccount.com"
```

Grant access:

```powershell
gcloud secrets add-iam-policy-binding COCKROACH_CONNECTION_STRING --member="serviceAccount:$runServiceAccount" --role="roles/secretmanager.secretAccessor"
gcloud secrets add-iam-policy-binding JWT_SECRET --member="serviceAccount:$runServiceAccount" --role="roles/secretmanager.secretAccessor"
gcloud secrets add-iam-policy-binding ZABBIX_API_TOKEN --member="serviceAccount:$runServiceAccount" --role="roles/secretmanager.secretAccessor"
gcloud secrets add-iam-policy-binding GRAFANA_API_TOKEN --member="serviceAccount:$runServiceAccount" --role="roles/secretmanager.secretAccessor"
gcloud secrets add-iam-policy-binding TELEGRAM_BOT_TOKEN --member="serviceAccount:$runServiceAccount" --role="roles/secretmanager.secretAccessor"
gcloud secrets add-iam-policy-binding SMTP_PASSWORD --member="serviceAccount:$runServiceAccount" --role="roles/secretmanager.secretAccessor"
```

## 7. Build the API Image

```powershell
gcloud builds submit `
  --region=us-central1 `
  --config=deploy/google-cloud/cloudbuild-api.yaml `
  --substitutions=_REGION=us-central1,_REPOSITORY=sysassist,_IMAGE=sysassist-api,_TAG=latest .
```

The build uses [deploy/docker/api.Dockerfile](../docker/api.Dockerfile).

## 8. Deploy the API

Option A, deploy from CLI:

```powershell
$webUrl = "https://sysassist-web.example.invalid"
gcloud run deploy sysassist-api `
  --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/sysassist/sysassist-api:latest `
  --region=us-central1 `
  --platform=managed `
  --allow-unauthenticated `
  --port=8080 `
  --set-env-vars=ASPNETCORE_ENVIRONMENT=Production,SysAssist__UseDemoData=false,SysAssist__ApplyMigrationsOnStartup=false,Jwt__Issuer=SysAssist,Jwt__Audience=sysassist-api,Cors__AllowedOrigins__0=$webUrl `
  --set-secrets=ConnectionStrings__DefaultConnection=COCKROACH_CONNECTION_STRING:latest,ConnectionStrings__SysAssistDb=COCKROACH_CONNECTION_STRING:latest,Auth__JwtSecret=JWT_SECRET:latest,Jwt__SigningKey=JWT_SECRET:latest,ZABBIX_API_TOKEN=ZABBIX_API_TOKEN:latest,GRAFANA_API_TOKEN=GRAFANA_API_TOKEN:latest,TELEGRAM_BOT_TOKEN=TELEGRAM_BOT_TOKEN:latest,SMTP_PASSWORD=SMTP_PASSWORD:latest
```

Option B, edit [service-api.yaml](service-api.yaml), set the Cloud project image path and replace the example `Cors__AllowedOrigins__0` value with the web app URL, then:

```powershell
gcloud run services replace deploy/google-cloud/service-api.yaml --region=us-central1
```

Capture the API URL:

```powershell
$apiUrl = gcloud run services describe sysassist-api --region=us-central1 --format="value(status.url)"
```

## 9. Run EF Core Migrations

For production, keep `SysAssist__ApplyMigrationsOnStartup=false` in Cloud Run and run migrations as an explicit release step.

From a trusted machine with network access to CockroachDB Cloud:

```powershell
dotnet tool restore
$env:ConnectionStrings__SysAssistDb="YOUR_COCKROACHDB_CLOUD_CONNECTION_STRING"
dotnet dotnet-ef database update `
  --project src/SysAssist.Infrastructure `
  --startup-project src/SysAssist.Api `
  --context SysAssistDbContext
```

The design-time factory also accepts:

- `ConnectionStrings__DefaultConnection`
- `SYSASSIST_MIGRATION_CONNECTION`

## 10. Build the Web Image

Use the API URL from step 8:

```powershell
gcloud builds submit `
  --region=us-central1 `
  --config=deploy/google-cloud/cloudbuild-web.yaml `
  --substitutions=_REGION=us-central1,_REPOSITORY=sysassist,_IMAGE=sysassist-web,_TAG=latest,_VITE_API_BASE_URL=$apiUrl .
```

The build uses [deploy/docker/web.Dockerfile](../docker/web.Dockerfile), which builds the Vite app and serves it through nginx.

## 11. Deploy the Web App

```powershell
gcloud run deploy sysassist-web `
  --image=us-central1-docker.pkg.dev/YOUR_PROJECT_ID/sysassist/sysassist-web:latest `
  --region=us-central1 `
  --platform=managed `
  --allow-unauthenticated `
  --port=4173
```

Or edit [service-web.yaml](service-web.yaml), replace `PROJECT_ID`, then:

```powershell
gcloud run services replace deploy/google-cloud/service-web.yaml --region=us-central1
```

Capture the web URL:

```powershell
$webUrl = gcloud run services describe sysassist-web --region=us-central1 --format="value(status.url)"
```

## 12. Configure CORS

Redeploy or update the API with the final web URL:

```powershell
gcloud run services update sysassist-api `
  --region=us-central1 `
  --update-env-vars=Cors__AllowedOrigins__0=$webUrl
```

If you use the YAML path, update `Cors__AllowedOrigins__0` in [service-api.yaml](service-api.yaml) and run `gcloud run services replace` again.

## 13. Verify Deployment

Check health:

```powershell
Invoke-RestMethod "$apiUrl/health"
```

Open Swagger:

```text
$apiUrl/swagger
```

Login:

```powershell
$body = @{ login = "admin"; password = $env:SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD } | ConvertTo-Json
Invoke-RestMethod "$apiUrl/api/auth/login" `
  -Method Post `
  -ContentType application/json `
  -Body $body
```

Open the frontend:

```text
$webUrl
```

## Production Notes

- Keep CockroachDB Cloud as the production database.
- Do not use SQLite.
- Keep secrets in Secret Manager.
- Rotate `JWT_SECRET` before real production use.
- Prefer a dedicated Cloud Run service account instead of the default compute service account.
- Restrict ingress and add identity-aware access controls if the deployment is private.
- For high-security environments, add VPC egress controls and CockroachDB Cloud network restrictions.
