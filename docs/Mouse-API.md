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

## Позиция курсора

### CurrentPosition

Текущая координата курсора относительно верхнего левого угла Canvas.

**Сигнатура:**
```csharp
public static Point? CurrentPosition { get; }
```

**Возвращает:** `Point?` - координаты курсора или `null`, если курсор сейчас не на Canvas.

**Примеры:**
```csharp
var position = Mouse.CurrentPosition;
if (position.HasValue)
{
    Console.WriteLine($"Курсор на позиции: {position.Value.X}, {position.Value.Y}");
}
else
{
    Console.WriteLine("Курсор вне Canvas");
}
```

### LastActualPosition

Последняя актуальная позиция курсора относительно верхнего левого угла Canvas.

**Сигнатура:**
```csharp
public static Point LastActualPosition { get; }
```

**Возвращает:** `Point` - последняя известная позиция курсора на Canvas.

**Примеры:**
```csharp
var lastPosition = Mouse.LastActualPosition;
Console.WriteLine($"Последняя позиция: {lastPosition.X}, {lastPosition.Y}");
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

## Состояние нажатых кнопок

### CurrentPressedButton

Код с информацией о текущей нажатой кнопке мыши.

**Сигнатура:**
```csharp
public static PressButtonStatus CurrentPressedButton { get; }
```

**Возвращает:** `PressButtonStatus` - текущее состояние нажатых кнопок. Может включать комбинации флагов `LeftButton`, `RightButton` и `OutOfArea`.

**Логика:**
- Если курсор на Canvas: возвращает только флаги нажатых кнопок (без `OutOfArea`)
- Если курсор вне Canvas: возвращает `OutOfArea | флаги нажатых кнопок`

**Примеры:**
```csharp
// Проверка нажатой кнопки
if ((Mouse.CurrentPressedButton & PressButtonStatus.LeftButton) != 0)
{
    Console.WriteLine("Левая кнопка нажата");
}

// Проверка комбинации кнопок
if (Mouse.CurrentPressedButton == (PressButtonStatus.LeftButton | PressButtonStatus.RightButton))
{
    Console.WriteLine("Обе кнопки нажаты одновременно");
}

// Проверка, находится ли курсор на Canvas
if ((Mouse.CurrentPressedButton & PressButtonStatus.OutOfArea) == 0)
{
    Console.WriteLine("Курсор на Canvas");
}
else
{
    Console.WriteLine("Курсор вне Canvas");
}
```

### LastActualPressedButton

Код с информацией о последней нажатой кнопке мыши на Canvas.

**Сигнатура:**
```csharp
public static PressButtonStatus LastActualPressedButton { get; }
```

**Возвращает:** `PressButtonStatus` - последнее состояние нажатых кнопок, когда курсор был на Canvas. Никогда не содержит флаг `OutOfArea`.

**Примеры:**
```csharp
var lastState = Mouse.LastActualPressedButton;
if ((lastState & PressButtonStatus.LeftButton) != 0)
{
    Console.WriteLine("Последняя нажатая кнопка на Canvas была левая");
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

- Все операции с UI выполняются в UI потоке через `InvokeOnUI()`
- События Canvas уже приходят в UI потоке
- Все свойства возвращают значения через `InvokeOnUI()` для обеспечения потокобезопасности

### Координаты

- Все координаты относительно верхнего левого угла Canvas
- Используется `e.GetPosition(canvas)` для получения координат
- `CurrentPosition` возвращает `null`, если курсор вне Canvas

## Примеры использования

### Отслеживание позиции курсора

```csharp
using System;
using System.Threading;
using KID;

while (true)
{
    StopManager.StopIfButtonPressed();
    
    var position = Mouse.CurrentPosition;
    if (position.HasValue)
    {
        Graphics.Color = "Green";
        Graphics.Circle(position.Value.X, position.Value.Y, 1);
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
    
    var pressed = Mouse.CurrentPressedButton;
    
    // Проверка нажатой левой кнопки
    if ((pressed & PressButtonStatus.LeftButton) != 0)
    {
        var pos = Mouse.CurrentPosition;
        if (pos.HasValue)
        {
            Graphics.Color = "Blue";
            Graphics.Circle(pos.Value.X, pos.Value.Y, 3);
        }
    }
    
    // Проверка комбинации кнопок
    if (pressed == (PressButtonStatus.LeftButton | PressButtonStatus.RightButton))
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
    var position = Mouse.CurrentPosition;
    // ...
}
```
