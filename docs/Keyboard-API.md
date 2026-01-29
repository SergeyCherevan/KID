# Keyboard API — Документация

## Обзор

Keyboard API предоставляет доступ к состоянию клавиатуры и событиям ввода **на уровне окна приложения** (WPF `Window`):
- нажатия/отпускания клавиш (KeyDown/KeyUp)
- текстовый ввод (Unicode) через `PreviewTextInput`
- хоткеи/комбинации (Shortcuts)

Важно:
- События клавиатуры **собираются в UI-потоке**, но обработчики, на которые подписался пользователь, **вызываются в фоновом потоке** (как в `Mouse`), чтобы пользовательский код не «подвешивал» интерфейс.
- По умолчанию `Keyboard` **не игнорирует** ввод в консоль: клавиатура активна всегда. Если вам нужно, чтобы игра не реагировала во время печати в консоли — используйте `Keyboard.CapturePolicy`.

## Пространство имён

```csharp
using KID;
```

## Быстрый старт (самое простое)

### Проверка “зажата ли клавиша”

```csharp
using System.Threading;
using KID;

while (true)
{
    if (Keyboard.IsDown(KeyboardKeys.Space))
        Console.WriteLine("Space зажата!");

    Thread.Sleep(20);
}
```

### Поймать «нажатие один раз» (edge / consume)

```csharp
using System.Threading;
using KID;

while (true)
{
    if (Keyboard.WasPressed(KeyboardKeys.Enter))
        Console.WriteLine("Enter нажали!");

    Thread.Sleep(10);
}
```

## Polling-состояние

### `Keyboard.CurrentState`
Снимок состояния клавиатуры:
- `Modifiers` (`KeyModifiers`): текущие модификаторы (Shift/Ctrl/Alt/Win)
- `CapsLockOn`, `NumLockOn`, `ScrollLockOn`
- `LastKeyDown`, `LastKeyUp`
- `DownKeys` — массив зажатых клавиш

### `Keyboard.CurrentKeyPress` и `Keyboard.LastKeyPress`
- `CurrentKeyPress` — короткий «пульс» последнего KeyDown (примерно 50–100 мс), чтобы его было удобно «поймать» polling’ом.
- `LastKeyPress` — последнее событие KeyDown, сохранённое модулем.

### `Keyboard.CurrentTextInput` и `Keyboard.LastTextInput`
Аналогично, но для текстового ввода:
- `Text` — строка (может содержать несколько символов)
- `Modifiers`

### Буфер текстового ввода
- `Keyboard.ReadText()` — возвращает накопленный текст и очищает буфер
- `Keyboard.ReadChar()` — возвращает первый символ из буфера (или `null`)

## События

### `Keyboard.KeyDownEvent`
Срабатывает при нажатии клавиши. Передаёт `KeyPressInfo`.

### `Keyboard.KeyUpEvent`
Срабатывает при отпускании клавиши. Передаёт `KeyPressInfo`.

### `Keyboard.TextInputEvent`
Срабатывает при текстовом вводе. Передаёт `TextInputInfo`.

### `Keyboard.ShortcutEvent`
Срабатывает при срабатывании хоткея (см. ниже). Передаёт `ShortcutFiredInfo`.

## Хоткеи (Shortcuts)

### Регистрация

```csharp
using KID;

int id = Keyboard.RegisterShortcut(
    new Shortcut(new KeyChord(KeyModifiers.Ctrl, KeyboardKeys.S), name: "Save")
);

Keyboard.ShortcutEvent += s =>
{
    Console.WriteLine($"HOTKEY: {s.Shortcut.Name}");
};
```

### Последовательности
Shortcut может быть последовательностью chord’ов (например, «Ctrl+K, Ctrl+C»), с таймаутом между шагами:

```csharp
using KID;

var seq = new Shortcut(
    new[]
    {
        new KeyChord(KeyModifiers.Ctrl, KeyboardKeys.K),
        new KeyChord(KeyModifiers.Ctrl, KeyboardKeys.C),
    },
    stepTimeoutMs: 800,
    name: "Comment"
);

Keyboard.RegisterShortcut(seq);
```

## Важная настройка: `Keyboard.CapturePolicy`

### Зачем это нужно
В .KID консольный ввод (например, `Console.ReadLine()`) идёт через TextBox, который получает фокус.
Если ваша программа одновременно реагирует на клавиши (WASD), удобно **не реагировать**, когда пользователь печатает в консоли.

### Режимы
- `KeyboardCapturePolicy.CaptureAlways` — по умолчанию: клавиатура активна всегда
- `KeyboardCapturePolicy.IgnoreWhenTextInputFocused` — если фокус в поле ввода текста, `Keyboard` игнорирует ввод

Пример:

```csharp
using KID;

Keyboard.CapturePolicy = KeyboardCapturePolicy.IgnoreWhenTextInputFocused;
```

## Архитектура (как устроено внутри)
- `Keyboard.System.cs` — подписки на WPF `Window.PreviewKeyDown/Up/PreviewTextInput`, обновление состояния, распознавание хоткеев
- `Keyboard.State.cs` — потокобезопасные polling-геттеры + буферы/edge-флаги
- `Keyboard.Events.cs` — очередь событий и фоновой воркер доставки (как у `Mouse`)

