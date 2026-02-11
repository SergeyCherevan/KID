---
name: remove-projecttemplates-from-docs
overview: Убрать из документации `docs/` упоминания и примеры, привязанные к `/ProjectTemplates`, сохранив при необходимости нейтральное описание механизма шаблонного кода без ссылки на конкретную папку.
todos:
  - id: scan-docs
    content: Собрать полный список упоминаний `/ProjectTemplates` в `docs/` (включая вариации путей и примеры файлов).
    status: completed
  - id: edit-readme-docs
    content: Убрать из `docs/README.md` все упоминания `ProjectTemplates` (дерево структуры + раздел про шаблоны и инструкции).
    status: completed
  - id: edit-features-docs
    content: Убрать из `docs/FEATURES.md` разделы/примеры, привязанные к `ProjectTemplates` (категории, пути, инструкции с `TemplateName`).
    status: completed
  - id: edit-development-docs
    content: Убрать `ProjectTemplates` из дерева структуры в `docs/DEVELOPMENT.md`.
    status: completed
  - id: validate-no-mentions
    content: "Перепроверить `docs/` поиском: `ProjectTemplates` и `/ProjectTemplates` — 0 совпадений; убедиться, что markdown не сломан."
    status: completed
isProject: false
---

## 1. Анализ требований
- **Цель**: удалить из `docs/` упоминания «скриптов/шаблонов», которые ссылаются на `/ProjectTemplates` (пути `KID.WPF.IDE/ProjectTemplates/...`, `ProjectTemplates/...`, перечисления `Gold/NextLevel/CheatSheets`, примеры файлов).
- **Сценарии**: читатель открывает документацию и больше не видит подсказок/инструкций, ведущих к `ProjectTemplates`.
- **Вход/выход**:
  - Вход: текущие markdown-файлы в `docs/`.
  - Выход: обновлённые markdown-файлы без упоминаний `/ProjectTemplates`.
- **Ограничения/допущение (по умолчанию)**: удаляем именно привязку к `/ProjectTemplates` и примеры; общие термины `TemplateCode/TemplateName` в архитектурных документах можно оставить, если они не ссылаются на `ProjectTemplates`.

## 2. Архитектурный анализ
- Затрагивается только документация.
- Код/подсистемы не меняются.
- Важный нюанс: в `docs/README.md` и `docs/FEATURES.md` есть отдельные разделы про ProjectTemplates (их нужно удалить/переписать), а в `docs/SUBSYSTEMS.md` и `docs/ARCHITECTURE.md` встречаются общие упоминания `TemplateCode/TemplateName` (без прямой привязки к папке).

## 3. Список задач
### 3.1. Найти все упоминания
- Просканировать `docs/**/*.md` по ключам: `ProjectTemplates`, `/ProjectTemplates`, `TemplateName` (только чтобы понять контекст), `Gold/`, `NextLevel/`, `CheatSheets/`.

### 3.2. Правки конкретных файлов
- [`docs/README.md`](d:/Visual%20Studio%20Projects/KID/docs/README.md)
  - Удалить строку в дереве структуры, где упоминается `ProjectTemplates/`.
  - Удалить весь раздел `## Шаблоны проектов (ProjectTemplates)` и подпункты `### Как открыть шаблон` с примерами `ProjectTemplates/...`.
  - Проверить, не появились «дырки» в оглавлении/переходах; при необходимости заменить 1–2 предложениями нейтрального текста (без путей) или просто убрать без замены.
- [`docs/FEATURES.md`](d:/Visual%20Studio%20Projects/KID/docs/FEATURES.md)
  - Удалить (или переписать без путей/категорий) блок `### Набор шаблонов проектов` + перечни файлов `Gold/…`, `NextLevel/…`, `CheatSheets/…`.
  - Удалить инструкции из `### Как использовать шаблоны`, где фигурируют `ProjectTemplates/...` и пример `TemplateName = ProjectTemplates/...`.
  - Оставить (если нужно) общую функциональность «Новый файл загружает шаблонный код», но без ссылок на конкретный набор шаблонов и без путей.
- [`docs/DEVELOPMENT.md`](d:/Visual%20Studio%20Projects/KID/docs/DEVELOPMENT.md)
  - Удалить упоминание `ProjectTemplates/` из дерева структуры проекта.
- (Опционально, если хотите убрать любые намёки на шаблоны как функцию)
  - [`docs/SUBSYSTEMS.md`](d:/Visual%20Studio%20Projects/KID/docs/SUBSYSTEMS.md): удалить/перефразировать строки про `TemplateCode/TemplateName` и «загрузку шаблонного кода».
  - [`docs/ARCHITECTURE.md`](d:/Visual%20Studio%20Projects/KID/docs/ARCHITECTURE.md): убрать «Управление шаблонным кодом» и `TemplateCode/TemplateName` из описаний модели настроек.

### 3.3. Валидация
- Повторный поиск по `docs/` на `ProjectTemplates` и `/ProjectTemplates` — должно быть 0 совпадений.
- Быстро проверить, что markdown остаётся читаемым (нет «сиротских» заголовков/пунктов списков).

## 4. Порядок выполнения
1. Глобальный поиск по `docs/` и фиксация точных мест.
2. Правка `docs/README.md` (удаление раздела + строки в дереве).
3. Правка `docs/FEATURES.md` (удаление/упрощение разделов про набор шаблонов).
4. Правка `docs/DEVELOPMENT.md` (дерево структуры).
5. (Опционально) Правки `docs/SUBSYSTEMS.md` и `docs/ARCHITECTURE.md`.
6. Финальный поиск/проверка результата.

## 5. Оценка сложности
- **Поиск и инвентаризация упоминаний**: низкая, 5–10 мин, риск: пропустить вариации написания (решение: несколько ключей поиска).
- **`docs/README.md`**: низкая, 5–10 мин, риск: потерять полезный контекст — (решение: при необходимости заменить нейтральной фразой без путей).
- **`docs/FEATURES.md`**: средняя, 10–25 мин, риск: разрыв логики раздела «Шаблоны/Новый файл» — (решение: оставить общий текст без привязки к ProjectTemplates).
- **`docs/DEVELOPMENT.md`**: низкая, 2–5 мин, риск минимальный.
- **Опционально SUBSYSTEMS/ARCHITECTURE**: средняя, 10–20 мин, риск: скрыть реальную часть архитектуры/модели настроек (решение: делать только если это действительно требуется по смыслу).