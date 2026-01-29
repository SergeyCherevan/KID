---
name: Keyboard implementation
overview: "Реализовать полный код модуля `Keyboard` в `KIDLibrary` по спроектированному внешнему интерфейсу: window-level клавиатура, события в фоне, polling-состояния, текстовый ввод Unicode, хоткеи/комбинации, интеграция в запуск и документация/шаблоны."
todos:
  - id: kb-dto-and-keys
    content: Создать DTO/enums для Keyboard + kid-friendly KeyboardKeys.
    status: pending
  - id: kb-event-worker
    content: Реализовать Keyboard.Events.cs (очередь+worker) по образцу Mouse.
    status: pending
    dependencies:
      - kb-dto-and-keys
  - id: kb-state-polling
    content: "Реализовать Keyboard.State.cs: хранение состояния, IsDown/WasPressed, text buffer ReadText/ReadChar, snapshots."
    status: pending
    dependencies:
      - kb-dto-and-keys
  - id: kb-wpf-hooks
    content: "Реализовать Keyboard.System.cs: подписки на Window PreviewKeyDown/Up/TextInput, capture policy, pulses, enqueue событий."
    status: pending
    dependencies:
      - kb-event-worker
      - kb-state-polling
  - id: kb-shortcuts
    content: Реализовать shortcuts (registry+matching+edge detection; опционально sequences).
    status: pending
    dependencies:
      - kb-wpf-hooks
  - id: kb-context-init
    content: Подключить Keyboard.Init(window) в CanvasGraphicsContext.Init().
    status: pending
    dependencies:
      - kb-wpf-hooks
  - id: kb-docs
    content: Добавить docs/Keyboard-API.md и обновить README/FEATURES/SUBSYSTEMS/ARCHITECTURE.
    status: pending
    dependencies:
      - kb-dto-and-keys
  - id: kb-template
    content: Добавить ProjectTemplates/KeyboardTests.cs как пример/проверку.
    status: pending
    dependencies:
      - kb-wpf-hooks
---

# План: реализация полного модуля Keyboard (код + интеграция)

## 1. Анализ требований

### 1.1. Что реализуем

- Полный код модуля `KID.Keyboard` по спроектированному внешнему API из плана `[.cursor/plans/keyboard_api_design_cdb11b21.plan.md](d:/Visual Studio Projects/KID/.cursor/plans/keyboard_api_design_cdb11b21.plan.md)`.
- Источник событий: **WPF window-level** (`Window`) + `PreviewKeyDown/PreviewKeyUp/PreviewTextInput`.
- Публичная модель: 
- **polling** (быстрые методы: `IsDown/WasPressed/ReadText`)
- **events** (обработчики пользователя вызываются **в фоне**, как у `Mouse`)
- **короткие «пульсы»** (`CurrentKeyPress`, `CurrentTextInput`) по аналогии с `Mouse.CurrentClick`.
- Безопасность: потокобезопасность данных, изоляция исключений в обработчиках, отсутствие блокировок UI.

### 1.2. Ключевые сценарии

- Игровые циклы (WASD/стрелки/Space), удержание клавиш.
- Реакция на события (нажал/отпустил/повтор).
- Текстовый ввод (русский/украинский/Unicode) через `PreviewTextInput`.
- Хоткеи/комбинации (Ctrl+S, Shift+Enter, пользовательские chords/последовательности).
- Совместимость с `Console.ReadLine()` в `TextBoxConsole`.

### 1.3. Ограничения/особенности репо

- Архитектурный стиль `KIDLibrary`: `public static partial class`, разнесение на `*.System.cs`, `*.State.cs`, `*.Events.cs` (см. `Mouse`).
- Потоки:
- WPF события приходят в UI.
- Обработчики пользователя исполняем в фоне через очередь (паттерн `Mouse.Events.cs`).
- Консольный ввод:
- `TextBoxConsole` слушает `PreviewKeyDown/PreviewTextInput` и при чтении ставит `e.Handled = true` (см. `[KID.WPF.IDE/Services/CodeExecution/TextBoxConsole.cs](d:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/TextBoxConsole.cs)`).
- Window-level Preview получает событие **раньше** TextBox (туннелинг), поэтому `Keyboard` может “видеть” ввод даже когда консоль читает.

