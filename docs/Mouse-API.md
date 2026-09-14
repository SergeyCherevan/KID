# Mouse API — Документация

## Обзор

Mouse API предоставляет доступ к состоянию мыши **относительно Canvas** (графического холста) и событиям мыши: перемещение, изменение нажатых кнопок, клики (одинарные/двойные).

Важно:
- События мыши **собираются в UI-потоке**, но обработчики, на которые подписался пользователь, **вызываются в фоновом потоке** (чтобы пользовательский код не «подвешивал» интерфейс).
- Если из обработчика нужно безопасно работать с UI — используйте API, которое уже само синхронизируется (например, `Graphics.*`), либо `DispatcherManager.InvokeOnUI(...)`.

## Пространство имён

```csharp
using KID;
```

## Состояние курсора

### `Mouse.CurrentCursor`
Текущее состояние курсора.

- `Position` (`Point?`): координата курсора относительно левого верхнего угла Canvas. Если курсор вне Canvas — `null`.
- `PressedButton` (`PressButtonStatus`): флаги нажатых кнопок + флаг `OutOfArea`, если курсор вне Canvas.

### `Mouse.LastActualCursor`
Последнее актуальное состояние курсора **в момент, когда он был на Canvas**.

- `Position` (`Point?`): последняя позиция на Canvas. Если курсор сейчас на Canvas — равна `Mouse.CurrentCursor.Position`.
- `PressedButton` (`PressButtonStatus`): состояние нажатых кнопок в момент, когда курсор был на Canvas (без `OutOfArea`).

## Клики

### `Mouse.CurrentClick`
Текущий клик как короткий «пульс». После короткого интервала автоматически сбрасывается в `NoClick`.

- `Status` (`ClickStatus`): `NoClick`, `OneLeftClick`, `OneRightClick`, `DoubleLeftClick`, `DoubleRightClick`
- `Position` (`Point?`): координата клика относительно Canvas

**Семантика:** `CurrentClick` держится примерно **50–100 мс**, чтобы его было удобно «поймать» при polling (например, в цикле).

### `Mouse.LastClick`
Последний зарегистрированный клик по Canvas.

- `Status` (`ClickStatus`): то же перечисление, что и у `CurrentClick` (включая `NoClick` как начальное значение)
- `Position` (`Point?`): координата клика

## Перечисления и структуры

### `PressButtonStatus` (флаги)
- `NoButton = 0b000`
- `LeftButton = 0b001`
- `RightButton = 0b010`
- `OutOfArea = 0b100`

Флаги могут комбинироваться (например, `LeftButton | OutOfArea`).

### `ClickStatus`
- `NoClick`
- `OneLeftClick`
- `OneRightClick`
- `DoubleLeftClick`
- `DoubleRightClick`

### `CursorInfo`
- `Point? Position`
- `PressButtonStatus PressedButton`

### `MouseClickInfo`
- `ClickStatus Status`
- `Point? Position`

## События

### `Mouse.MouseMoveEvent`
Событие перемещения мыши по Canvas. Передаёт `CursorInfo`.

### `Mouse.MousePressButtonEvent`
Событие изменения `CurrentCursor.PressedButton`. Передаёт `CursorInfo`.

### `Mouse.MouseClickEvent`
Событие клика по Canvas. Передаёт `MouseClickInfo`.

## Примеры

### Простой polling координаты

```csharp
using System;
using System.Threading;
using KID;

while (true)
{
    var pos = Mouse.CurrentCursor.Position;
    Console.Clear();
    Console.WriteLine(pos.HasValue ? $"X={pos.Value.X:0}, Y={pos.Value.Y:0}" : "Вне Canvas");
    Thread.Sleep(20);
}
```

### Реакция на клик

```csharp
using System;
using KID;

Mouse.MouseClickEvent += click =>
{
    Console.WriteLine($"{click.Status} at {click.Position}");
};
```

### «Поймать» CurrentClick в цикле

