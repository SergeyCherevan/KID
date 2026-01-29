---
name: Добавление Mouse API
overview: Реализация статического класса Mouse для получения информации о позиции курсора и кликах мыши на Canvas, аналогично BASIC256. Класс будет состоять из нескольких частичных файлов и интегрирован с существующей системой инициализации контекста выполнения.
todos:
  - id: create_mouse_folder
    content: Создать папку KID.Library/Mouse/
    status: pending
  - id: create_click_status
    content: "Создать ClickStatus.cs - enum со значениями: NoClick, OneLeftClick, OneRightClick, DoubleLeftClick, DoubleRightClick"
    status: pending
  - id: create_mouse_click_info
    content: Создать MouseClickInfo.cs - структура с полями Status (ClickStatus) и Position (Point?)
    status: pending
  - id: create_mouse_system
    content: Создать Mouse.System.cs - Init(Canvas), InvokeOnUI(), подписка на события Canvas (MouseMove, MouseLeave, MouseLeftButtonDown, MouseRightButtonDown, MouseLeftButtonUp, MouseRightButtonUp, MouseDoubleClick)
    status: pending
  - id: create_mouse_position
    content: Создать Mouse.Position.cs - свойства CurrentPosition (Point?) и LastActualPosition (Point), обновление LastActualPosition в MouseMove
    status: pending
  - id: create_mouse_click
    content: Создать Mouse.Click.cs - свойства CurrentClick и LastClick (MouseClickInfo), логика обработки одиночных и двойных кликов (левая/правая кнопка)
    status: pending
  - id: create_mouse_events
    content: Создать Mouse.Events.cs - события MouseMoveEvent (EventHandler<Point>) и MouseClickEvent (EventHandler<MouseClickInfo>)
    status: pending
  - id: update_graphics_context
    content: Обновить CanvasGraphicsContext.cs - добавить Mouse.Init(canvas) в метод Init()
    status: pending
---

# План реализации Mouse API

## 1. Анализ требований

### Описание функции

Статический класс `Mouse` для получения информации о позиции курсора и кликах мыши на Canvas. Класс должен предоставлять:

- Текущую позицию курсора относительно Canvas (null если курсор вне Canvas)
- Последнюю актуальную позицию курсора
- Информацию о текущем и последнем клике
- События перемещения и клика мыши

### Целевая аудитория

Начинающие программисты, изучающие C# через визуальное программирование.

### Сценарии использования

- Отслеживание позиции курсора для интерактивных приложений
- Обработка кликов мыши для создания интерактивных элементов
- Реакция на перемещение мыши для визуальной обратной связи

## 2. Архитектурный анализ

### Затронутые подсистемы

- **KIDLibrary** - добавление нового статического класса Mouse
- **Code Execution** - инициализация Mouse в контексте выполнения
- **Graphics Context** - интеграция с CanvasGraphicsContext

### Новые компоненты

1. **Mouse.System.cs** - базовая инициализация и системные функции
2. **Mouse.Position.cs** - свойства позиции курсора
3. **Mouse.Click.cs** - обработка кликов и структуры данных
4. **Mouse.Events.cs** - события мыши

### Изменяемые компоненты

1. **CanvasGraphicsContext.cs** - добавление инициализации Mouse.Init()
2. **MouseClickInfo.cs** - структура для информации о клике (новый файл)
3. **ClickStatus.cs** - enum для статуса клика (новый файл)

### Зависимости

- WPF Canvas для получения событий мыши
- System.Windows.Input для обработки событий
- Graphics.Canvas для определения координат относительно Canvas

## 3. Структура файлов

### Новые файлы в `KID.Library/Mouse/`:

1. **Mouse.System.cs** - Init(), InvokeOnUI(), базовые поля
2. **Mouse.Position.cs** - CurrentPosition, LastActualPosition
3. **Mouse.Click.cs** - CurrentClick, LastClick, обработка кликов
4. **Mouse.Events.cs** - MouseMoveEvent, MouseClickEvent
5. **MouseClickInfo.cs** - структура с полями Status и Position
6. **ClickStatus.cs** - enum: NoClick, OneLeftClick, OneRightClick, DoubleLeftClick, DoubleRightClick

### Изменяемые файлы:

1. **CanvasGraphicsContext.cs** - добавление `Mouse.Init(canvas)` в метод `Init()`

## 4. Детальная реализация

### 4.1. ClickStatus.cs

```csharp
namespace KID
{
    public enum ClickStatus
    {
        NoClick,
        OneLeftClick,
        OneRightClick,
        DoubleLeftClick,
        DoubleRightClick
    }
}
```

### 4.2. MouseClickInfo.cs

```csharp
namespace KID
{
    public struct MouseClickInfo
    {
        public ClickStatus Status { get; set; }
        public Point? Position { get; set; }
    }
}
```

### 4.3. Mouse.System.cs

- Статическое поле `Canvas`
- Статическое поле `Dispatcher`
- Метод `Init(Canvas targetCanvas)` - инициализация и подписка на события Canvas
- Метод `InvokeOnUI(Action)` - выполнение в UI потоке
- Подписка на события Canvas: MouseMove, MouseLeave, MouseLeftButtonDown, MouseRightButtonDown, MouseLeftButtonUp, MouseRightButtonUp, MouseDoubleClick

### 4.4. Mouse.Position.cs

