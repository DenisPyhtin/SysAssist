# ER-диаграмма базы данных SysAssist

Диаграмма построена строго по `src/SysAssist.Infrastructure/Data/SysAssistDbContext.cs`.

В ней отражены только реально настроенные таблицы, ключи, внешние ключи и связи из `OnModelCreating`.

```mermaid
erDiagram
    users {
        uuid id PK
        string login UK
        string display_name
        string email UK
        string password_hash
    }

    roles {
        uuid id PK
        string name UK
        string description
    }

    user_roles {
        uuid user_id PK, FK
        uuid role_id PK, FK
    }

    integration_modules {
        uuid id PK
        string key UK
        string name
        string type
        string description
        string health_status
        bool is_enabled
    }

    integration_settings {
        uuid id PK
        uuid module_id FK
        string key
        string value
        string value_type
        string description
    }

    integration_health_checks {
        uuid id PK
        uuid module_id FK
        string status
        string message
        jsonb details_json
        datetime checked_at
    }

    integration_module_actions {
        uuid id PK
        uuid module_id FK
        string action_key
        string name
        string description
        string risk_level
        jsonb parameter_schema_json
    }

    incident_events {
        uuid id PK
        uuid module_id FK
        string external_event_id
        string source
        string event_type
        string severity
        string target
        string status
        string correlation_id
        string summary
        jsonb payload_json
        datetime created_at
    }

    event_recommendations {
        uuid id PK
        uuid event_id FK, UK
        uuid suggested_action_id FK
        string classification
        string explanation
        decimal confidence
        string probable_cause
        string next_step
    }

    approval_requests {
        uuid id PK
        uuid event_id FK
        uuid action_id FK
        uuid requested_by_user_id FK
        uuid decided_by_user_id FK
        string status
        string decision_comment
        jsonb execution_result_json
    }

    audit_entries {
        uuid id PK
        string actor
        string action
        string resource
        string result
        string details
        string correlation_id
        datetime created_at
    }

    system_logs {
        uuid id PK
        string level
        string component
        string message
        string correlation_id
        jsonb details_json
        datetime created_at
    }

    notification_messages {
        uuid id PK
        uuid related_event_id FK
        string channel
        string recipient
        string subject
        string body
        string status
        string error_message
        datetime created_at
    }

    webhook_events {
        uuid id PK
        uuid module_id FK
        string source
        string external_event_id
        jsonb payload_json
        string processing_status
        string error_message
        datetime received_at
    }

    support_bundle_exports {
        uuid id PK
        string requested_by
        string file_name
        jsonb included_sections_json
        datetime created_at
    }

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

## Реальные связи из DbContext

| Связь | Тип | Поведение удаления |
|---|---:|---|
| `users` → `user_roles` | 1:N | `Cascade` |
| `roles` → `user_roles` | 1:N | `Cascade` |
| `integration_modules` → `integration_settings` | 1:N | `Cascade` |
| `integration_modules` → `integration_health_checks` | 1:N | `Cascade` |
| `integration_modules` → `integration_module_actions` | 1:N | `Cascade` |
| `integration_modules` → `incident_events` | 1:N | `SetNull` |
| `incident_events` → `event_recommendations` | 1:1 | `Cascade` |
| `integration_module_actions` → `event_recommendations` | 1:N | `SetNull` |
| `incident_events` → `approval_requests` | 1:N | `Cascade` |
| `integration_module_actions` → `approval_requests` | 1:N | `Restrict` |
| `users` → `approval_requests.requested_by_user_id` | 1:N | `SetNull` |
| `users` → `approval_requests.decided_by_user_id` | 1:N | `SetNull` |
| `incident_events` → `notification_messages` | 1:N | `SetNull` |
| `integration_modules` → `webhook_events` | 1:N | `SetNull` |

## Индексы и ограничения

| Таблица | Индексы / ограничения |
|---|---|
| `users` | `login` unique, `email` unique |
| `roles` | `name` unique |
| `user_roles` | composite PK: `user_id + role_id` |
| `integration_modules` | `key` unique, `is_enabled`, `health_status` |
| `integration_settings` | unique: `module_id + key` |
| `integration_health_checks` | `module_id + checked_at` |
| `integration_module_actions` | unique: `module_id + action_key` |
| `incident_events` | `created_at`, `source`, `status`, `severity`, `correlation_id`, `external_event_id` |
| `event_recommendations` | `event_id` unique |
| `approval_requests` | `status`, `event_id` |
| `audit_entries` | `created_at`, `actor`, `correlation_id` |
| `system_logs` | `created_at`, `level`, `component`, `correlation_id` |
| `notification_messages` | `created_at`, `status` |
| `webhook_events` | `source`, `external_event_id`, `received_at` |
| `support_bundle_exports` | `created_at` |

## Главная рабочая цепочка данных

```mermaid
flowchart LR
    M[integration_modules] --> E[incident_events]
    E --> R[event_recommendations]
    R --> A[integration_module_actions]
    E --> Q[approval_requests]
    A --> Q
    Q --> L[audit_entries]
    E --> N[notification_messages]
```

Важно: `audit_entries` в текущей реализации не имеет физического внешнего ключа на `approval_requests` или `incident_events`. Связь с процессом выполняется через поля `actor`, `action`, `resource`, `result`, `details` и `correlation_id`.
