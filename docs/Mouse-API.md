# Mouse API - Документация

## Обзор

Mouse API предоставляет функциональность для получения информации о позиции курсора и кликах мыши на Canvas, аналогичную командам работы с мышью в BASIC256. API поддерживает отслеживание позиции курсора, обработку кликов (одиночных и двойных), отслеживание состояния нажатых кнопок и события мыши.

## Инициализация

Mouse API автоматически инициализируется при выполнении кода вместе с Graphics API. Не требуется явная инициализация в пользовательском коде.

## Структуры данных

### ClickStatus

Enum для статуса клика мыши.

**Значения:**
- `NoClick` - нет клика
- `OneLeftClick` - одиночный клик левой кнопкой мыши
- `OneRightClick` - одиночный клик правой кнопкой мыши
- `DoubleLeftClick` - двойной клик левой кнопкой мыши
- `DoubleRightClick` - двойной клик правой кнопкой мыши

### MouseClickInfo

Структура с информацией о клике мыши на Canvas.

**Свойства:**
- `Status` (ClickStatus) - статус клика
- `Position` (Point?) - координаты клика относительно верхнего левого угла Canvas. null, если клик не был зарегистрирован

### PressButtonStatus

Enum с флагами для состояния нажатых кнопок мыши.

**Значения:**
- `NoButton` (0b000) - нет нажатых кнопок
- `LeftButton` (0b001) - нажата левая кнопка мыши
- `RightButton` (0b010) - нажата правая кнопка мыши
- `OutOfArea` (0b100) - курсор находится вне Canvas

**Примечание:** Флаги могут комбинироваться (например, `LeftButton | RightButton` для обеих кнопок одновременно). Флаг `OutOfArea` может комбинироваться с флагами кнопок, если курсор вышел за пределы Canvas, но кнопки все еще нажаты.

### CursorInfo

Структура с информацией о курсоре мыши на Canvas.

**Свойства:**
- `Position` (Point?) - позиция курсора относительно верхнего левого угла Canvas. null, если курсор сейчас не на Canvas
- `PressedButton` (PressButtonStatus) - состояние нажатых кнопок мыши. Может включать комбинации флагов LeftButton, RightButton и OutOfArea

## Информация о курсоре

### CurrentCursor

Информация о текущем состоянии курсора на Canvas. Объединяет позицию курсора и состояние нажатых кнопок.

**Сигнатура:**
```csharp
public static CursorInfo CurrentCursor { get; }
```

**Возвращает:** `CursorInfo` - информация о текущем состоянии курсора.

**Логика:**
- Если курсор на Canvas: `Position` содержит координаты, `PressedButton` содержит только флаги нажатых кнопок (без `OutOfArea`)
- Если курсор вне Canvas: `Position` равен `null`, `PressedButton` содержит `OutOfArea | флаги нажатых кнопок`

**Примеры:**
```csharp
var cursor = Mouse.CurrentCursor;

// Проверка позиции курсора
if (cursor.Position.HasValue)
{
    Console.WriteLine($"Курсор на позиции: {cursor.Position.Value.X}, {cursor.Position.Value.Y}");
}
else
{
    Console.WriteLine("Курсор вне Canvas");
}

// Проверка нажатой кнопки
if ((cursor.PressedButton & PressButtonStatus.LeftButton) != 0)
{
    Console.WriteLine("Левая кнопка нажата");
}

// Проверка комбинации кнопок
if (cursor.PressedButton == (PressButtonStatus.LeftButton | PressButtonStatus.RightButton))
{
    Console.WriteLine("Обе кнопки нажаты одновременно");
}

// Проверка, находится ли курсор на Canvas
if ((cursor.PressedButton & PressButtonStatus.OutOfArea) == 0)
{
    Console.WriteLine("Курсор на Canvas");
}
else
{
    Console.WriteLine("Курсор вне Canvas");
}
```

### LastActualCursor

Информация о последнем актуальном состоянии курсора на Canvas. Объединяет последнюю позицию курсора и последнее состояние нажатых кнопок.

**Сигнатура:**
```csharp
public static CursorInfo LastActualCursor { get; }
```

**Возвращает:** `CursorInfo` - информация о последнем актуальном состоянии курсора на Canvas. `PressedButton` никогда не содержит флаг `OutOfArea`.

**Примеры:**
```csharp
var lastCursor = Mouse.LastActualCursor;
Console.WriteLine($"Последняя позиция: {lastCursor.Position.X}, {lastCursor.Position.Y}");

if ((lastCursor.PressedButton & PressButtonStatus.LeftButton) != 0)
{
    Console.WriteLine("Последняя нажатая кнопка на Canvas была левая");
}
```

## Информация о кликах

### CurrentClick

Структура с информацией о текущем клике по Canvas.

**Сигнатура:**
```csharp
public static MouseClickInfo CurrentClick { get; }
```

**Возвращает:** `MouseClickInfo` - информация о текущем клике.

**Примеры:**
```csharp
var click = Mouse.CurrentClick;
if (click.Status == ClickStatus.OneLeftClick)
{
    Console.WriteLine($"Левый клик на позиции: {click.Position.Value.X}, {click.Position.Value.Y}");
}
else if (click.Status == ClickStatus.DoubleRightClick)
{
    Console.WriteLine("Двойной клик правой кнопкой");
}
```

### LastClick