- `CurrentPosition` (Point?) - вычисляемое свойство, проверяет IsMouseOver и возвращает координаты или null
- `LastActualPosition` (Point) - последняя известная позиция на Canvas
- Обновление LastActualPosition в обработчике MouseMove

### 4.5. Mouse.Click.cs

- `CurrentClick` (MouseClickInfo) - текущий клик, сбрасывается при следующем клике
- `LastClick` (MouseClickInfo) - последний зарегистрированный клик
- Обработка одиночных и двойных кликов
- Логика определения типа клика (левый/правый, одиночный/двойной)
- Таймер для определения двойного клика (если WPF MouseDoubleClick не срабатывает надежно)

### 4.6. Mouse.Events.cs

- `MouseMoveEvent` (event EventHandler<Point>) - событие перемещения мыши
- `MouseClickEvent` (event EventHandler<MouseClickInfo>) - событие клика мыши
- Вызов событий в обработчиках Canvas

### 4.7. CanvasGraphicsContext.cs

Добавить в метод `Init()`:

```csharp
Mouse.Init(canvas);
```

## 5. Технические детали

### Обработка координат

- Использование `e.GetPosition(canvas)` для получения координат относительно Canvas
- Проверка `canvas.IsMouseOver` для определения нахождения курсора на Canvas
- Использование `Canvas.GetLeft()` и `Canvas.GetTop()` не требуется, так как координаты уже относительно Canvas

### Обработка двойных кликов

- WPF предоставляет событие `MouseDoubleClick`, но оно срабатывает только для левой кнопки
- Для правой кнопки нужно отслеживать два клика в короткий промежуток времени
- Использовать таймер с интервалом ~300-500ms для определения двойного клика

### Потокобезопасность

- Все обновления свойств через `InvokeOnUI()`
- События Canvas уже приходят в UI потоке
- Использование lock для синхронизации доступа к полям при необходимости

## 6. Порядок выполнения задач

1. Создать папку `KID.Library/Mouse/`
2. Создать `ClickStatus.cs` - enum для статуса клика
3. Создать `MouseClickInfo.cs` - структура для информации о клике
4. Создать `Mouse.System.cs` - базовая инициализация и подписка на события
5. Создать `Mouse.Position.cs` - свойства позиции курсора
6. Создать `Mouse.Click.cs` - обработка кликов
7. Создать `Mouse.Events.cs` - события мыши
8. Обновить `CanvasGraphicsContext.cs` - добавить инициализацию Mouse
9. Протестировать функционал

## 7. Оценка сложности

### Задача 1: Создание структуры данных (ClickStatus, MouseClickInfo)

- **Сложность:** Низкая
- **Время:** 15-20 минут
- **Риски:** Минимальные

### Задача 2: Реализация Mouse.System.cs

- **Сложность:** Средняя
- **Время:** 30-45 минут
- **Риски:** Правильная подписка на события WPF, обработка инициализации

### Задача 3: Реализация Mouse.Position.cs

- **Сложность:** Низкая
- **Время:** 20-30 минут
- **Риски:** Правильное определение координат относительно Canvas

### Задача 4: Реализация Mouse.Click.cs

- **Сложность:** Высокая
- **Время:** 60-90 минут
- **Риски:** Корректная обработка двойных кликов, особенно для правой кнопки, синхронизация состояния

### Задача 5: Реализация Mouse.Events.cs

- **Сложность:** Низкая
- **Время:** 15-20 минут
- **Риски:** Минимальные

### Задача 6: Интеграция с CanvasGraphicsContext

- **Сложность:** Низкая
- **Время:** 5-10 минут
- **Риски:** Минимальные

### Задача 7: Тестирование

- **Сложность:** Средняя
- **Время:** 30-45 минут
- **Риски:** Проверка всех сценариев (одиночные/двойные клики, левая/правая кнопка, перемещение)

**Общая оценка:** 2.5-3.5 часа

## 8. Потенциальные проблемы и решения

### Проблема 1: Двойной клик правой кнопкой

**Решение:** Отслеживать время между кликами и использовать таймер для определения двойного клика

### Проблема 2: Координаты при выходе курсора за пределы Canvas

**Решение:** CurrentPosition возвращает null, LastActualPosition сохраняет последнюю позицию

### Проблема 3: Сброс CurrentClick

**Решение:** Сбрасывать CurrentClick при следующем клике или через определенное время

### Проблема 4: Потокобезопасность при доступе из пользовательского кода

**Решение:** Все свойства должны быть потокобезопасными, использовать InvokeOnUI для обновлений

## 9. Примеры использования (для документации)

```csharp
using System;
using KID;

// Инициализация (вызывается автоматически)
Mouse.Init(Graphics.Canvas);

// Получение позиции
Point? current = Mouse.CurrentPosition;
Point last = Mouse.LastActualPosition;

// Обработка кликов
MouseClickInfo click = Mouse.CurrentClick;
if (click.Status == ClickStatus.OneLeftClick)
{
    Console.WriteLine($"Клик на позиции: {click.Position}");
}

// Подписка на события
Mouse.MouseMoveEvent += (sender, position) => {
    Console.WriteLine($"Мышь на позиции: {position}");
};

Mouse.MouseClickEvent += (sender, clickInfo) => {
    Console.WriteLine($"Клик: {clickInfo.Status} на {clickInfo.Position}");
};
```