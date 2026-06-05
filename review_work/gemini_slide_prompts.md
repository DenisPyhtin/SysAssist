# Отдельные промпты для Gemini по слайдам защиты SysAssist

## Общий стиль для всех слайдов

Используй единый стиль: строгая инженерная презентация для защиты диплома, формат 16:9, темный фон в стиле operator console, без маркетинговых клише и без людей в офисе. Палитра: графитовый, почти черный, темный коричнево-серый, акценты спокойный teal/green для готовности и amber для предупреждений. Шрифт современный sans-serif, крупные заголовки, не больше 3-4 коротких тезисов на слайде. Визуалы должны выглядеть как технические схемы, UI-фрагменты, status cards, flow diagrams, architecture maps. Не добавляй факты, которых нет в промпте. Не пиши, что система промышленно внедрена.

---

## Слайд 1. Титульный слайд

**Длительность:** 30 секунд

**Промпт для Gemini:**

Создай титульный слайд 16:9 для дипломной защиты. Тема: "SysAssist - веб-приложение для автоматизации обработки ИТ-инцидентов и регламентированных действий". Подзаголовок: ".NET 9, React, CockroachDB, SafeMode, approval workflow". Визуал: темная операторская консоль, схематичный контур мониторинга и инцидентов, справа небольшая архитектурная сетка с узлами Web, API, DB, Modules, Audit. Стиль строгий, технический, без рекламного вида. Добавь небольшую строку внизу: "Дипломный проект, 2026". Не перегружай текстом.

---

## Слайд 2. Проблема и актуальность

**Длительность:** 45 секунд

**Промпт для Gemini:**

Создай слайд "Проблема". Покажи, что мониторинг находит событие, но дальнейшая реакция часто распадается на чаты, логи, ручные таблицы и отдельные дашборды. Визуал: слева несколько разрозненных источников - Monitoring, Logs, Chat, Manual checklist; справа единая цепочка SysAssist. Тезисы: "нет единого маршрута реакции", "не всегда видно, кто принял решение", "сложно доказать результат после инцидента". Стиль: темный technical operations, аккуратные линии, amber-warning акценты.

---

## Слайд 3. Цель и задачи проекта

**Длительность:** 40 секунд

**Промпт для Gemini:**

Создай слайд "Цель проекта". Главный текст: "Разработать SysAssist как локально разворачиваемую платформу для обработки ИТ-инцидентов". Ниже 5 компактных задач в виде вертикального checklist: "архитектура и стек", "модель данных и API", "модульный каталог", "роли, SafeMode и approval", "тестирование и readiness". Визуал: checklist рядом с небольшим контуром incident lifecycle. Стиль спокойный, академический, не маркетинговый. Используй зеленые check-акценты, но не делай слайд слишком ярким.

---

## Слайд 4. Архитектура решения

**Длительность:** 55 секунд

**Промпт для Gemini:**

Создай архитектурный слайд "Архитектура SysAssist". Нарисуй layered diagram: React + Vite Web UI -> ASP.NET Core Minimal API -> Application/Contracts -> Infrastructure -> CockroachDB/Npgsql -> Integration Adapters. Отдельно покажи Domain как внутренний слой с сущностями IncidentEvent, ApprovalRequest, AuditEntry. Внизу подпиши проекты: Domain, Application, Contracts, Infrastructure, Api, Web. Стиль: темная техническая схема, тонкие линии, аккуратные подписи, без 3D и без лишних декоративных элементов.

---

## Слайд 5. Технологический стек и база данных

**Длительность:** 45 секунд

**Промпт для Gemini:**

Создай слайд "Стек и данные". Визуально раздели на три блока: Backend - ".NET 9, C#, ASP.NET Core Minimal API"; Frontend - "React, TypeScript, Vite"; Database - "CockroachDB, EF Core, Npgsql, JSONB". Справа добавь мини-схему базы данных с ключевыми сущностями: Users/Roles, IntegrationModules, IncidentEvents, Recommendations, ApprovalRequests, AuditEntries, SupportBundleExports. Стиль: status dashboard, чистая техническая инфографика, без длинных абзацев.

