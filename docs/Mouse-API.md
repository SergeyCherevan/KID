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

