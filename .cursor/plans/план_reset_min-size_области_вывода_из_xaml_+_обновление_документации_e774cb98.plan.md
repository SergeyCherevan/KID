---
name: "План: reset min-size области вывода из XAML + обновление документации"
overview: Убрать магические числа (300/310) из `MenuViewModel.ExecuteRun()` и перенести логику сброса min-ограничений области вывода в `GraphicsOutputViewModel`, инициализируя дефолты из XAML через `FindName`. Дополнительно обновить документацию по Graphics API (reset/min-size поведение).
todos:
  - id: update-graphicsoutputviewmodel-interface
    content: Расширить `IGraphicsOutputViewModel` свойствами DefaultOutputViewMinWidth/Height и методом ResetOutputViewMinSizeToDefault().
    status: pending
  - id: implement-default-min-capture
    content: В `GraphicsOutputViewModel.Initialize` найти `WorkspaceGrid`/`OutputGrid` через Window.FindName, сохранить ссылки на `ColumnDefinition`/`RowDefinition` и значения дефолтных MinWidth/MinHeight из XAML.
    status: pending
  - id: implement-reset-method
    content: Реализовать `ResetOutputViewMinSizeToDefault()` через прямые присваивания `MinWidth/MinHeight` сохранённым definitions.
    status: pending
  - id: replace-run-reset-call
    content: В `MenuViewModel.ExecuteRun` заменить Graphics.SetCanvasMinSize(300,300) на graphicsOutputViewModel.ResetOutputViewMinSizeToDefault().
    status: pending
  - id: update-docs
    content: "Обновить `docs/Graphics-API.md`: описать сброс min-ограничений к XAML-дефолтам перед Run и где это реализовано."
    status: pending
  - id: verify-build
    content: Проверить сборку `dotnet build` и отсутствие новых предупреждений/ошибок.
    status: pending
---

# План: reset min-size области вывода из XAML + обновление документации

## 1. Анализ требований
- **Проблема**: в `MenuViewModel.ExecuteRun()` используются магические числа (`300, 300`) при сбросе min-size.
- **Цель**: убрать магию из `MenuViewModel`, восстановление min-size делать по значениям из XAML:
  - `WorkspaceGrid.ColumnDefinitions[2].MinWidth` (дефолт в XAML)
  - `OutputGrid.RowDefinitions[2].MinHeight` (дефолт в XAML)
- **Требование**: reset выполняется простым присваиванием свойств разметки, **без вызовов `Graphics`**.
- **Документация**: описать поведение reset/min-size в `docs/Graphics-API.md`.

## 2. Архитектурное решение
- Управление XAML-минимумами — это UI-слой → реализуем в `GraphicsOutputViewModel`.
- `GraphicsOutputViewModel.Initialize(Canvas)` получает доступ к нужным объектам разметки через:
  - `Window.GetWindow(canvas)`
  - `window.FindName("WorkspaceGrid")` и `window.FindName("OutputGrid")`
  - индексы `[2]` (правая колонка / строка графики)
- В VM сохраняем:
  - дефолтные значения min-ов (считанные из XAML один раз)
  - ссылки на `ColumnDefinition`/`RowDefinition` для быстрого reset (O(1)).

## 3. Конкретные изменения

### 3.1. Интерфейс `IGraphicsOutputViewModel`
Файл: `[d:\Visual Studio Projects\KID\KID.WPF.IDE\ViewModels\Interfaces\IGraphicsOutputViewModel.cs](d:\Visual Studio Projects\KID\KID.WPF.IDE\ViewModels\Interfaces\IGraphicsOutputViewModel.cs)`
- Добавить:
  - `double DefaultOutputViewMinWidth { get; }`
  - `double DefaultOutputViewMinHeight { get; }`
  - `void ResetOutputViewMinSizeToDefault();`

### 3.2. `GraphicsOutputViewModel`
Файл: `[d:\Visual Studio Projects\KID\KID.WPF.IDE\ViewModels\GraphicsOutputViewModel.cs](d:\Visual Studio Projects\KID\KID.WPF.IDE\ViewModels\GraphicsOutputViewModel.cs)`
- Добавить свойства:
  - `DefaultOutputViewMinWidth`, `DefaultOutputViewMinHeight` (инициализируются в `Initialize`).
- Добавить приватные поля для ссылок:
  - `ColumnDefinition? _outputColumn;`
  - `RowDefinition? _graphicsRow;`
- В `Initialize(Canvas canvas)`:
  - сохранить `GraphicsCanvasControl = canvas`
  - найти окно и `WorkspaceGrid`/`OutputGrid`
  - сохранить ссылки `_outputColumn = workspaceGrid.ColumnDefinitions[2]`, `_graphicsRow = outputGrid.RowDefinitions[2]`
  - запомнить дефолты:
    - `DefaultOutputViewMinWidth = _outputColumn.MinWidth`
    - `DefaultOutputViewMinHeight = _graphicsRow.MinHeight`
- Реализовать `ResetOutputViewMinSizeToDefault()`:
  - если ссылки не найдены → no-op
  - иначе:
    - `_outputColumn.MinWidth = DefaultOutputViewMinWidth;`
    - `_graphicsRow.MinHeight = DefaultOutputViewMinHeight;`

### 3.3. `MenuViewModel.ExecuteRun`
Файл: `[d:\Visual Studio Projects\KID\KID.WPF.IDE\ViewModels\MenuViewModel.cs](d:\Visual Studio Projects\KID\KID.WPF.IDE\ViewModels\MenuViewModel.cs)`
- Удалить сброс через `Graphics.*` (и магические числа).
- Вместо этого вызывать:
  - `graphicsOutputViewModel.ResetOutputViewMinSizeToDefault();`

### 3.4. Обновление документации
Файл: `[d:\Visual Studio Projects\KID\docs\Graphics-API.md](d:\Visual Studio Projects\KID\docs\Graphics-API.md)`
- Дополнить раздел «Размеры Canvas»:
  - указать, что **приложение перед Run** сбрасывает min-ограничения UI-области вывода к дефолтам из XAML.
  - отметить, что это делается на уровне UI (VM), а не в `Graphics`.

## 4. Порядок выполнения
1. Обновить `IGraphicsOutputViewModel`.
2. Реализовать захват дефолтов min-size в `GraphicsOutputViewModel.Initialize`.
3. Реализовать `ResetOutputViewMinSizeToDefault()`.
4. Заменить сброс в `MenuViewModel.ExecuteRun()`.
5. Обновить `docs/Graphics-API.md`.
6. Проверить сборку `dotnet build`.

## 5. Оценка сложности
- **Низкая (30–60 мин)**
  - Риски: `FindName` вернёт `null`, если поменяются имена в XAML или если `Initialize` вызван до полной загрузки окна.
  - Митигация: fail-soft (no-op) при отсутствии окна/элементов + хранение ссылок после успешного поиска.
  - Документация: 10–20 мин.