## 2. Архитектурный анализ

### 2.1. Затронутые подсистемы

- `KID.Library` — новый модуль.
- `KID.WPF.IDE/Services/CodeExecution/Contexts` — инициализация модуля в контексте выполнения пользовательского кода.
- `docs/` — документация API.
- `KID.WPF.IDE/ProjectTemplates/` — пример/скрипт для проверки.

### 2.2. Новые компоненты и файлы (конкретно)

Создать папку: `[KID.Library/Keyboard/](d:/Visual Studio Projects/KID/KID.Library/Keyboard/)`

- **Core**
- `Keyboard.System.cs` — подписка на события `Window`, обновление состояния, детект repeat, сбор text input, распознавание shortcut.
- `Keyboard.State.cs` — потокобезопасные геттеры snapshot’ов + методы `IsDown/IsUp/WasPressed/WasReleased/ReadText/ReadChar`.
- `Keyboard.Events.cs` — очередь `ConcurrentQueue<Action>` + `SemaphoreSlim` + worker `Task` (копия подхода из `Mouse.Events.cs`).

- **DTO/Enums**
- `KeyPressInfo.cs` — структура события клавиши (key, modifiers, isRepeat, timestamp, возможно source).
- `TextInputInfo.cs` — структура события текстового ввода (text, timestamp, modifiers).
- `KeyboardState.cs` — snapshot состояния (modifiers, locks, downKeys, lastKeyDown/Up, etc.).
- `KeyModifiers.cs` — `[Flags]` (Shift/Ctrl/Alt/Win) в `KID` namespace.
- `KeyboardCapturePolicy.cs` — enum режимов захвата.

- **Shortcuts**
- `KeyChord.cs` — chord: modifiers + набор клавиш (1..N).
- `Shortcut.cs` — либо chord, либо последовательность chord’ов + таймаут.
- `ShortcutFiredInfo.cs` — что сработало, на каком шаге, timestamp.

- **Kid-friendly constants (важно для удобства)**
- `KeyboardKeys.cs` — `public static class KeyboardKeys` c публичными полями/свойствами типа `System.Windows.Input.Key` (например, `Space`, `A`, `W`, `Left`, …). Это позволит детям писать `Keyboard.IsDown(KeyboardKeys.Space)` без `using System.Windows.Input;`.

### 2.3. Изменяемые файлы

- `[KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs](d:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs)`:
- получить `Window` через `Window.GetWindow(canvas)`
- вызвать `Keyboard.Init(window)` рядом с `Mouse.Init(canvas)`
- поведение при `window == null`: no-op + безопасный reset состояния.

- Документация:
- добавить `[docs/Keyboard-API.md](d:/Visual Studio Projects/KID/docs/Keyboard-API.md)`
- обновить ссылки в `[docs/README.md](d:/Visual Studio Projects/KID/docs/README.md)`
- обновить упоминания в `[docs/FEATURES.md](d:/Visual Studio Projects/KID/docs/FEATURES.md)`
- добавить раздел в `[docs/SUBSYSTEMS.md](d:/Visual Studio Projects/KID/docs/SUBSYSTEMS.md)`
- обновить поток данных в `[docs/ARCHITECTURE.md](d:/Visual Studio Projects/KID/docs/ARCHITECTURE.md)`

- Шаблоны:
- добавить `[KID.WPF.IDE/ProjectTemplates/KeyboardTests.cs](d:/Visual Studio Projects/KID/KID.WPF.IDE/ProjectTemplates/KeyboardTests.cs)`

### 2.4. Потоки, очереди и жизненный цикл

#### 2.4.1. Сбор событий

- В `Keyboard.Init(Window)` подписаться на:
- `window.PreviewKeyDown += OnPreviewKeyDown`
- `window.PreviewKeyUp += OnPreviewKeyUp`
- `window.PreviewTextInput += OnPreviewTextInput`
- На переинициализации: отписаться от старого окна, очистить состояние, перезапустить воркер.

