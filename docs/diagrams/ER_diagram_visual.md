# ER-диаграмма базы данных SysAssist

Ниже представлена визуальная ER-диаграмма реальной базы данных SysAssist. Она отражает все реально реализованные таблицы и связи из `DbContext`.

В диаграмме показаны:
- Основные таблицы: `users`, `roles`, `user_roles`, `integration_modules`, `integration_settings`, `integration_health_checks`, `integration_module_actions`, `incident_events`, `event_recommendations`, `approval_requests`, `audit_entries`, `system_logs`, `notification_messages`, `webhook_events`, `support_bundle_exports`
- Все первичные и внешние ключи
- Основные связи 1:1, 1:N и N:M

Используется для презентации без текстового описания полей, только структура и связи между таблицами.

```mermaid
erDiagram
    users ||--o{ user_roles : "user_id"
    roles ||--o{ user_roles : "role_id"
    integration_modules ||--o{ integration_settings : "module_id"
    integration_modules ||--o{ integration_health_checks : "module_id"
    integration_modules ||--o{ integration_module_actions : "module_id"
    integration_modules ||--o{ incident_events : "module_id nullable"
    integration_modules ||--o{ webhook_events : "module_id nullable"
    incident_events ||--|| event_recommendations : "event_id unique"
    integration_module_actions ||--o{ event_recommendations : "suggested_action_id nullable"
    incident_events ||--o{ approval_requests : "event_id"
    integration_module_actions ||--o{ approval_requests : "action_id"
    users ||--o{ approval_requests : "requested_by_user_id nullable"
    users ||--o{ approval_requests : "decided_by_user_id nullable"
    incident_events ||--o{ notification_messages : "related_event_id nullable"
```