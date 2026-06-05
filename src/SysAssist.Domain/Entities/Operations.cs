using SysAssist.Domain.Common;
using SysAssist.Domain.Enums;

namespace SysAssist.Domain.Entities;

public sealed class IncidentEvent : Entity
{
    public string? ExternalEventId { get; set; }
    public required string Source { get; set; }
    public required string EventType { get; set; }
    public EventSeverity Severity { get; set; }
    public required string Target { get; set; }
    public EventStatus Status { get; set; } = EventStatus.New;
    public string? CorrelationId { get; set; }
    public required string Summary { get; set; }
    public string? PayloadJson { get; set; }
    public Guid? RecommendationId { get; set; }
    public EventRecommendation? Recommendation { get; set; }
    public Guid? ModuleId { get; set; }
    public IntegrationModule? Module { get; set; }
    public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = [];
    public ICollection<NotificationMessage> NotificationMessages { get; set; } = [];
}

public sealed class EventRecommendation : Entity
{
    public Guid EventId { get; set; }
    public IncidentEvent? Event { get; set; }
    public required string Classification { get; set; }
    public required string Explanation { get; set; }
    public decimal Confidence { get; set; }
    public string? ProbableCause { get; set; }
    public string? NextStep { get; set; }
    public Guid? SuggestedActionId { get; set; }
    public IntegrationModuleAction? SuggestedAction { get; set; }
}

public sealed class ApprovalRequest : Entity
{
    public Guid EventId { get; set; }
    public IncidentEvent? Event { get; set; }
    public Guid ActionId { get; set; }
    public IntegrationModuleAction? Action { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
    public string? DecisionComment { get; set; }
    public string? ExecutionResultJson { get; set; }
}

public sealed class AuditEntry : Entity
{
    public required string Actor { get; set; }
    public required string Action { get; set; }
    public required string Resource { get; set; }
    public required string Result { get; set; }
    public string? Details { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class SystemLog : Entity
{
    public required string Level { get; set; }
    public required string Component { get; set; }
    public required string Message { get; set; }
    public string? CorrelationId { get; set; }
    public string? DetailsJson { get; set; }
}

public sealed class NotificationMessage : Entity
{
    public required string Channel { get; set; }
    public required string Recipient { get; set; }
    public string? Subject { get; set; }
    public required string Body { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.LocalOnly;
    public string? ErrorMessage { get; set; }
    public Guid? RelatedEventId { get; set; }
    public IncidentEvent? RelatedEvent { get; set; }
}

public sealed class WebhookEvent : Entity
{
    public Guid? ModuleId { get; set; }
    public IntegrationModule? Module { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public required string Source { get; set; }
    public string? ExternalEventId { get; set; }
    public required string PayloadJson { get; set; }
    public bool SignatureValid { get; set; }
    public required string ProcessingStatus { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class SupportBundleExport : Entity
{
    public required string RequestedBy { get; set; }
    public required string FileName { get; set; }
    public required string IncludedSectionsJson { get; set; }
}