#### 2.4.2. Доставка событий пользователю

- Использовать тот же механизм, что и `Mouse.Events.cs`:
- `EnqueueEvent(Action)` → очередь → `_eventSignal.Release()`
- worker drains queue и выполняет `Action` в фоне
- ошибки обработчиков глотать (fault isolation)

#### 2.4.3. Потокобезопасность

- Один `_stateLock` для всех state-полей.
- Из UI-обработчиков обновлять внутреннее состояние под lock, наружу отдавать snapshot’ы (struct/record).

### 2.5. Нюансы WPF Key/Text

#### 2.5.1. Нормализация клавиши

- В WPF `KeyEventArgs.Key` может быть `Key.System` (Alt-комбинации) — использовать `e.SystemKey`.
- Обрабатывать `Key.ImeProcessed`/`Key.DeadCharProcessed` аккуратно (обычно текст приходит через `PreviewTextInput`).

#### 2.5.2. Repeat

- `e.IsRepeat` — инкрементировать `RepeatCount` в per-key state.
- Для shortcut’ов по умолчанию **не триггерить** на repeat (только на первичное нажатие), иначе Ctrl+S будет спамиться.

#### 2.5.3. TextInput

- `TextCompositionEventArgs.Text` может содержать больше 1 символа → буферизовать строку полностью.
- `CurrentTextInput` делать “пульсом” (с авто-сбросом по версии, как в `Mouse.System.cs`).

### 2.6. Политика захвата и совместимость с Console

#### 2.6.1. Проблема

- `Keyboard` на window Preview увидит событие до того, как `TextBoxConsole` поставит `Handled=true`.

#### 2.6.2. Реализация политики

- Ввести `Keyboard.CapturePolicy` (static property) со значением по умолчанию **`IgnoreWhenTextInputFocused`** (рекомендуется для детей: печатаешь в консоль — игра не реагирует).
- Проверка “текстовый фокус?” в UI обработчиках:
- взять `global::System.Windows.Input.Keyboard.FocusedElement` и проверить:
  - `TextBoxBase`/`TextBox`/`PasswordBox` (или `IInputElement` с `IsKeyboardFocusWithin` + тип)
- Важно: `Keyboard` **не ставит** `e.Handled = true` ни при каком policy; policy лишь решает, обновлять ли наш state/события.

## 3. Список задач (реализация)

### 3.1. Каркас и DTO (низкий риск)

- Создать файлы DTO/enums (`KeyPressInfo`, `TextInputInfo`, `KeyboardState`, `KeyModifiers`, `KeyboardCapturePolicy`).
- Добавить `KeyboardKeys` (набор наиболее частых клавиш + цифры + стрелки + Enter/Backspace/Tab/Escape/Space).

### 3.2. State-механика polling (средний риск)

- Хранение:
- `HashSet<Key> _downKeys`
- `Dictionary<Key, KeyRuntimeState>` (downSince, repeatCount)
- `Dictionary<Key, bool> _pressedEdge`, `_releasedEdge` (или `HashSet<Key>` для edge)
- text buffer: `StringBuilder _textBuffer` + лимит.
- Реализовать:
- `IsDown/IsUp`
- `WasPressed/WasReleased` (семантика: “с прошлого вызова WasPressed/WasReleased” — возвращает true и очищает edge-флаг)
- `ReadText/ReadChar` (consume semantics)
- snapshot `CurrentState`

### 3.3. WPF hooks (высокий риск)

- Реализовать `Keyboard.Init(Window)` с `Subscribe/Unsubscribe` и `ResetState`.
- Реализовать `OnPreviewKeyDown/Up`:
- нормализовать key
- вычислить modifiers (через `global::System.Windows.Input.Keyboard.Modifiers` или через downKeys)
- обновить state + edge flags
- обновить `LastKeyDown/LastKeyUp`
- обновить `CurrentKeyPress` (пульс) + `LastKeyPress`
- enqueue события `KeyDownEvent/KeyUpEvent` (и при необходимости `KeyPressEvent`)
- Реализовать `OnPreviewTextInput`:
- применить capture policy
- append to buffer
- обновить `CurrentTextInput` (пульс) + `LastTextInput`
- enqueue `TextInputEvent`

