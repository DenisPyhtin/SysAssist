CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE audit_entries (
        "Id" uuid NOT NULL,
        "Actor" character varying(160) NOT NULL,
        "Action" character varying(160) NOT NULL,
        "Resource" character varying(240) NOT NULL,
        "Result" character varying(80) NOT NULL,
        "Details" character varying(4096),
        "CorrelationId" character varying(160),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_audit_entries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE integration_modules (
        "Id" uuid NOT NULL,
        "Key" character varying(120) NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Type" character varying(48) NOT NULL,
        "Description" character varying(1024),
        "IsEnabled" boolean NOT NULL,
        "HealthStatus" character varying(48) NOT NULL,
        "LastHealthCheckAt" timestamp with time zone,
        "LastFetchAt" timestamp with time zone,
        "SupportsPolling" boolean NOT NULL,
        "SupportsWebhooks" boolean NOT NULL,
        "SupportsActions" boolean NOT NULL,
        "UseFallbackMode" boolean NOT NULL,
        "SafeMode" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_integration_modules" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE roles (
        "Id" uuid NOT NULL,
        "Name" character varying(80) NOT NULL,
        "Description" character varying(512),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE support_bundle_exports (
        "Id" uuid NOT NULL,
        "RequestedBy" character varying(160) NOT NULL,
        "FileName" character varying(240) NOT NULL,
        "IncludedSectionsJson" jsonb NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_support_bundle_exports" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE system_logs (
        "Id" uuid NOT NULL,
        "Level" character varying(48) NOT NULL,
        "Component" character varying(160) NOT NULL,
        "Message" character varying(2048) NOT NULL,
        "CorrelationId" character varying(160),
        "DetailsJson" jsonb,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_system_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "Login" character varying(80) NOT NULL,
        "DisplayName" character varying(160) NOT NULL,
        "Email" character varying(240) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE incident_events (
        "Id" uuid NOT NULL,
        "ExternalEventId" character varying(160),
        "Source" character varying(120) NOT NULL,
        "EventType" character varying(120) NOT NULL,
        "Severity" character varying(48) NOT NULL,
        "Target" character varying(240) NOT NULL,
        "Status" character varying(48) NOT NULL,
        "CorrelationId" character varying(160),
        "Summary" character varying(1024) NOT NULL,
        "PayloadJson" jsonb,
        "RecommendationId" uuid,
        "ModuleId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_incident_events" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_incident_events_integration_modules_ModuleId" FOREIGN KEY ("ModuleId") REFERENCES integration_modules ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE integration_health_checks (
        "Id" uuid NOT NULL,
        "ModuleId" uuid NOT NULL,
        "CheckedAt" timestamp with time zone NOT NULL,
        "Status" character varying(48) NOT NULL,
        "LatencyMs" integer,
        "Message" character varying(1024),
        "DetailsJson" jsonb,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_integration_health_checks" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_integration_health_checks_integration_modules_ModuleId" FOREIGN KEY ("ModuleId") REFERENCES integration_modules ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE integration_module_actions (
        "Id" uuid NOT NULL,
        "ModuleId" uuid NOT NULL,
        "ActionKey" character varying(120) NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Description" character varying(1024),
        "RiskLevel" character varying(48) NOT NULL,
        "RequiresApproval" boolean NOT NULL,
        "IsEnabled" boolean NOT NULL,
        "ParameterSchemaJson" jsonb,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_integration_module_actions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_integration_module_actions_integration_modules_ModuleId" FOREIGN KEY ("ModuleId") REFERENCES integration_modules ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE integration_settings (
        "Id" uuid NOT NULL,
        "ModuleId" uuid NOT NULL,
        "Key" character varying(120) NOT NULL,
        "Value" character varying(4096),
        "IsSecret" boolean NOT NULL,
        "IsRequired" boolean NOT NULL,
        "ValueType" character varying(32) NOT NULL,
        "Description" character varying(512),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_integration_settings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_integration_settings_integration_modules_ModuleId" FOREIGN KEY ("ModuleId") REFERENCES integration_modules ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE webhook_events (
        "Id" uuid NOT NULL,
        "ModuleId" uuid,
        "ReceivedAt" timestamp with time zone NOT NULL,
        "Source" character varying(120) NOT NULL,
        "ExternalEventId" character varying(160),
        "PayloadJson" jsonb NOT NULL,
        "SignatureValid" boolean NOT NULL,
        "ProcessingStatus" character varying(80) NOT NULL,
        "ErrorMessage" character varying(2048),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_webhook_events" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_webhook_events_integration_modules_ModuleId" FOREIGN KEY ("ModuleId") REFERENCES integration_modules ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE user_roles (
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        CONSTRAINT "PK_user_roles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_user_roles_roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES roles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_user_roles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE notification_messages (
        "Id" uuid NOT NULL,
        "Channel" character varying(80) NOT NULL,
        "Recipient" character varying(240) NOT NULL,
        "Subject" character varying(240),
        "Body" character varying(4096) NOT NULL,
        "Status" character varying(48) NOT NULL,
        "ErrorMessage" character varying(2048),
        "RelatedEventId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_notification_messages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_notification_messages_incident_events_RelatedEventId" FOREIGN KEY ("RelatedEventId") REFERENCES incident_events ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE approval_requests (
        "Id" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "ActionId" uuid NOT NULL,
        "Status" character varying(48) NOT NULL,
        "RequestedAt" timestamp with time zone NOT NULL,
        "RequestedByUserId" uuid,
        "DecidedAt" timestamp with time zone,
        "DecidedByUserId" uuid,
        "DecisionComment" character varying(2048),
        "ExecutionResultJson" jsonb,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_approval_requests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_approval_requests_incident_events_EventId" FOREIGN KEY ("EventId") REFERENCES incident_events ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_approval_requests_integration_module_actions_ActionId" FOREIGN KEY ("ActionId") REFERENCES integration_module_actions ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_approval_requests_users_DecidedByUserId" FOREIGN KEY ("DecidedByUserId") REFERENCES users ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_approval_requests_users_RequestedByUserId" FOREIGN KEY ("RequestedByUserId") REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE TABLE event_recommendations (
        "Id" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "Classification" character varying(120) NOT NULL,
        "Explanation" character varying(2048) NOT NULL,
        "Confidence" numeric(5,4) NOT NULL,
        "ProbableCause" character varying(1024),
        "NextStep" character varying(1024),
        "SuggestedActionId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_event_recommendations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_event_recommendations_incident_events_EventId" FOREIGN KEY ("EventId") REFERENCES incident_events ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_event_recommendations_integration_module_actions_SuggestedA~" FOREIGN KEY ("SuggestedActionId") REFERENCES integration_module_actions ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_approval_requests_ActionId" ON approval_requests ("ActionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_approval_requests_DecidedByUserId" ON approval_requests ("DecidedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_approval_requests_EventId" ON approval_requests ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_approval_requests_RequestedByUserId" ON approval_requests ("RequestedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_approval_requests_Status" ON approval_requests ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_audit_entries_Actor" ON audit_entries ("Actor");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_audit_entries_CorrelationId" ON audit_entries ("CorrelationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_audit_entries_CreatedAt" ON audit_entries ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE UNIQUE INDEX "IX_event_recommendations_EventId" ON event_recommendations ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_event_recommendations_SuggestedActionId" ON event_recommendations ("SuggestedActionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_incident_events_CorrelationId" ON incident_events ("CorrelationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_incident_events_CreatedAt" ON incident_events ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_incident_events_ExternalEventId" ON incident_events ("ExternalEventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_incident_events_ModuleId" ON incident_events ("ModuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_incident_events_Severity" ON incident_events ("Severity");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_incident_events_Source" ON incident_events ("Source");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_incident_events_Status" ON incident_events ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_integration_health_checks_ModuleId_CheckedAt" ON integration_health_checks ("ModuleId", "CheckedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE UNIQUE INDEX "IX_integration_module_actions_ModuleId_ActionKey" ON integration_module_actions ("ModuleId", "ActionKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_integration_modules_HealthStatus" ON integration_modules ("HealthStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_integration_modules_IsEnabled" ON integration_modules ("IsEnabled");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE UNIQUE INDEX "IX_integration_modules_Key" ON integration_modules ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE UNIQUE INDEX "IX_integration_settings_ModuleId_Key" ON integration_settings ("ModuleId", "Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_notification_messages_CreatedAt" ON notification_messages ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_notification_messages_RelatedEventId" ON notification_messages ("RelatedEventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_notification_messages_Status" ON notification_messages ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE UNIQUE INDEX "IX_roles_Name" ON roles ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_support_bundle_exports_CreatedAt" ON support_bundle_exports ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_system_logs_Component" ON system_logs ("Component");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_system_logs_CorrelationId" ON system_logs ("CorrelationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_system_logs_CreatedAt" ON system_logs ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_system_logs_Level" ON system_logs ("Level");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_user_roles_RoleId" ON user_roles ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE UNIQUE INDEX "IX_users_Login" ON users ("Login");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_webhook_events_ExternalEventId" ON webhook_events ("ExternalEventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_webhook_events_ModuleId" ON webhook_events ("ModuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_webhook_events_ReceivedAt" ON webhook_events ("ReceivedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    CREATE INDEX "IX_webhook_events_Source" ON webhook_events ("Source");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529232059_InitialCockroachSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260529232059_InitialCockroachSchema', '9.0.14');
    END IF;
END $EF$;
COMMIT;

