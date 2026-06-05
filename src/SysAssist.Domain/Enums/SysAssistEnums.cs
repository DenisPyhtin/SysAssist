namespace SysAssist.Domain.Enums;

public enum ModuleType
{
    Monitoring = 0,
    Alerting = 1,
    Database = 2,
    Infrastructure = 3,
    Notification = 4,
    LocalCheck = 5,
    Advisor = 6
}

public enum HealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Warning = 2,
    Error = 3,
    NotConfigured = 4,
    Disabled = 5
}

public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum EventSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

public enum EventStatus
{
    New = 0,
    Analyzing = 1,
    PendingApproval = 2,
    Completed = 3,
    Rejected = 4,
    Failed = 5
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Executed = 3,
    Failed = 4
}

public enum NotificationStatus
{
    LocalOnly = 0,
    Pending = 1,
    Sent = 2,
    Failed = 3,
    Read = 4
}