```csharp
using System;
using System.Threading;
using KID;

while (true)
{
    var c = Mouse.CurrentClick;
    if (c.Status != ClickStatus.NoClick)
        Console.WriteLine($"CLICK: {c.Status} at {c.Position}");

    Thread.Sleep(10);
}
```

## Архитектура и паттерны (реализация модуля Mouse)

Ниже — краткое описание того, **какие паттерны проектирования** и **архитектурные решения** реально применены внутри `KID.Library/Mouse`.

### Статический Facade (Singleton-подобный модуль)
- **Что это даёт**: единая точка входа `Mouse.*` для пользовательского кода (состояние + события), без создания объектов.
- **Как реализовано**: `Mouse` — `public static partial class`, но его внутренний scope принадлежит одной execution-сессии. Повторный `Init` до завершения обязательного `ShutdownAsync` отклоняется как ошибка lifecycle.

### Observer (события) + асинхронная доставка обработчиков
- **Что это даёт**: реактивная модель через `MouseMoveEvent`, `MousePressButtonEvent`, `MouseClickEvent`.
- **Ключевая особенность**: события собираются в **UI-потоке** (WPF события `Canvas`), но **пользовательские обработчики вызываются в фоне**, чтобы не блокировать UI.

### Producer–Consumer (очередь событий) + “воркер доставки”
- **Что это даёт**: развязку UI-потока (производит события) и фонового потока (потребляет и вызывает обработчики).
- **Как реализовано**:
  - каждый запуск получает собственный `MouseExecutionScope` и собственный экземпляр общего `ExecutionEventWorker`;
  - UI-поток ставит отдельный `Action` для каждого подписчика в очередь только текущего scope;
  - linked token соединяет lifetime worker с токеном execution;
  - shutdown закрывает вход, снимает WPF-подписки, отменяет worker и ожидает уже выполняющийся handler; оставшаяся очередь отбрасывается.

### Snapshot / DTO-подход к данным (значимые типы)
- **Что это даёт**: наружу отдаются “снимки” состояния и событий, которые удобно передавать и безопасно читать из разных потоков.
- **Как реализовано**: `CursorInfo` и `MouseClickInfo` — `struct`, а не ссылочные объекты; наружу возвращаются копии текущих значений.

### Два способа работы с вводом: Polling + Event-driven
- **Polling**: `CurrentCursor`, `LastActualCursor`, `CurrentClick`, `LastClick` — удобно читать в цикле.
- **События**: `Mouse*Event` — удобно реагировать на действия пользователя.
- Это сделано намеренно: для обучающих/игровых сценариев часто нужен “опрос” в цикле, но также полезна реактивность.

### “Пульс клика” (temporal cache) + защита от гонок версией
- **Что это даёт**: `CurrentClick` держит значение короткое время (около 50–100 мс), чтобы его можно было “поймать” polling’ом, и затем автоматически сбрасывается.
- **Защита от гонок**: pulse-задача принадлежит тому же scope и linked token; перед сбросом проверяются ownership и версия. Shutdown отменяет и ожидает pulse, поэтому старый запуск не может изменить состояние нового.

### Потокобезопасность и устойчивость к пользовательскому коду
- **Thread-safety**: общее состояние (`CurrentCursor`, `LastActualCursor`, `CurrentClick`, `LastClick`) защищено через `lock`.
- **Fault isolation**: исключения в пользовательских обработчиках перехватываются, чтобы не “уронить” поток доставки событий и не нарушить работу приложения.

### Разделение ответственности внутри модуля (внутренняя архитектура)
- **`Mouse.System.cs`**: per-run scope, интеграция с WPF (`Canvas` events Enter/Leave/Move/Down/Up), вычисление кликов, “пульс” `CurrentClick` и `ShutdownAsync`.
- **`Mouse.State.cs`**: публичное состояние для polling (потокобезопасные геттеры).
- **`Mouse.Events.cs`**: публичные события и постановка отдельных подписчиков в очередь.
- **`MouseExecutionScope.cs`**: владелец `Canvas`, исходного `ExecutionEnvironment` и экземпляра `ExecutionEventWorker`.
- **DTO/Enums**: `CursorInfo`, `MouseClickInfo`, `PressButtonStatus`, `ClickStatus` — компактные контракты данных.

