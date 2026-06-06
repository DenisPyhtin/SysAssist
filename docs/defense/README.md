# SysAssist — презентация защиты проекта

> **Тема:** «Разработка программы для автоматизации и оптимизации работы системных администраторов средних и крупных предприятий»

**Описание:**
SysAssist — веб-приложение для системных администраторов. Система обрабатывает события, сохраняет их в базе, формирует рекомендации, проверяет риск, запускает согласование, выполняет действия и фиксирует результаты в аудите.

## 1. Скриншоты интерфейса
![Dashboard](../screenshots/01_dashboard.png)
![Modules](../screenshots/02_modules.png)
![Approvals](../screenshots/04_approvals.png)
![Audit Trail](../screenshots/05_audit.png)
![Diagnostics](../screenshots/06_diagnostics.png)

## 2. Главная цепочка событий
```mermaid
flowchart LR
    A[Событие] --> B[IncidentEvent]
    B --> C[EventRecommendation]
    C --> D{RiskLevel}
    D -->|Low / Medium| E[Action / SafeMode]
    D -->|High / Critical| F[ApprovalRequest]
    F --> E
    E --> G[AuditEntry]
    G --> H[Support Bundle]
```

## 3. ISO 9001 — процесс обработки события
```mermaid
flowchart TD
    A[Вход события] --> B[Нормализация]
    B --> C[Сохранение IncidentEvent]
    C --> D[Формирование EventRecommendation]
    D --> E{Проверка риска}
    E -->|Low / Medium| F[Action / SafeMode]
    E -->|High / Critical| G[ApprovalRequest]
    G --> F
    F --> H[AuditEntry]
    H --> I[SystemLogs и Notifications]
    I --> J[Support Bundle]
    J --> K[Обработанный инцидент]
```

## 4. ER-диаграмма базы данных
```mermaid
erDiagram
    users ||--o{ user_roles : user_id
    roles ||--o{ user_roles : role_id
    integration_modules ||--o{ integration_settings : module_id
    integration_modules ||--o{ integration_health_checks : module_id
    integration_modules ||--o{ integration_module_actions : module_id
    integration_modules ||--o{ incident_events : module_id
    incident_events ||--|| event_recommendations : event_id
    integration_module_actions ||--o{ event_recommendations : suggested_action_id
    incident_events ||--o{ approval_requests : event_id
    integration_module_actions ||--o{ approval_requests : action_id
    users ||--o{ approval_requests : requested_by_user_id
    users ||--o{ approval_requests : decided_by_user_id
    incident_events ||--o{ notification_messages : related_event_id
```

## 5. Структура классов и ООП
```mermaid
flowchart TB
    User --> UserRole
    Role --> UserRole
    IntegrationModule --> IntegrationSetting
    IntegrationModule --> IntegrationHealthCheck
    IntegrationModule --> IntegrationModuleAction
    IntegrationModule --> IncidentEvent
    IncidentEvent --> EventRecommendation
    IncidentEvent --> ApprovalRequest
    IntegrationModuleAction --> ApprovalRequest
    User --> ApprovalRequest
    IncidentEvent --> NotificationMessage
```
- **Инкапсуляция:** внутренние поля скрыты, доступ через свойства и методы
- **Абстракция:** интеграции через `IIntegrationAdapter`
- **Полиморфизм:** разные адаптеры используют один интерфейс
- **Dependency Injection:** сервисы и адаптеры через DI

## 6. Процесс работы события (для видео)
1. Login → Dashboard
2. Поступление IncidentEvent → EventRecommendation
3. Проверка риска → ApprovalRequest при высоком риске
4. Action / SafeMode
5. AuditEntry → Support Bundle

## 7. Docker и Git
```bash
git clone <repo-url> sysassist
cd sysassist
cp .env.example .env
# заполнить переменные .env
docker compose up -d --build
```
Проверка:
```bash
docker compose ps
docker compose logs -f sysassist-api
```
Перезапуск / остановка:
```bash
docker compose restart
docker compose down
```
Обновление:
```bash
git pull
docker compose up -d --build
```
Проверка API:
```bash
curl http://localhost:5000/health
```

---
**Презентация готова для показа на защите.**