### 3.4. Event worker (низкий риск)

- Перенести подход из `Mouse.Events.cs`:
- `ConcurrentQueue<Action>` + `SemaphoreSlim` + `CancellationTokenSource`
- `StartEventWorker/StopEventWorker` вызываются из `Init`.

### 3.5. Shortcuts (средний/высокий риск)

- Реализовать registry:
- `int RegisterShortcut(Shortcut shortcut)`
- `bool UnregisterShortcut(int id)`
- `ShortcutEvent`
- Реализовать matching:
- chord match: modifiers совпадают (с опцией “содержит хотя бы эти”, либо “точно эти” — зафиксировать и задокументировать)
- key set: проверять что все keys в chord сейчас down (для multi-key chord)
- anti-repeat: не срабатывать на `IsRepeat`, и/или хранить `HashSet<int> _activeShortcuts` чтобы сработало только при “входе” в состояние.
- Если поддерживаем последовательности chord’ов:
- хранить per-shortcut progress + таймаут между шагами
- обновлять прогресс на каждом первичном KeyDown

### 3.6. Интеграция в запуск (низкий риск)

- В `CanvasGraphicsContext.Init()`:
- `var window = Window.GetWindow(canvas);`
- `Keyboard.Init(window)` (если `window != null`)

### 3.7. Документация (низкий риск)

- `docs/Keyboard-API.md`:
- объяснить polling vs events
- объяснить “пульсы” `CurrentKeyPress/CurrentTextInput`
- capture policy и как избежать конфликтов с консолью
- примеры: WASD, ввод строки, хоткей
- Обновить индексные документы (README/FEATURES/SUBSYSTEMS/ARCHITECTURE).

### 3.8. ProjectTemplates (средний риск)

- `KeyboardTests.cs`:
- polling: двигаем объект WASD
- TextInput: вводим имя и отображаем
- Shortcut: Ctrl+R сброс текста/позиции
- не забыть `StopManager.StopIfButtonPressed()` в циклах

## 4. Порядок выполнения

1. DTO/enums + `KeyboardKeys`.
2. `Keyboard.Events.cs` (очередь/worker) по образцу `Mouse`.
3. `Keyboard.State.cs` (state storage + polling API).
4. `Keyboard.System.cs` (WPF hooks + политика capture + pulses + enqueue).
5. Shortcuts (registry + matching + edge detection).
6. Интеграция в `CanvasGraphicsContext.Init()`.
7. Документация.
8. `ProjectTemplates/KeyboardTests.cs`.

## 5. Оценка сложности и рисков

### 5.1. DTO/Enums/KeyboardKeys

- **Сложность**: низкая
- **Время**: 1–2 часа
- **Риски**: минимальные

### 5.2. Polling-state (IsDown/WasPressed/ReadText)

- **Сложность**: средняя
- **Время**: 2–4 часа
- **Риски**:
- неочевидная семантика `WasPressed` → нужно чётко задокументировать “consume”.

### 5.3. WPF hooks + focus policy

- **Сложность**: высокая
- **Время**: 3–6 часов
- **Риски**:
- события приходят даже во время консольного ввода → решаем policy по фокусу.
- IME/dead keys → проверяем через `PreviewTextInput`.

### 5.4. Event worker

- **Сложность**: низкая
- **Время**: 1–2 часа
- **Риски**: минимальные (паттерн уже есть в `Mouse`).

### 5.5. Shortcuts/Sequences

- **Сложность**: средняя/высокая
- **Время**: 3–8 часов
- **Риски**:
- repeat/edge cases (зажал, отпустил, порядок)
- нужно чётко определить правило сравнения modifiers и chord.

### 5.6. Интеграция+Docs+Templates

- **Сложность**: низкая/средняя
- **Время**: 2–5 часов
- **Риски**: минимальные