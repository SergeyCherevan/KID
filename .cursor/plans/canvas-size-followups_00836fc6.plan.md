---
name: canvas-size-followups
overview: "Доработать новый раздел API размеров Canvas: вынести в отдельный файл `Graphics.CanvasSize.cs`, добавить `MinSize` и одиночные `Width/Height` методы, разрешить `SetCanvasSize` задавать размеры < 300, и вернуть дефолтные минимумы 300×300 перед каждым запуском в `MenuViewModel`."
todos:
  - id: split-canvas-size-file
    content: Вынести методы размеров Canvas в `KID/KIDLibrary/Graphics/Graphics.CanvasSize.cs` и оставить `Graphics.System.cs` системным.
    status: pending
  - id: add-single-size-methods
    content: Добавить `GetCanvasWidth/Height`, `SetCanvasWidth/Height`, `SetCanvasMinWidth/MinHeight`; парные методы должны вызывать одиночные.
    status: pending
  - id: allow-below-300
    content: Сделать `SetCanvasSize` способным задавать размеры < 300, снижая min-ограничения разметки при необходимости.
    status: pending
  - id: reset-min-before-run
    content: В `MenuViewModel.ExecuteRun` (стр. 193–194) сбрасывать min Canvas к 300×300 (с поправкой на отступы внутри реализации).
    status: pending
  - id: update-graphics-api-docs
    content: Обновить `docs/Graphics-API.md` с новым разделом Width/Height/MinSize и описанием reset-поведения.
    status: pending
  - id: verify-build-and-manual
    content: "Собрать проект и вручную проверить: SetCanvasSize(100,100), затем повторный Run → min возвращается к 300×300."
    status: pending
---

# План: CanvasSize API (вынос в файл, min-size, single Width/Height)

## 1. Анализ требований
1.1. **Цель**
- Вынести методы работы с размером Canvas в отдельный файл `Graphics.CanvasSize.cs`.
- Расширить API:
  - `GetCanvasSize()` + одиночные `GetCanvasWidth()`/`GetCanvasHeight()`.
  - `SetCanvasSize(width,height)` + одиночные `SetCanvasWidth(width)`/`SetCanvasHeight(height)`.
  - Добавить `SetCanvasMinSize(minWidth,minHeight)` + одиночные `SetCanvasMinWidth(minWidth)`/`SetCanvasMinHeight(minHeight)`.
- Сделать так, чтобы `Graphics.SetCanvasSize()` мог задавать размеры **меньше 300×300**.
- Перед **каждым новым запуском** (кнопка Run) вернуть минимальные размеры Canvas обратно к **300×300** (с учётом отступов/«дельты» между колонкой/строкой и реальным Canvas), реализовав это в `[d:\Visual Studio Projects\KID\KID\ViewModels\MenuViewModel.cs](d:\Visual Studio Projects\KID\KID\ViewModels\MenuViewModel.cs)` около строк 193–194.

1.2. **Ограничения**
- WPF разметка уже содержит жёсткие минимумы:
  - `ColumnDefinition MinWidth="310"` (правая панель)
  - `RowDefinition MinHeight="300"` (строка графики)
  Эти минимумы нужно сделать **динамически управляемыми** через новый API, иначе `SetCanvasSize(<300)` физически не сможет уменьшить область.
- Все операции должны выполняться в UI-потоке через `DispatcherManager.InvokeOnUI(...)`.
- Парные `*Size` методы должны быть тонкими обёртками над одиночными `*Width`/`*Height` (основная логика — в одиночных методах).

## 2. Архитектурный анализ
2.1. **Что реально меняет размеры Canvas**
- Canvas растягивается разметкой.
- Реальный «рычаг» размера — `WorkspaceGrid.ColumnDefinitions[2]` (ширина) и `OutputGrid.RowDefinitions[2]` (высота).
- `GridSplitter` меняет эти же значения, поэтому для программного управления нужно работать с `ColumnDefinition.Width/MinWidth` и `RowDefinition.Height/MinHeight`.

2.2. **Как обеспечить размеры < 300**
- Сейчас `OutputGrid.RowDefinitions[2].MinHeight = 300`, а правая колонка `MinWidth=310` → это блокирует размеры меньше.
- Решение:
  - `SetCanvasWidth/Height` должны уметь **временно снижать** соответствующий `MinWidth/MinHeight`, если запрошенный размер меньше текущего минимума.
  - Перед следующим запуском (Run) минимумы должны сбрасываться обратно к 300×300.

2.3. **Поправка на отступы ("все отступы")**
- Для ширины есть известный системный «разрыв» между шириной колонки и реальным `Canvas.ActualWidth` из-за `OutputGrid.Margin="5"` (минимум +10 по ширине).
- Для большей устойчивости можно вычислять дельту в UI-потоке:
  - `deltaW = OutputGrid.ActualWidth - Canvas.ActualWidth` (если layout уже измерен), иначе fallback на `OutputGrid.Margin.Left+Right`.
  - Аналогично для высоты можно считать `deltaH = GraphicsOutputView.ActualHeight - Canvas.ActualHeight` (если потребуется); в текущей разметке обычно 0.
- Все `*MinWidth/Height` и `*Width/Height` переводят «размер Canvas» → «размер колонки/строки» через эти дельты.

2.4. **Где делать reset перед Run**
- В `MenuViewModel.ExecuteRun()` прямо перед очисткой (стр. 193–194) есть гарантированный доступ к `graphicsOutputViewModel.GraphicsCanvasControl`.
- Для сброса дефолтного минимума можно:
  - обеспечить, что `Graphics` имеет ссылку на Canvas (вызвать `Graphics.Init(graphicsOutputViewModel.GraphicsCanvasControl)` — безопасно, т.к. `Init` просто присваивает ссылку и потом всё равно будет вызван в `CanvasGraphicsContext.Init()`),
  - вызвать `Graphics.SetCanvasMinSize(300, 300)`.

