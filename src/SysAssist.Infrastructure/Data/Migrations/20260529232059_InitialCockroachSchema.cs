using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysAssist.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCockroachSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Resource = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Result = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HealthStatus = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    LastHealthCheckAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFetchAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupportsPolling = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsWebhooks = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsActions = table.Column<bool>(type: "boolean", nullable: false),
                    UseFallbackMode = table.Column<bool>(type: "boolean", nullable: false),
                    SafeMode = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_modules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "support_bundle_exports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FileName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    IncludedSectionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_bundle_exports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Component = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Login = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incident_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Target = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incident_events_integration_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "integration_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "integration_health_checks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_health_checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_health_checks_integration_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "integration_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "integration_module_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RiskLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ParameterSchemaJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_module_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_module_actions_integration_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "integration_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "integration_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Value = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    IsSecret = table.Column<bool>(type: "boolean", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ValueType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_settings_integration_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "integration_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_events_integration_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "integration_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Subject = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Body = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RelatedEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_messages_incident_events_RelatedEventId",
                        column: x => x.RelatedEventId,
                        principalTable: "incident_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExecutionResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_requests_incident_events_EventId",
                        column: x => x.EventId,
                        principalTable: "incident_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_approval_requests_integration_module_actions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "integration_module_actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_requests_users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_approval_requests_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "event_recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Classification = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ProbableCause = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    NextStep = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SuggestedActionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_recommendations_incident_events_EventId",
                        column: x => x.EventId,
                        principalTable: "incident_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_recommendations_integration_module_actions_SuggestedA~",
                        column: x => x.SuggestedActionId,
                        principalTable: "integration_module_actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_ActionId",
                table: "approval_requests",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_DecidedByUserId",
                table: "approval_requests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_EventId",
                table: "approval_requests",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_RequestedByUserId",
                table: "approval_requests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_Status",
                table: "approval_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_Actor",
                table: "audit_entries",
                column: "Actor");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_CorrelationId",
                table: "audit_entries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_CreatedAt",
                table: "audit_entries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_event_recommendations_EventId",
                table: "event_recommendations",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_recommendations_SuggestedActionId",
                table: "event_recommendations",
                column: "SuggestedActionId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_CorrelationId",
                table: "incident_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_CreatedAt",
                table: "incident_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_ExternalEventId",
                table: "incident_events",
                column: "ExternalEventId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_ModuleId",
                table: "incident_events",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_Severity",
                table: "incident_events",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_Source",
                table: "incident_events",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_Status",
                table: "incident_events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_integration_health_checks_ModuleId_CheckedAt",
                table: "integration_health_checks",
                columns: new[] { "ModuleId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_module_actions_ModuleId_ActionKey",
                table: "integration_module_actions",
                columns: new[] { "ModuleId", "ActionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_modules_HealthStatus",
                table: "integration_modules",
                column: "HealthStatus");

            migrationBuilder.CreateIndex(
                name: "IX_integration_modules_IsEnabled",
                table: "integration_modules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_integration_modules_Key",
                table: "integration_modules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_settings_ModuleId_Key",
                table: "integration_settings",
                columns: new[] { "ModuleId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_messages_CreatedAt",
                table: "notification_messages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_notification_messages_RelatedEventId",
                table: "notification_messages",
                column: "RelatedEventId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_messages_Status",
                table: "notification_messages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_bundle_exports_CreatedAt",
                table: "support_bundle_exports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_Component",
                table: "system_logs",
                column: "Component");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_CorrelationId",
                table: "system_logs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_CreatedAt",
                table: "system_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_Level",
                table: "system_logs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Login",
                table: "users",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_ExternalEventId",
                table: "webhook_events",
                column: "ExternalEventId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_ModuleId",
                table: "webhook_events",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_ReceivedAt",
                table: "webhook_events",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_Source",
                table: "webhook_events",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_requests");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "event_recommendations");

            migrationBuilder.DropTable(
                name: "integration_health_checks");

            migrationBuilder.DropTable(
                name: "integration_settings");

            migrationBuilder.DropTable(
                name: "notification_messages");

            migrationBuilder.DropTable(
                name: "support_bundle_exports");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "webhook_events");

            migrationBuilder.DropTable(
                name: "integration_module_actions");

            migrationBuilder.DropTable(
                name: "incident_events");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "integration_modules");
        }
    }
}
