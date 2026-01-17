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

Ниже — краткое описание того, **какие паттерны проектирования** и **архитектурные решения** реально применены внутри `KIDLibrary/Mouse`.

### Статический Facade (Singleton-подобный модуль)
- **Что это даёт**: единая точка входа `Mouse.*` для пользовательского кода (состояние + события), без создания объектов.
- **Как реализовано**: `Mouse` — `public static partial class`, инициализация через `Mouse.Init(Canvas)`; при повторной инициализации модуль отписывается от старого `Canvas` и подписывается на новый.

### Observer (события) + асинхронная доставка обработчиков
- **Что это даёт**: реактивная модель через `MouseMoveEvent`, `MousePressButtonEvent`, `MouseClickEvent`.
- **Ключевая особенность**: события собираются в **UI-потоке** (WPF события `Canvas`), но **пользовательские обработчики вызываются в фоне**, чтобы не блокировать UI.

### Producer–Consumer (очередь событий) + “воркер доставки”
- **Что это даёт**: развязку UI-потока (производит события) и фонового потока (потребляет и вызывает обработчики).
- **Как реализовано**:
  - UI-поток ставит `Action` в потокобезопасную очередь.
  - Фоновая задача ждёт сигнал и последовательно исполняет действия.
  - При переинициализации воркер корректно останавливается и очищает очередь.

### Snapshot / DTO-подход к данным (значимые типы)
- **Что это даёт**: наружу отдаются “снимки” состояния и событий, которые удобно передавать и безопасно читать из разных потоков.
- **Как реализовано**: `CursorInfo` и `MouseClickInfo` — `struct`, а не ссылочные объекты; наружу возвращаются копии текущих значений.

### Два способа работы с вводом: Polling + Event-driven
- **Polling**: `CurrentCursor`, `LastActualCursor`, `CurrentClick`, `LastClick` — удобно читать в цикле.
- **События**: `Mouse*Event` — удобно реагировать на действия пользователя.
- Это сделано намеренно: для обучающих/игровых сценариев часто нужен “опрос” в цикле, но также полезна реактивность.

### “Пульс клика” (temporal cache) + защита от гонок версией
- **Что это даёт**: `CurrentClick` держит значение короткое время (около 50–100 мс), чтобы его можно было “поймать” polling’ом, и затем автоматически сбрасывается.
- **Защита от гонок**: сброс выполняется только если за время ожидания не пришёл новый клик (используется версионный счётчик).

### Потокобезопасность и устойчивость к пользовательскому коду
- **Thread-safety**: общее состояние (`CurrentCursor`, `LastActualCursor`, `CurrentClick`, `LastClick`) защищено через `lock`.
- **Fault isolation**: исключения в пользовательских обработчиках перехватываются, чтобы не “уронить” поток доставки событий и не нарушить работу приложения.

### Разделение ответственности внутри модуля (внутренняя архитектура)
- **`Mouse.System.cs`**: интеграция с WPF (`Canvas` events Enter/Leave/Move/Down/Up), вычисление кликов, обновление состояния, “пульс” `CurrentClick`.
- **`Mouse.State.cs`**: публичное состояние для polling (потокобезопасные геттеры).
- **`Mouse.Events.cs`**: транспорт событий (очередь, сигнализация, фоновой воркер).
- **DTO/Enums**: `CursorInfo`, `MouseClickInfo`, `PressButtonStatus`, `ClickStatus` — компактные контракты данных.
