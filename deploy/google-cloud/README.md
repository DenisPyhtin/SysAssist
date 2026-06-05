# SysAssist on Google Cloud

This folder contains production deployment examples for Google Cloud Run, Artifact Registry, Secret Manager, Cloud Build, and CockroachDB Cloud.

The local Docker Compose lab remains the primary demo path. Google Cloud is the production deployment option. Do not put real secrets in this repository.

## Reference Docs

- Cloud Build can build Docker images and push them to Artifact Registry with the `images` field and substitutions.
- Cloud Run can consume Secret Manager values as environment variables through `valueFrom.secretKeyRef`.

Official docs:

- https://docs.cloud.google.com/artifact-registry/docs/configure-cloud-build
- https://docs.cloud.google.com/run/docs/configuring/services/secrets

## Files

- `README_DEPLOY_GCP.md`: full deployment runbook.
- `cloudbuild-api.yaml`: builds and pushes the API image.
- `cloudbuild-web.yaml`: builds and pushes the web image.
- `service-api.yaml`: example Cloud Run service for the API.
- `service-web.yaml`: example Cloud Run service for the frontend.
- `env.example`: copy/paste template for local deployment variables.

## Required Secrets

Create these in Secret Manager:

- `COCKROACH_CONNECTION_STRING`
- `JWT_SECRET`
- `ZABBIX_API_TOKEN`
- `GRAFANA_API_TOKEN`
- `TELEGRAM_BOT_TOKEN`
- `SMTP_PASSWORD`

`COCKROACH_CONNECTION_STRING` should be the CockroachDB Cloud PostgreSQL-compatible connection string, including SSL settings.