Структура с информацией о последнем зарегистрированном клике.

**Сигнатура:**
```csharp
public static MouseClickInfo LastClick { get; }
```

**Возвращает:** `MouseClickInfo` - информация о последнем клике.

**Примеры:**
```csharp
var lastClick = Mouse.LastClick;
if (lastClick.Status != ClickStatus.NoClick)
{
    Console.WriteLine($"Последний клик: {lastClick.Status}");
}
```

## События мыши

### MouseMoveEvent

Событие перемещения мыши по Canvas.

**Сигнатура:**
```csharp
public static event EventHandler<Point>? MouseMoveEvent;
```

**Параметры события:**
- `sender` - всегда `null`
- `position` (Point) - позиция курсора относительно верхнего левого угла Canvas

**Примеры:**
```csharp
Mouse.MouseMoveEvent += (sender, position) =>
{
    Console.WriteLine($"Мышь переместилась на позицию: {position.X}, {position.Y}");
};
```

### MouseClickEvent

Событие клика мыши по Canvas.

**Сигнатура:**
```csharp
public static event EventHandler<MouseClickInfo>? MouseClickEvent;
```

**Параметры события:**
- `sender` - всегда `null`
- `clickInfo` (MouseClickInfo) - информация о клике

**Примеры:**
```csharp
Mouse.MouseClickEvent += (sender, clickInfo) =>
{
    Console.WriteLine($"Клик: {clickInfo.Status} на позиции {clickInfo.Position.Value.X}, {clickInfo.Position.Value.Y}");
};
```

## Особенности реализации

### Обработка двойных кликов

- WPF предоставляет событие `MouseDoubleClick` только для левой кнопки
- Для правой кнопки используется отслеживание двух кликов в короткий промежуток времени (500ms)
- Двойной клик определяется по времени между кликами и близости позиций (допуск 5 пикселей)

### Потокобезопасность

- Все операции с UI выполняются в UI потоке через `DispatcherManager.InvokeOnUI()`
- События Canvas уже приходят в UI потоке
- Все свойства возвращают значения через `DispatcherManager.InvokeOnUI()` для обеспечения потокобезопасности
- `DispatcherManager` автоматически инициализируется при создании контекста выполнения

### Координаты

- Все координаты относительно верхнего левого угла Canvas
- Используется `e.GetPosition(canvas)` для получения координат
- `CurrentCursor.Position` возвращает `null`, если курсор вне Canvas

## Примеры использования

### Отслеживание позиции курсора

```csharp
using System;
using System.Threading;
using KID;

while (true)
{
    StopManager.StopIfButtonPressed();
    
    var cursor = Mouse.CurrentCursor;
    if (cursor.Position.HasValue)
    {
        Graphics.Color = "Green";
        Graphics.Circle(cursor.Position.Value.X, cursor.Position.Value.Y, 1);
    }
    
    Thread.Sleep(10);
}
```

### Обработка кликов

```csharp
using System;
using KID;

while (true)
{
    StopManager.StopIfButtonPressed();
    
    var click = Mouse.CurrentClick;
    if (click.Status == ClickStatus.OneLeftClick && click.Position.HasValue)
    {
        Graphics.Color = "Red";
        Graphics.Circle(click.Position.Value.X, click.Position.Value.Y, 5);
        Console.WriteLine($"Клик на {click.Position.Value.X}, {click.Position.Value.Y}");
    }
    
    Thread.Sleep(10);
}
```

### Отслеживание нажатых кнопок

```csharp
using System;
using System.Threading;
using KID;

while (true)
{
    StopManager.StopIfButtonPressed();
    
    var cursor = Mouse.CurrentCursor;
    
    // Проверка нажатой левой кнопки
    if ((cursor.PressedButton & PressButtonStatus.LeftButton) != 0)
    {
        if (cursor.Position.HasValue)
        {
            Graphics.Color = "Blue";
            Graphics.Circle(cursor.Position.Value.X, cursor.Position.Value.Y, 3);
        }
    }
    
    // Проверка комбинации кнопок
    if (cursor.PressedButton == (PressButtonStatus.LeftButton | PressButtonStatus.RightButton))
    {
        Console.WriteLine("Обе кнопки нажаты!");
    }
    
    Thread.Sleep(10);
}
```

### Использование событий

```csharp
using System;
using KID;

// Подписка на событие перемещения
Mouse.MouseMoveEvent += (sender, position) =>
{
    Console.WriteLine($"Мышь: {position.X}, {position.Y}");
};

// Подписка на событие клика
Mouse.MouseClickEvent += (sender, clickInfo) =>
{
    if (clickInfo.Status == ClickStatus.DoubleLeftClick)
    {
        Console.WriteLine("Двойной клик левой кнопкой!");
    }
};

// Основной цикл программы
while (true)
{
    StopManager.StopIfButtonPressed();
    Thread.Sleep(100);
}
```

## Интеграция с StopManager

Все операции Mouse API автоматически выполняются в UI потоке и не требуют явной проверки `StopManager`. Однако, если вы используете Mouse API в цикле, рекомендуется проверять `StopManager.StopIfButtonPressed()` для корректной остановки программы.

```csharp
while (true)
{
    StopManager.StopIfButtonPressed();
    
    // Работа с Mouse API
    var cursor = Mouse.CurrentCursor;
    // ...
}
```