## 3. Список задач
3.1. **Рефакторинг файлов (вынос в отдельный файл)**
- Создать новый файл:
  - `[d:\Visual Studio Projects\KID\KID\KIDLibrary\Graphics\Graphics.CanvasSize.cs](d:\Visual Studio Projects\KID\KID\KIDLibrary\Graphics\Graphics.CanvasSize.cs)`
- Переместить из `Graphics.System.cs` все методы, связанные с размерами Canvas, в новый partial-файл:
  - `GetCanvasSize`, `SetCanvasSize` (и новые одиночные/Min-методы).
- В `Graphics.System.cs` оставить только системную часть (`Canvas`, `Init`, `Clear` и т.п.).

3.2. **Добавление одиночных Width/Height аналогов (основная логика)**
- Добавить методы (в `Graphics.CanvasSize.cs`):
  - `double GetCanvasWidth()`
  - `double GetCanvasHeight()`
  - `(double width, double height) GetCanvasSize()` — вызывает одиночные.
  - `void SetCanvasWidth(double width)`
  - `void SetCanvasHeight(double height)`
  - `void SetCanvasSize(double width, double height)` — вызывает одиночные.
  - `void SetCanvasMinWidth(double minWidth)`
  - `void SetCanvasMinHeight(double minHeight)`
  - `void SetCanvasMinSize(double minWidth, double minHeight)` — вызывает одиночные.

3.3. **Сделать SetCanvasSize способным задавать < 300×300**
- В `SetCanvasWidth/SetCanvasHeight`:
  - если текущий `MinWidth/MinHeight` больше запрошенного значения, **снизить минимум** до запрошенного (или до 0, если нужно разрешить «совсем маленькое»).
  - затем выставить `ColumnDefinition.Width`/`RowDefinition.Height` в пикселях (DIP).
- При отсутствии нужных элементов разметки (`WorkspaceGrid`/`OutputGrid` не найдены) — сохранить текущий fallback на `Canvas.Width/Canvas.Height`.

3.4. **Reset min-size перед новым запуском (MenuViewModel)**
- В `[d:\Visual Studio Projects\KID\KID\ViewModels\MenuViewModel.cs](d:\Visual Studio Projects\KID\KID\ViewModels\MenuViewModel.cs)` в `ExecuteRun()` около строк 193–194:
  - добавить сброс минимальных размеров Canvas обратно к 300×300:
    - `Graphics.Init(graphicsOutputViewModel.GraphicsCanvasControl);`
    - `Graphics.SetCanvasMinSize(300, 300);`
  - (опционально) можно делать это **до** `graphicsOutputViewModel.Clear()`, чтобы минимумы применялись даже если предыдущий запуск «сжал» область.

3.5. **Обновление документации**
- Обновить `[d:\Visual Studio Projects\KID\docs\Graphics-API.md](d:\Visual Studio Projects\KID\docs\Graphics-API.md)`:
  - описать новые методы `GetCanvasWidth/Height`, `SetCanvasWidth/Height`, `SetCanvasMinSize`, `SetCanvasMinWidth/MinHeight`.
  - явно указать поведение:
    - `SetCanvasSize` меняет UI-layout (GridSplitter-equivalent),
    - допускает размеры < 300, но **перед новым Run** минимумы сбрасываются к 300×300.

3.6. **Тестирование**
- Сборка: `dotnet build`.
- Ручной тест в UI:
  - Запустить код, вызвать `Graphics.SetCanvasSize(100, 100)` → область графики должна реально стать меньше 300.
  - Остановить и снова нажать Run → минимумы должны вернуться к 300×300 (область не должна оставаться «сжатой» минимально).
  - Проверить `Graphics.SetCanvasMinSize(500, 500)` → последующие `SetCanvasSize(100,100)` должны либо снижать минимум (если так задумано), либо требовать явного `SetCanvasMinSize` (по принятой реализации); в плане закладываем вариант, что `SetCanvasSize` снижает минимум при необходимости.

## 4. Порядок выполнения
1. Создать `Graphics.CanvasSize.cs` и перенести туда текущие `GetCanvasSize/SetCanvasSize`.
2. Добавить одиночные `GetCanvasWidth/Height` и перевести `GetCanvasSize` на них.
3. Добавить `SetCanvasWidth/Height` и перевести `SetCanvasSize` на них.
4. Добавить `SetCanvasMinWidth/MinHeight` и `SetCanvasMinSize`.
5. Изменить логику `SetCanvasWidth/Height` так, чтобы размеры < 300 реально применялись (снижая min-ограничения разметки).
6. Добавить reset к 300×300 в `MenuViewModel.ExecuteRun()` на строках 193–194.
7. Обновить `docs/Graphics-API.md`.
8. Прогнать сборку и ручную проверку.

## 5. Оценка сложности
- **Вынос в `Graphics.CanvasSize.cs`**: низкая, 10–20 мин
  - Риск: конфликт partial/using, легко чинится.
- **Одиночные методы + делегирование парных**: низкая–средняя, 20–40 мин
  - Риск: пропустить один из методов; покрывается компиляцией.
- **MinSize и поддержка <300**: средняя, 40–90 мин
  - Риски:
    - взаимодействие с XAML `MinWidth/MinHeight` и корректный пересчёт «дельты» (margin/контейнеры).
- **Reset перед Run в MenuViewModel**: низкая, 10–20 мин
  - Риск: вызвать до/после Clear — выбираем стабильный порядок.
- **Документация**: низкая, 10–20 мин
- **Ручные тесты**: средняя, 20–40 мин