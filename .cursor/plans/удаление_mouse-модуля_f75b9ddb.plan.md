---
name: Удаление Mouse-модуля
overview: Полное удаление Mouse API из KID (код + шаблоны + документация) с сохранением собираемости приложения. Пользовательские скрипты, использующие `Mouse.*`, больше не будут компилироваться — это ожидаемое изменение.
todos:
  - id: remove-mouse-init
    content: Удалить `Mouse.Init(canvas)` из `KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs`.
    status: pending
  - id: delete-mouse-library
    content: Удалить папку `KID.Library/Mouse/` целиком (все файлы Mouse API).
    status: pending
  - id: delete-mouse-template
    content: Удалить `KID.WPF.IDE/ProjectTemplates/MouseTest.cs`.
    status: pending
  - id: update-docs
    content: Убрать Mouse из `docs/README.md`, `docs/FEATURES.md`, `docs/ARCHITECTURE.md`, `docs/SUBSYSTEMS.md`, `docs/DEVELOPMENT.md` и удалить `docs/Mouse-API.md`.
    status: pending
  - id: build-smoke
    content: Проверить сборку и кратко проверить запуск приложения.
    status: pending
---

# План: удалить модуль Mouse из .KID

## 1. Анализ требований

1.1. **Цель**
- Полностью удалить Mouse API/модуль из проекта (слой `KIDLibrary`), чтобы позже реализовать заново «с нуля».

1.2. **Целевая аудитория и сценарии**
- **Разработчик .KID**: хочет временно убрать функциональность Mouse.
- **Пользователь .KID (пишет код)**: скрипты с `Mouse.*` должны начать **ошибаться при компиляции** (ожидаемо), чтобы не было «тихих» заглушек.

1.3. **Входы/выходы**
- **Вход**: текущее состояние репозитория.
- **Выход**:
  - Удалены файлы `KID.Library/Mouse/*`.
  - Убрана инициализация Mouse в графическом контексте.
  - Удалены связанные шаблоны и документация.
  - Проект **успешно собирается**.

1.4. **Ограничения/требования**
- WPF/.NET 8.0, MVVM и DI не должны пострадать.
- Не добавлять новые зависимости.
- Все изменения должны быть прозрачными и воспроизводимыми.

## 2. Архитектурный анализ

2.1. **Затронутые подсистемы**
- **KIDLibrary layer**: удаляется `Mouse API` (папка `KID.Library/Mouse/`).
- **Code Execution / Contexts**: `CanvasGraphicsContext.Init()` сейчас вызывает `Mouse.Init(canvas)`.
- **ProjectTemplates**: есть шаблон `KID.WPF.IDE/ProjectTemplates/MouseTest.cs`.
- **Документация**: `docs/*` описывает Mouse и его расширение.

2.2. **Что удаляем**
- Полностью удалить реализацию `public static partial class Mouse` и связанные типы (`ClickStatus`, `MouseClickInfo`, `PressButtonStatus`, `CursorInfo`, и т.п.).

2.3. **Что меняем**
- Удалить вызов `Mouse.Init(canvas)` в:
  - `KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs`

2.4. **Зависимости между компонентами**
- `CanvasGraphicsContext` инициализирует API слоя KIDLibrary:
  - `Graphics.Init(canvas)`
  - `Mouse.Init(canvas)` (будет удалено)
  - `Music.Init()`

Новая цепочка должна стать: `Graphics.Init(canvas)` → `Music.Init()`.

## 3. Список задач (с файлами)

3.1. **Удаление кода Mouse API**
- Удалить папку целиком: `KID.Library/Mouse/` (все `*.cs`).
  - Ожидаемые файлы (по текущей документации/структуре):
    - `KID.Library/Mouse/Mouse.System.cs`
    - `KID.Library/Mouse/Mouse.Position.cs`
    - `KID.Library/Mouse/Mouse.Click.cs`
    - `KID.Library/Mouse/Mouse.Events.cs`
    - `KID.Library/Mouse/Mouse.State.cs`
    - `KID.Library/Mouse/ClickStatus.cs`
    - `KID.Library/Mouse/MouseClickInfo.cs`
    - `KID.Library/Mouse/PressButtonStatus.cs`
    - `KID.Library/Mouse/CursorInfo.cs`

3.2. **Удаление интеграции Mouse из инициализации**
- Обновить `KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs`:
  - удалить строку `Mouse.Init(canvas);`

3.3. **Удаление шаблонов**
- Удалить `KID.WPF.IDE/ProjectTemplates/MouseTest.cs`

3.4. **Обновление документации**
- Удалить файл: `docs/Mouse-API.md`
- Обновить ссылки/разделы:
  - `docs/README.md` (убрать пункт Mouse API)
  - `docs/FEATURES.md` (убрать раздел про Mouse API и связанные пункты)
  - `docs/ARCHITECTURE.md` (убрать Mouse из описания KID.Library/потоков данных/расширяемости)
  - `docs/SUBSYSTEMS.md` (убрать подсистему Mouse API и упоминания в потоках)
  - `docs/DEVELOPMENT.md` (убрать раздел «Добавление нового метода Mouse API» и ссылки на `docs/Mouse-API.md`)

3.5. **Проверки/тестирование**
- Сборка решения: `dotnet build` (или сборка через Visual Studio) — должна пройти.
- Быстрая проверка запуска приложения (smoke): открыть окно, запустить любой шаблон без Mouse.

## 4. Порядок выполнения

4.1. Удалить инициализацию Mouse в `CanvasGraphicsContext` (чтобы сборка не зависела от удалённых типов).
4.2. Удалить `KID.Library/Mouse/*`.
4.3. Удалить `KID.WPF.IDE/ProjectTemplates/MouseTest.cs`.
4.4. Удалить/обновить документацию (`docs/*`).
4.5. Прогнать сборку и короткий smoke-test запуска.

## 5. Оценка сложности, времени и рисков

5.1. **Убрать `Mouse.Init` из `CanvasGraphicsContext`**
- **Сложность**: низкая
- **Время**: ~5–10 минут
- **Риски**: минимальные; риск только если где-то ещё есть скрытые обращения к Mouse.

5.2. **Удалить `KID.Library/Mouse/*`**
- **Сложность**: средняя
- **Время**: ~10–25 минут
- **Риски**:
  - возможные дополнительные ссылки на типы Mouse (например, в других файлах/шаблонах). Снижается глобальным поиском и сборкой.

5.3. **Удалить `KID.WPF.IDE/ProjectTemplates/MouseTest.cs`**
- **Сложность**: низкая
- **Время**: ~5 минут
- **Риски**: низкие; риск только если шаблон где-то явно перечисляется (но сейчас явных ссылок не найдено).

5.4. **Обновить/удалить документацию**
- **Сложность**: средняя
- **Время**: ~20–40 минут
- **Риски**: средние; риск «битых» внутренних ссылок/нумерации разделов — решается внимательной правкой и быстрым просмотром.

5.5. **Сборка и smoke-test**
- **Сложность**: низкая
- **Время**: ~10–20 минут
- **Риски**: выявит любые оставшиеся зависимости; если сборка падает, потребуется точечно убрать оставшиеся ссылки.