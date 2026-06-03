using Microsoft.EntityFrameworkCore;
using SysAssist.Domain.Entities;

namespace SysAssist.Infrastructure.Data;

public sealed class SysAssistDbContext(DbContextOptions<SysAssistDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<IntegrationModule> IntegrationModules => Set<IntegrationModule>();
    public DbSet<IntegrationSetting> IntegrationSettings => Set<IntegrationSetting>();
    public DbSet<IntegrationHealthCheck> IntegrationHealthChecks => Set<IntegrationHealthCheck>();
    public DbSet<IntegrationModuleAction> IntegrationModuleActions => Set<IntegrationModuleAction>();
    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();
    public DbSet<EventRecommendation> EventRecommendations => Set<EventRecommendation>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<SupportBundleExport> SupportBundleExports => Set<SupportBundleExport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUsers(modelBuilder);
        ConfigureIntegrations(modelBuilder);
        ConfigureOperations(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Login).HasMaxLength(80).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(240).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(user => user.Login).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.Name).HasMaxLength(80).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(512);
            entity.HasIndex(role => role.Name).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
            entity.HasOne(userRole => userRole.User)
                .WithMany(user => user.UserRoles)
                .HasForeignKey(userRole => userRole.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(userRole => userRole.Role)
                .WithMany(role => role.UserRoles)
                .HasForeignKey(userRole => userRole.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureIntegrations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntegrationModule>(entity =>
        {
            entity.ToTable("integration_modules");
            entity.HasKey(module => module.Id);
            entity.Property(module => module.Key).HasMaxLength(120).IsRequired();
            entity.Property(module => module.Name).HasMaxLength(160).IsRequired();
            entity.Property(module => module.Type).HasConversion<string>().HasMaxLength(48);
            entity.Property(module => module.Description).HasMaxLength(1024);
            entity.Property(module => module.HealthStatus).HasConversion<string>().HasMaxLength(48);
            entity.HasIndex(module => module.Key).IsUnique();
            entity.HasIndex(module => module.IsEnabled);
            entity.HasIndex(module => module.HealthStatus);
        });

        modelBuilder.Entity<IntegrationSetting>(entity =>
        {
            entity.ToTable("integration_settings");
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Key).HasMaxLength(120).IsRequired();
            entity.Property(setting => setting.Value).HasMaxLength(4096);
            entity.Property(setting => setting.ValueType).HasMaxLength(32).IsRequired();
            entity.Property(setting => setting.Description).HasMaxLength(512);
            entity.HasIndex(setting => new { setting.ModuleId, setting.Key }).IsUnique();
            entity.HasOne(setting => setting.Module)
                .WithMany(module => module.Settings)
                .HasForeignKey(setting => setting.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationHealthCheck>(entity =>
        {
            entity.ToTable("integration_health_checks");
            entity.HasKey(check => check.Id);
            entity.Property(check => check.Status).HasConversion<string>().HasMaxLength(48);
            entity.Property(check => check.Message).HasMaxLength(1024);
            entity.Property(check => check.DetailsJson).HasColumnType("jsonb");
            entity.HasIndex(check => new { check.ModuleId, check.CheckedAt });
            entity.HasOne(check => check.Module)
                .WithMany(module => module.HealthChecks)
                .HasForeignKey(check => check.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationModuleAction>(entity =>
        {
            entity.ToTable("integration_module_actions");
            entity.HasKey(action => action.Id);
            entity.Property(action => action.ActionKey).HasMaxLength(120).IsRequired();
            entity.Property(action => action.Name).HasMaxLength(160).IsRequired();
            entity.Property(action => action.Description).HasMaxLength(1024);
            entity.Property(action => action.RiskLevel).HasConversion<string>().HasMaxLength(48);
            entity.Property(action => action.ParameterSchemaJson).HasColumnType("jsonb");
            entity.HasIndex(action => new { action.ModuleId, action.ActionKey }).IsUnique();
            entity.HasOne(action => action.Module)
                .WithMany(module => module.Actions)
                .HasForeignKey(action => action.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncidentEvent>(entity =>
        {
            entity.ToTable("incident_events");
            entity.HasKey(incident => incident.Id);
            entity.Property(incident => incident.ExternalEventId).HasMaxLength(160);
            entity.Property(incident => incident.Source).HasMaxLength(120).IsRequired();
            entity.Property(incident => incident.EventType).HasMaxLength(120).IsRequired();
            entity.Property(incident => incident.Severity).HasConversion<string>().HasMaxLength(48);
            entity.Property(incident => incident.Target).HasMaxLength(240).IsRequired();
            entity.Property(incident => incident.Status).HasConversion<string>().HasMaxLength(48);
            entity.Property(incident => incident.CorrelationId).HasMaxLength(160);
            entity.Property(incident => incident.Summary).HasMaxLength(1024).IsRequired();
            entity.Property(incident => incident.PayloadJson).HasColumnType("jsonb");
            entity.HasIndex(incident => incident.CreatedAt);
            entity.HasIndex(incident => incident.Source);
            entity.HasIndex(incident => incident.Status);
            entity.HasIndex(incident => incident.Severity);
            entity.HasIndex(incident => incident.CorrelationId);
            entity.HasIndex(incident => incident.ExternalEventId);
            entity.HasOne(incident => incident.Module)
                .WithMany()
                .HasForeignKey(incident => incident.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EventRecommendation>(entity =>
        {
            entity.ToTable("event_recommendations");
            entity.HasKey(recommendation => recommendation.Id);
            entity.Property(recommendation => recommendation.Classification).HasMaxLength(120).IsRequired();
            entity.Property(recommendation => recommendation.Explanation).HasMaxLength(2048).IsRequired();
            entity.Property(recommendation => recommendation.Confidence).HasPrecision(5, 4);
            entity.Property(recommendation => recommendation.ProbableCause).HasMaxLength(1024);
            entity.Property(recommendation => recommendation.NextStep).HasMaxLength(1024);
            entity.HasIndex(recommendation => recommendation.EventId).IsUnique();
            entity.HasOne(recommendation => recommendation.Event)
                .WithOne(incident => incident.Recommendation)
                .HasForeignKey<EventRecommendation>(recommendation => recommendation.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(recommendation => recommendation.SuggestedAction)
                .WithMany()
                .HasForeignKey(recommendation => recommendation.SuggestedActionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("approval_requests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(48);
            entity.Property(request => request.DecisionComment).HasMaxLength(2048);
            entity.Property(request => request.ExecutionResultJson).HasColumnType("jsonb");
            entity.HasIndex(request => request.Status);
            entity.HasIndex(request => request.EventId);
            entity.HasOne(request => request.Event)
                .WithMany(incident => incident.ApprovalRequests)
                .HasForeignKey(request => request.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(request => request.Action)
                .WithMany()
                .HasForeignKey(request => request.ActionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.RequestedByUser)
                .WithMany()
                .HasForeignKey(request => request.RequestedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(request => request.DecidedByUser)
                .WithMany()
                .HasForeignKey(request => request.DecidedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Actor).HasMaxLength(160).IsRequired();
            entity.Property(audit => audit.Action).HasMaxLength(160).IsRequired();
            entity.Property(audit => audit.Resource).HasMaxLength(240).IsRequired();
            entity.Property(audit => audit.Result).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Details).HasMaxLength(4096);
            entity.Property(audit => audit.CorrelationId).HasMaxLength(160);
            entity.HasIndex(audit => audit.CreatedAt);
            entity.HasIndex(audit => audit.Actor);
            entity.HasIndex(audit => audit.CorrelationId);
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.ToTable("system_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Level).HasMaxLength(48).IsRequired();
            entity.Property(log => log.Component).HasMaxLength(160).IsRequired();
            entity.Property(log => log.Message).HasMaxLength(2048).IsRequired();
            entity.Property(log => log.CorrelationId).HasMaxLength(160);
            entity.Property(log => log.DetailsJson).HasColumnType("jsonb");
            entity.HasIndex(log => log.CreatedAt);
            entity.HasIndex(log => log.Level);
            entity.HasIndex(log => log.Component);
            entity.HasIndex(log => log.CorrelationId);
        });

        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.ToTable("notification_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Channel).HasMaxLength(80).IsRequired();
            entity.Property(message => message.Recipient).HasMaxLength(240).IsRequired();
            entity.Property(message => message.Subject).HasMaxLength(240);
            entity.Property(message => message.Body).HasMaxLength(4096).IsRequired();
            entity.Property(message => message.Status).HasConversion<string>().HasMaxLength(48);
            entity.Property(message => message.ErrorMessage).HasMaxLength(2048);
            entity.HasIndex(message => message.CreatedAt);
            entity.HasIndex(message => message.Status);
            entity.HasOne(message => message.RelatedEvent)
                .WithMany(incident => incident.NotificationMessages)
                .HasForeignKey(message => message.RelatedEventId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("webhook_events");
            entity.HasKey(webhook => webhook.Id);
            entity.Property(webhook => webhook.Source).HasMaxLength(120).IsRequired();
            entity.Property(webhook => webhook.ExternalEventId).HasMaxLength(160);
            entity.Property(webhook => webhook.PayloadJson).HasColumnType("jsonb");
            entity.Property(webhook => webhook.ProcessingStatus).HasMaxLength(80).IsRequired();
            entity.Property(webhook => webhook.ErrorMessage).HasMaxLength(2048);
            entity.HasIndex(webhook => webhook.Source);
            entity.HasIndex(webhook => webhook.ExternalEventId);
            entity.HasIndex(webhook => webhook.ReceivedAt);
            entity.HasOne(webhook => webhook.Module)
                .WithMany()
                .HasForeignKey(webhook => webhook.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SupportBundleExport>(entity =>
        {
            entity.ToTable("support_bundle_exports");
            entity.HasKey(export => export.Id);
            entity.Property(export => export.RequestedBy).HasMaxLength(160).IsRequired();
            entity.Property(export => export.FileName).HasMaxLength(240).IsRequired();
            entity.Property(export => export.IncludedSectionsJson).HasColumnType("jsonb");
            entity.HasIndex(export => export.CreatedAt);
        });
    }
}