---

## Слайд 6. Жизненный цикл инцидента

**Длительность:** 60 секунд

**Промпт для Gemini:**

Создай слайд "Жизненный цикл инцидента". Нарисуй горизонтальный процесс: Webhook/Polling -> IncidentEvent -> Recommendation -> Risk check -> Approval или SafeMode -> Action result -> Audit + Support bundle. Используй цветовую логику: green для безопасных шагов, amber для risk/approval, gray для audit evidence. Короткие подписи: "нормализация", "рекомендация", "оценка риска", "согласование", "фиксация результата". Не утверждай, что опасные действия всегда реально выполняются: покажи вариант блокировки SafeMode.

---

## Слайд 7. Модули и действия

**Длительность:** 45 секунд

**Промпт для Gemini:**

Создай слайд "Модульный каталог". Покажи сетку из 13 модулей: Zabbix, Grafana, Prometheus Alertmanager, PostgreSQL, Redis, Docker, Nginx, Linux Host, HTTP Endpoint Checker, File System Monitor, SMTP Email, Telegram Bot, Local Rule Advisor. В центре крупно: "13 модулей" и "65 remediation actions". Добавь три маленьких индикатора: "health checks", "settings + secrets", "SafeMode + fallback controls". Стиль: темная operator console, карточки модулей, аккуратные иконки, без чрезмерной декоративности.

---

## Слайд 8. Безопасность и контроль риска

**Длительность:** 60 секунд

**Промпт для Gemini:**

Создай слайд "Безопасность и контроль риска". Сделай схему из 5 защитных слоев: JWT authentication, RBAC roles, Approval workflow, SafeMode, AES-GCM secret protection. Отдельной карточкой покажи "Support bundle без секретов": no password hashes, no raw secrets, no JWT keys, no license tokens. Добавь небольшую ветку: "опасное действие -> approval -> SafeMode может заблокировать реальное выполнение". Стиль строгий, security engineering, с lock/check icons, amber для риска и green для контроля.

---

## Слайд 9. Deployment, readiness и тестирование

**Длительность:** 55 секунд

**Промпт для Gemini:**

Создай слайд "Проверка готовности". Визуал: production readiness gate как панель с gates: Production environment, demo data off, strong JWT secret, encryption key, valid license, CockroachDB ready, diagnostics fresh, enabled modules healthy, fallback off, SafeMode on. Слева покажи deployment flow: docker-compose local lab -> production-shaped compose -> migrator -> api -> web/reverse proxy. Справа добавь testing block: "unit tests", "security tests", "workflow tests", "prod-smoke.ps1". Не пиши, что промышленное внедрение завершено. Пиши: "готово для controlled demo и пилота".

---

## Слайд 10. Итоги, ограничения и развитие

**Длительность:** 50 секунд

**Промпт для Gemini:**

Создай заключительный слайд "Итог работы". Основной тезис крупно: "SysAssist связывает событие, решение, действие и доказательства в единую цепочку". Ниже три блока: "Реализовано" - backend, frontend, CockroachDB, modules, approval, SafeMode, audit, support bundle, readiness; "Ограничения" - нужны реальные внешние настройки, TLS, backup, security audit, мониторинг; "Развитие" - SSO/OIDC, immutable audit sink, WAF/IAP, provider-specific webhook signatures, scheduler. Стиль уверенный, спокойный, без саморекламы. Финальная строка: "Проект готов для controlled demo и пилотного разворачивания".

---

## Рекомендуемый порядок показа и речь

1. Слайд 1 - 0:30
2. Слайд 2 - 0:45
3. Слайд 3 - 0:40
4. Слайд 4 - 0:55
5. Слайд 5 - 0:45
6. Слайд 6 - 1:00
7. Слайд 7 - 0:45
8. Слайд 8 - 1:00
9. Слайд 9 - 0:55
10. Слайд 10 - 0:50

Итого: примерно 7 минут 5 секунд. При выступлении можно чуть ускорить слайды 4 или 9, чтобы уложиться ровно в 7 минут.
