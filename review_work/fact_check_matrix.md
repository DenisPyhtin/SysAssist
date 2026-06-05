# Fact-check matrix

| Утверждение | Статус | Подтверждение / исправление |
| --- | --- | --- |
| Backend .NET 9 и ASP.NET Core Minimal API | Подтверждено | src/SysAssist.Api/SysAssist.Api.csproj: TargetFramework net9.0; Program.cs: MapGet/MapPost groups |
| CockroachDB через EF Core/Npgsql | Подтверждено | src/SysAssist.Infrastructure.csproj: Npgsql.EntityFrameworkCore.PostgreSQL; SysAssistDbContext; migration InitialCockroachSchema |
| Frontend React, TypeScript, Vite | Подтверждено | src/SysAssist.Web/package.json: react, typescript, vite |
| Архитектура Domain/Application/Contracts/Infrastructure/Api/Web | Подтверждено | README.md и структура src/* |
| 13 встроенных модулей | Подтверждено | ModuleCatalog.cs: Zabbix, Grafana, Alertmanager, PostgreSQL, Redis, Docker, Nginx, Linux Host, HTTP, File System, SMTP, Telegram, Local Rule Advisor |
| 65 remediation actions | Подтверждено | RemediationActionCatalog.cs: 13 модулей по 5 actions |
| SafeMode блокирует реальные действия | Подтверждено | SysAssistApiService.cs: при module.SafeMode approved action не исполняется и аудитируется ACTION_BLOCKED |
| Fallback Mode не является production-режимом | Подтверждено | ModuleCatalog.cs: UseFallbackMode Deprecated; production gate требует отключения fallback |
| Approval workflow для high/critical actions | Подтверждено | SysAssistApiService.cs: RequiresApproval/High/Critical создают ApprovalRequest |
| Audit log и support bundle | Подтверждено | SysAssistApiService.cs: AuditAsync, GetSupportBundleFileAsync; tests проверяют отсутствие секретов |
| Production deployment | Частично подтверждено | docker-compose.prod.yml и docs/server-deployment.md подготовлены; промышленное внедрение не заявляется |
| SSO/OIDC | Не подтверждено как реализация | docs/prod-readiness.md относит SSO/OIDC к Current Product Gaps; в дипломе оставлено как развитие |