## Алгоритм доставки событий (как работает `Mouse.Events.cs`)

Файл `Mouse.Events.cs` отвечает за доставку событий Mouse API в обработчики пользователя **в фоновом потоке**. Это сделано специально: WPF события Canvas приходят в UI-потоке, а “тяжёлый” пользовательский код не должен замораживать интерфейс.

Внутри реализована схема **Producer–Consumer** (производитель–потребитель):
- **Producer**: UI-поток кладёт работу в очередь текущего `MouseExecutionScope`.
- **Consumer**: один `ExecutionEventWorker` этой execution забирает работу и выполняет её последовательно.

### Главные элементы

#### Очередь работ: `ConcurrentQueue<Action>`
- В очередь кладутся делегаты `Action`, внутри которых вызываются пользовательские обработчики (`MouseMoveEvent`, `MousePressButtonEvent`, `MouseClickEvent`).
- Очередь потокобезопасная: несколько потоков могут одновременно добавлять задачи.

#### Сигнализация: `SemaphoreSlim`
- Семафор выступает как “счётчик сигналов”: на каждую поставленную задачу вызывается `Release()`.
- Воркер ждёт `WaitAsync(token)` и просыпается только тогда, когда появилось событие (или когда его отменили).

#### Управление жизненным циклом
- Scope создаёт linked `CancellationTokenSource`, связанную с исходным `ExecutionEnvironment`.
- Один и тот же scope владеет очередью, семафором, точной worker task и pulse-задачами.
- `ShutdownAsync` сначала закрывает вход и отписывается от Canvas, затем отменяет и ожидает фоновые задачи.

### Как событие попадает к пользователю (пошагово)

1) **UI-поток получает WPF-событие** (например, `Canvas.MouseMove`).  
2) Код Mouse API формирует отдельный `Action` для каждого подписчика и вызывает `TryEnqueue(action)`.
3) `TryEnqueue`:
   - проверяет, что scope всё ещё current, его token не отменён и shutdown не начался;
   - добавляет `action` в очередь;
   - делает `_eventSignal.Release()` — будит воркер.
4) **Фоновый воркер**:
   - ждёт `_eventSignal.WaitAsync(token)` (без busy-wait);
   - после пробуждения **вычитывает очередь “пачкой”** (drain), выполняя все накопившиеся `Action` подряд;
   - ошибки пользовательского кода перехватываются, чтобы один упавший обработчик не ломал доставку остальных событий.

### Почему воркер “сливает очередь пачкой”, а не по одному событию
После одного `WaitAsync` воркер выполняет цикл `TryDequeue` до пустой очереди. Это снижает накладные расходы:
- меньше переключений и ожиданий на семафоре;
- быстрее разгребаются всплески событий (например, частые `MouseMove`).

### Что происходит при завершении Mouse API
`CanvasGraphicsContext.DisposeAsync` вызывает идемпотентный `Mouse.ShutdownAsync`: новые события сразу перестают приниматься, пять WPF-подписок снимаются с исходного Canvas, linked token отменяется, текущий handler и pulse-задачи ожидаются. Затем очищаются очередь, polling-state и три публичных события. Только после этого контекст освобождает Graphics/Dispatcher и host может выгружать пользовательскую ALC.

### Важные свойства/ограничения (их стоит знать)
- **Один consumer**: обработчики выполняются последовательно в одном фоне (упрощает модель и снижает гонки в пользовательском коде).
- **Порядок**: при обычном сценарии (всё приходит из UI-потока) порядок близок к FIFO. При конкурентных `Enqueue` из разных потоков строгой “временной” упорядоченности ожидать не стоит.
- **Нет backpressure**: если обработчики медленные, очередь может расти; зато UI не блокируется.
- **События могут быть отброшены**: после начала cleanup новые и ещё queued handlers намеренно не выполняются.
- **Per-run контракт**: events, state, pulse и очередь не переносятся в следующий запуск.