## Архитектура и паттерны (реализация модуля Keyboard)

Ниже — краткое описание того, **какие паттерны проектирования** и **архитектурные решения** реально применены внутри `KID.Library/Keyboard`.

### Статический Facade (Singleton-подобный модуль)
- **Что это даёт**: единая точка входа `Keyboard.*` для пользовательского кода (состояние + события), без создания объектов.
- **Как реализовано**: `Keyboard` — `public static partial class`, инициализация через `Keyboard.Init(Window)`; при повторной инициализации модуль отписывается от старого `Window` и подписывается на новый.

### Observer (события) + асинхронная доставка обработчиков
- **Что это даёт**: реактивная модель через `KeyDownEvent`, `KeyUpEvent`, `TextInputEvent`, `ShortcutEvent`.
- **Ключевая особенность**: события собираются в **UI-потоке** (WPF Preview-события окна), но **пользовательские обработчики вызываются в фоне**, чтобы не блокировать UI.

### Producer–Consumer (очередь событий) + “воркер доставки”
- **Что это даёт**: развязку UI-потока (производит события) и фонового потока (потребляет и вызывает обработчики).
- **Как реализовано**:
  - UI-поток ставит `Action` в потокобезопасную очередь.
  - Фоновая задача ждёт сигнал и последовательно исполняет действия.
  - При переинициализации воркер корректно останавливается и очищает очередь.

### Snapshot / DTO-подход к данным (значимые типы)
- **Что это даёт**: наружу отдаются “снимки” состояния и событий, которые удобно передавать и безопасно читать из разных потоков.
- **Как реализовано**: `KeyboardState`, `KeyPressInfo`, `TextInputInfo`, `ShortcutFiredInfo` — компактные структуры/контракты данных; наружу возвращаются копии текущих значений (например, `DownKeys` — массив-копия).

### Два способа работы с вводом: Polling + Event-driven
- **Polling**: `CurrentState`, `CurrentKeyPress`, `CurrentTextInput`, а также методы `IsDown/IsUp/WasPressed/WasReleased/ReadText/ReadChar` — удобно читать в цикле.
- **События**: `KeyDownEvent`, `KeyUpEvent`, `TextInputEvent`, `ShortcutEvent` — удобно реагировать на действия пользователя.

### “Пульс” (temporal cache) + защита от гонок версией
- **Что это даёт**: `CurrentKeyPress` и `CurrentTextInput` держат значение короткое время (примерно 50–100 мс), чтобы его можно было “поймать” polling’ом, и затем автоматически сбрасываются.
- **Защита от гонок**: сброс выполняется только если за время ожидания не пришло новое событие (используется версионный счётчик).

### Edge/Consume-семантика для polling (one-shot)
- **Что это даёт**: `WasPressed(key)` / `WasReleased(key)` позволяют “поймать” событие один раз в цикле без подписок.
- **Семантика**: методы имеют поведение “consume” — если событие было, метод вернёт `true` и сразу сбросит флаг.

### Policy/Strategy: `CapturePolicy` (управление конфликтом с вводом в консоль)
- **Что это даёт**: можно управлять ситуацией “игра реагирует на WASD, пока пользователь печатает в консоли”.
- **Как реализовано**: `Keyboard.CapturePolicy` задаёт правило, когда `Keyboard` должен учитывать ввод. По умолчанию клавиатура активна всегда; при необходимости можно включить игнорирование ввода, если фокус находится в `TextBox`/элементе ввода.

### “Shortcuts” как конечный автомат (FSM) + latch для chord-only
- **Что это даёт**:
  - **Chord-only** (например, Ctrl+S) срабатывает один раз, пока комбинация удерживается (latch/anti-spam).
  - **Последовательности chord’ов** (например, Ctrl+K затем Ctrl+C) реализованы через прогресс по шагам и таймаут между шагами (FSM).
- **Особенности**:
  - повторы (`IsRepeat`) не используются для срабатывания хоткеев;
  - для последовательностей используется таймаут `stepTimeoutMs`.

## Алгоритм доставки событий (как работает `Keyboard.Events.cs`)

`Keyboard.Events.cs` доставляет события Keyboard API в обработчики пользователя **в фоновом потоке**. Это сделано специально: WPF события окна приходят в UI-потоке, а “тяжёлый” пользовательский код не должен замораживать интерфейс.

Внутри реализована схема **Producer–Consumer**:
- **Producer**: UI-поток кладёт `Action` в очередь и сигналит семафор.
- **Consumer**: один фоновый воркер ждёт семафор и вычитывает очередь “пачкой”, исполняя `Action` последовательно.

### Важные свойства/ограничения
- **Один consumer**: обработчики выполняются последовательно в одном фоне (упрощает модель и снижает гонки в пользовательском коде).
- **Нет backpressure**: если обработчики медленные, очередь может расти; зато UI не блокируется.
- **Исключения изолируются**: ошибки в обработчиках перехватываются и не ломают доставку событий.
