# Graphics API - Документация

## Обзор

Graphics API предоставляет упрощённый интерфейс для рисования графики в пользовательском коде. Все методы автоматически выполняются в UI потоке, что обеспечивает безопасность при работе с WPF элементами.

## Инициализация

Graphics API автоматически инициализируется при выполнении кода. Не требуется явная инициализация в пользовательском коде.

## Работа с цветами

### Свойства

#### `FillColor`
Цвет заливки для фигур (круги, прямоугольники, эллипсы, многоугольники).

**Примеры:**
```csharp
Graphics.FillColor = "Red";
Graphics.FillColor = "Blue";
Graphics.FillColor = "#FF0000";
Graphics.FillColor = (255, 0, 0);  // RGB кортеж
Graphics.FillColor = 0xFF0000;   // Целое число
```

#### `StrokeColor`
Цвет обводки для фигур.

**Примеры:**
```csharp
Graphics.StrokeColor = "Black";
Graphics.StrokeColor = (0, 0, 0);
```

#### `Color`
Устанавливает одновременно и заливку, и обводку.

**Примеры:**
```csharp
Graphics.Color = "Green";
Graphics.Color = (0, 255, 0);
```

### Поддерживаемые форматы цветов

1. **Строки:** Названия цветов или hex-коды
   - `"Red"`, `"Blue"`, `"Green"`
   - `"#FF0000"`, `"#00FF00"`

2. **RGB кортежи:** `(byte r, byte g, byte b)`
   - `(255, 0, 0)` — красный
   - `(0, 255, 0)` — зелёный

3. **Целые числа:** RGB в формате `0xRRGGBB`
   - `0xFF0000` — красный
   - `0x00FF00` — зелёный

## Простые фигуры

### Circle (Круг)

**Сигнатуры:**
```csharp
Ellipse? Circle(double x, double y, double radius)
Ellipse? Circle(Point center, double radius)
```

**Параметры:**
- `x, y` — координаты центра круга
- `center` — точка центра круга
- `radius` — радиус круга

**Примеры:**
```csharp
Graphics.Color = "Blue";
Graphics.Circle(150, 150, 50);  // Круг в точке (150, 150) с радиусом 50

Point center = new Point(200, 200);
Graphics.Circle(center, 75);     // Круг в точке center с радиусом 75
```

**Возвращает:** `Ellipse` объект для дальнейшей модификации или `null` если Canvas не инициализирован.

### Ellipse (Эллипс)

**Сигнатуры:**
```csharp
Ellipse? Ellipse(double x, double y, double radiusX, double radiusY)
Ellipse? Ellipse(Point center, double radiusX, double radiusY)
```

**Параметры:**
- `x, y` — координаты центра эллипса
- `center` — точка центра эллипса
- `radiusX` — горизонтальный радиус
- `radiusY` — вертикальный радиус

**Примеры:**
```csharp
Graphics.Color = "Red";
Graphics.Ellipse(100, 100, 80, 40);  // Эллипс с радиусами 80x40
```

### Rectangle (Прямоугольник)

**Сигнатуры:**
```csharp
Rectangle? Rectangle(double x, double y, double width, double height)
Rectangle? Rectangle(Point topLeft, double width, double height)
Rectangle? Rectangle(Point topLeft, Point size)
```

**Параметры:**
- `x, y` — координаты левого верхнего угла
- `topLeft` — точка левого верхнего угла
- `width` — ширина прямоугольника
- `height` — высота прямоугольника
- `size` — размер прямоугольника (Point с X=width, Y=height)

**Примеры:**
```csharp
Graphics.Color = "Green";
Graphics.Rectangle(50, 50, 100, 80);  // Прямоугольник 100x80

Point pos = new Point(200, 200);
Point size = new Point(150, 100);
Graphics.Rectangle(pos, size);        // Прямоугольник в точке pos размером size
```

### Line (Линия)

**Сигнатуры:**
```csharp
Line? Line(double x1, double y1, double x2, double y2)
Line? Line(Point p1, Point p2)
```

**Параметры:**
- `x1, y1` — координаты начала линии
- `x2, y2` — координаты конца линии
- `p1` — точка начала линии
- `p2` — точка конца линии

**Примеры:**
```csharp
Graphics.StrokeColor = "Black";
Graphics.Line(0, 0, 300, 300);  // Линия от (0, 0) до (300, 300)

Point start = new Point(50, 50);
Point end = new Point(250, 250);
Graphics.Line(start, end);
```

**Примечание:** Линия использует только `StrokeColor`, `FillColor` не применяется.

### Polygon (Многоугольник)

**Сигнатуры:**
```csharp
Polygon? Polygon(Point[] points)
Polygon? Polygon((double x, double y)[] points)
```

**Параметры:**
- `points` — массив точек вершин многоугольника

**Примеры:**
```csharp
Graphics.Color = "Purple";
Point[] triangle = new Point[]
{
    new Point(150, 50),
    new Point(50, 150),
    new Point(250, 150)
};
Graphics.Polygon(triangle);  // Треугольник

// Используя кортежи
var square = new (double x, double y)[]
{
    (100, 100),
    (200, 100),
    (200, 200),
    (100, 200)
};
Graphics.Polygon(square);  // Квадрат
```

### QuadraticBezier (Квадратичная кривая Безье)

**Сигнатура:**
```csharp
Path? QuadraticBezier(Point[] points)
```

**Параметры:**
- `points` — массив из 3 точек: [начало, контрольная точка, конец]

**Примеры:**
```csharp
Graphics.StrokeColor = "Blue";
Point[] bezier = new Point[]
{
    new Point(50, 200),   // Начало
    new Point(150, 50),   // Контрольная точка
    new Point(250, 200)   // Конец
};
Graphics.QuadraticBezier(bezier);
```

### CubicBezier (Кубическая кривая Безье)

**Сигнатура:**
```csharp
Path? CubicBezier(Point[] points)
```

**Параметры:**
- `points` — массив из 4 точек: [начало, контрольная точка 1, контрольная точка 2, конец]

**Примеры:**
```csharp
Graphics.StrokeColor = "Red";
Point[] bezier = new Point[]
{
    new Point(50, 200),   // Начало
    new Point(100, 50),   // Контрольная точка 1
    new Point(200, 50),   // Контрольная точка 2
    new Point(250, 200)   // Конец
};
Graphics.CubicBezier(bezier);
```

## Работа с изображениями

### Image (Загрузка и отрисовка изображений)

**Сигнатуры:**
```csharp
Image? Image(double x, double y, string path, double? width = null, double? height = null)
Image? Image(Point position, string path, double? width = null, double? height = null)
Image? Image(Point position, string path, Point size)
```

**Параметры:**
- `x, y` — координаты позиции изображения (левый верхний угол)
- `position` — точка позиции изображения
- `path` — путь к файлу изображения
- `width` — ширина изображения (опционально, если не указано — оригинальный размер)
- `height` — высота изображения (опционально, если не указано — оригинальный размер)
- `size` — размер изображения (Point с X=width, Y=height)

**Примеры:**
```csharp
// Загрузка изображения с оригинальным размером
Graphics.Image(50, 50, "image.png");

// Загрузка изображения с указанием размеров
Graphics.Image(100, 100, "image.png", 200, 150);

// Использование Point для позиции
Point pos = new Point(200, 200);
Graphics.Image(pos, "image.png", 150, 100);

// Использование Point для размера
Graphics.Image(new Point(300, 300), "image.png", new Point(120, 80));
```

**Поддерживаемые форматы:**
- PNG, JPG, BMP, GIF, TIFF, ICO и другие форматы, поддерживаемые WPF BitmapImage

**Примечания:**
- Если файл не существует или путь некорректен, метод возвращает `null`
- Все операции выполняются в UI потоке
- Изображение автоматически добавляется на Canvas

### SetSource (Изменение источника изображения)

**Сигнатуры:**
```csharp
Image? SetSource(this Image image, string path, double? width = null, double? height = null)
Image? SetSource(this Image image, string path, Point size)
```

**Параметры:**
- `image` — объект Image, созданный методом `Image()`
- `path` — путь к новому файлу изображения
- `width` — новая ширина (опционально)
- `height` — новая высота (опционально)
- `size` — новый размер (Point)

**Примеры:**
```csharp
var img = Graphics.Image(50, 50, "image1.png", 100, 100);
// Позже можно изменить изображение
img.SetSource("image2.png");
// Или изменить изображение и размер
img.SetSource("image3.png", 150, 150);
```

**Примечание:** Метод возвращает `Image?` для цепочки вызовов.

## Работа с текстом

### SetFont (Установка шрифта)

**Сигнатура:**
```csharp
void SetFont(string fontName, double fontSize)
```

**Параметры:**
- `fontName` — название шрифта (например, "Arial", "Times New Roman")
- `fontSize` — размер шрифта

**Примеры:**
```csharp
Graphics.SetFont("Arial", 24);
Graphics.Text(100, 100, "Привет!");  // Текст будет отображён шрифтом Arial размером 24
```

### Text (Вывод текста)

**Сигнатуры:**
```csharp
TextBlock? Text(double x, double y, string text)
TextBlock? Text(Point position, string text)
```

**Параметры:**
- `x, y` — координаты позиции текста
- `position` — точка позиции текста
- `text` — текст для отображения

**Примеры:**
```csharp
Graphics.Color = "Black";
Graphics.SetFont("Arial", 20);
Graphics.Text(50, 50, "Hello, World!");

Point pos = new Point(100, 100);
Graphics.Text(pos, "Привет, Мир!");
```

**Примечание:** Текст использует текущий `FillColor` для цвета.

### SetText (Изменение текста)

**Сигнатура:**
```csharp
TextBlock? SetText(this TextBlock textBlock, string text)
```

**Параметры:**
- `textBlock` — объект TextBlock, созданный методом `Text()`
- `text` — новый текст

**Примеры:**
```csharp
var textBlock = Graphics.Text(100, 100, "Старый текст");
// Позже можно изменить текст
textBlock.SetText("Новый текст");
```

## Методы расширения для элементов

Все методы расширения возвращают элемент для цепочки вызовов (method chaining). После рефакторинга методы работают для всех элементов, наследуемых от `UIElement`/`FrameworkElement`: `Shape`, `Image`, `TextBlock` и других.

### Позиционирование

#### SetLeftX, SetTopY, SetLeftTopXY
Устанавливают позицию элемента относительно левого верхнего угла. Работают для всех `UIElement`.

```csharp
// Для фигур
var rect = Graphics.Rectangle(0, 0, 100, 50);
rect.SetLeftX(200);           // Переместить в X=200
rect.SetTopY(100);             // Переместить в Y=100
rect.SetLeftTopXY(200, 100);   // Переместить в (200, 100)
rect.SetLeftTopXY(new Point(200, 100));  // То же самое

// Для изображений
var img = Graphics.Image(0, 0, "image.png");
img.SetLeftX(300);
img.SetTopY(200);

// Для текста
var text = Graphics.Text(0, 0, "Hello");
text.SetLeftX(100);
text.SetTopY(50);
```

#### SetCenterX, SetCenterY, SetCenterXY
Устанавливают позицию центра элемента. Работают для всех `FrameworkElement`.

```csharp
// Для фигур
var circle = Graphics.Circle(0, 0, 50);
circle.SetCenterX(150);        // Центр по X=150
circle.SetCenterY(150);        // Центр по Y=150
circle.SetCenterXY(150, 150);  // Центр в (150, 150)

// Для изображений
var img = Graphics.Image(0, 0, "image.png", 100, 100);
img.SetCenterXY(200, 200);     // Центр изображения в (200, 200)
```

### Размеры

#### SetWidth, SetHeight, SetSize
Изменяют размеры элемента. Работают для всех `FrameworkElement`.

```csharp
// Для фигур
var rect = Graphics.Rectangle(0, 0, 100, 50);
rect.SetWidth(200);            // Ширина = 200
rect.SetHeight(100);            // Высота = 100
rect.SetSize(200, 100);         // Размер = 200x100
rect.SetSize(new Point(200, 100));  // То же самое

// Для изображений
var img = Graphics.Image(0, 0, "image.png");
img.SetWidth(150);              // Ширина = 150
img.SetHeight(100);             // Высота = 100
img.SetSize(200, 150);          // Размер = 200x150
```

### Цвета

#### SetStrokeColor, SetFillColor, SetColor
Изменяют цвета фигуры. Работают только для `Shape` (фигуры имеют свойства Stroke и Fill).

```csharp
var circle = Graphics.Circle(150, 150, 50);
circle.SetFillColor("Red");        // Заливка красная
circle.SetStrokeColor("Blue");     // Обводка синяя
circle.SetColor("Green");           // И заливка, и обводка зелёные
```

**Примечание:** Эти методы не применимы к `Image` и `TextBlock`, так как они не имеют свойств `Stroke` и `Fill`.

### Управление на холсте

#### AddToCanvas, RemoveFromCanvas
Добавляют или удаляют элемент с холста. Работают для всех `UIElement`.

```csharp
// Для фигур
var rect = Graphics.Rectangle(0, 0, 100, 50);
rect.RemoveFromCanvas();  // Удалить с холста
rect.AddToCanvas();        // Вернуть на холст

// Для изображений
var img = Graphics.Image(0, 0, "image.png");
img.RemoveFromCanvas();    // Удалить изображение
img.AddToCanvas();          // Вернуть изображение
```

## Очистка холста

### Clear
Очищает весь холст, удаляя все нарисованные элементы.

```csharp
Graphics.Clear();
```

## Размеры Canvas

Иногда нужно узнать или изменить **реальный размер области рисования** (той части UI, где расположен `Canvas`).

### GetCanvasSize
Возвращает текущий размер `Canvas` **после раскладки WPF** (то есть то, что вы видите на экране).

```csharp
var (w, h) = Graphics.GetCanvasSize();
Console.WriteLine($"Canvas: {w} x {h}");
```

### SetCanvasSize
Меняет размер области графики **через раскладку UI** (программно изменяет размеры строк/колонок в `Grid` — как если бы пользователь перетащил `GridSplitter`).

```csharp
Graphics.SetCanvasSize(800, 600);
```

**Важно:**
- Размеры задаются в DIP (как и `ActualWidth/ActualHeight`).
- Значения могут быть ограничены текущим размером окна, а также минимальными ограничениями панелей (например, минимальная высота области графики и минимальная ширина правой панели).
## Цепочки вызовов (Method Chaining)

Все методы расширения возвращают элемент, что позволяет создавать цепочки вызовов:

```csharp
// Для фигур
Graphics.Circle(150, 150, 50)
    .SetColor("Red")
    .SetCenterXY(200, 200)
    .SetSize(100, 100);

// Для изображений
Graphics.Image(50, 50, "image.png", 100, 100)
    .SetLeftX(200)
    .SetTopY(150)
    .SetSize(150, 150);

// Для текста
Graphics.Text(0, 0, "Hello")
    .SetLeftX(100)
    .SetTopY(50);
```

**Примечание:** При использовании методов, возвращающих разные типы (`UIElement` vs `FrameworkElement`), может потребоваться приведение типов:

```csharp
var img = Graphics.Image(0, 0, "image.png");
(img.SetLeftTopXY(500, 300) as FrameworkElement)?.SetSize(80, 80);
```

## Примеры использования

### Простой пример
```csharp
using System;
using KID;

Graphics.Color = "Blue";
Graphics.Circle(150, 150, 50);

Graphics.Color = "Red";
Graphics.Rectangle(100, 100, 100, 80);

Graphics.Color = "Black";
Graphics.SetFont("Arial", 20);
Graphics.Text(50, 50, "Привет, Мир!");
```

### Анимация (цикл)
```csharp
using System;
using System.Threading;
using KID;

for (int i = 0; i < 10; i++)
{
    Graphics.Clear();
    Graphics.Color = "Blue";
    Graphics.Circle(150 + i * 10, 150, 30);
    Thread.Sleep(100);
}
```

### Сложная композиция
```csharp
using System;
using KID;

// Фон
Graphics.FillColor = "LightGray";
Graphics.Rectangle(0, 0, 400, 400);

// Дом
Graphics.Color = "Brown";
Graphics.Rectangle(150, 200, 100, 100);

// Крыша
Point[] roof = new Point[]
{
    new Point(150, 200),
    new Point(200, 150),
    new Point(250, 200)
};
Graphics.Color = "Red";
Graphics.Polygon(roof);

// Окно
Graphics.FillColor = "Yellow";
Graphics.Rectangle(170, 220, 30, 30);

// Дверь
Graphics.Color = "DarkBrown";
Graphics.Rectangle(210, 250, 20, 50);
```

### Работа с изображениями
```csharp
using System;
using KID;

// Загрузка изображения с оригинальным размером
var img1 = Graphics.Image(50, 50, "logo.png");

// Загрузка изображения с указанием размеров
var img2 = Graphics.Image(200, 50, "photo.jpg", 200, 150);

// Использование методов расширения
var img3 = Graphics.Image(400, 50, "icon.png", 100, 100);
img3.SetLeftX(450)
    .SetTopY(200)
    .SetSize(150, 150);

// Изменение источника изображения
img3.SetSource("new_icon.png", 120, 120);
```

## Архитектура и паттерны (для разработчиков)

Ниже — краткое описание того, какие **паттерны проектирования** и **архитектурные особенности** реализованы в модуле `KIDLibrary/Graphics`.

### Паттерны проектирования в `Graphics`

- **Facade (Фасад) / “Static Gateway”**
  - `Graphics` предоставляет упрощённый API поверх WPF (`Canvas`, `Shape`, `TextBlock`, `Image`) и скрывает детали добавления в `Canvas.Children`, позиционирования, настройки кистей/шрифтов и т.п.

- **Singleton-подобный глобальный объект (через `static`)**
  - Состояние модуля хранится в статике: `Canvas`, текущие `Fill/Stroke` кисти, текущий шрифт/размер. Это единый “графический контекст” на время выполнения пользовательского кода.

- **Proxy / UI Dispatcher (маршаллинг вызовов в UI-поток) + элементы Command**
  - Все публичные операции выполняются через `DispatcherManager.InvokeOnUI(...)`: внешний вызов проксируется в UI-поток, а `Action/Func<T>` выступают в роли “команд” на выполнение.

- **Simple Factory / Factory Method**
  - Методы `Circle/Ellipse/Rectangle/Line/Polygon/*Bezier/Image/Text` создают конкретные WPF-объекты, настраивают их и возвращают для дальнейшей модификации.

- **Adapter / Value Object для цветов**
  - `ColorType` унифицирует ввод цвета в разных форматах (строка, `(byte,byte,byte)`, `int`, `Brush`) и приводит его к `Brush`.

- **Strategy (через замыкание-фабрику кисти)**
  - `ColorType` хранит `Func<Brush>` и “знает”, как построить кисть для конкретного формата, а `CreateBrush()` — единая точка получения результата.

- **Fluent Interface + Extension Methods**
  - Методы расширения (`SetLeftX/SetTopY/SetSize/SetColor/...`) возвращают элемент обратно, позволяя строить цепочки вызовов (fluent API / method chaining).

- **Fail-soft контракт через nullable-возврат**
  - Большинство методов возвращают `null` при невозможности выполнить операцию (например, `Canvas` не инициализирован, неверный путь к файлу, некорректный ввод).

### Архитектурные особенности модуля `Graphics`

- **Граница слоя “KIDLibrary API для пользовательского кода”**
  - `Graphics` находится в `KIDLibrary` и является библиотечным API, предназначенным для вызова из пользовательского кода, а не частью MVVM/сервисного слоя.

- **Жизненный цикл через контекст выполнения**
  - `DispatcherManager` инициализируется в `CodeExecutionContext.Init()`, после чего контекст графики вызывает `Graphics.Init(Canvas)` (и параллельно инициализирует `Mouse`/`Music`), задавая корректный порядок подготовки окружения.

- **Потокобезопасность — базовое правило**
  - Любой доступ к WPF-объектам выполняется через `DispatcherManager`. Для `void`-операций применяется асинхронный путь (через `BeginInvoke`), а для `Func<T>` — синхронный (через `Invoke`), чтобы можно было вернуть созданный/изменённый объект.

- **Модульность через `partial`-разбиение по подсистемам API**
  - `Graphics` разбит на части по областям ответственности (системные методы, цвета, примитивы, текст, изображения, extension methods), что упрощает сопровождение и расширение.

- **Retained-mode модель рисования**
  - Результат рисования — реальные WPF-элементы, добавленные в `Canvas.Children`. Пользователь может хранить ссылку на элемент и модифицировать его (через методы расширения), а `Clear()` очищает холст удалением дочерних элементов.

- **Общий “контекст рисования”**
  - Текущие кисти (`FillColor/StrokeColor/Color`) и шрифт (`SetFont`) влияют на последующие операции, т.е. поведение зависит от порядка вызовов (как в immediate-mode API, но с retained-объектами).

## Важные замечания

1. **UI поток:** Все операции автоматически выполняются в UI потоке через `DispatcherManager`, что обеспечивает безопасность при работе из разных потоков.

2. **Возвращаемые значения:** Методы создания фигур возвращают `null` если Canvas не инициализирован. Всегда проверяйте на `null` перед использованием методов расширения.

3. **Цвета по умолчанию:** По умолчанию используется чёрный цвет для заливки и обводки.

4. **Координаты:** Система координат начинается с (0, 0) в левом верхнем углу холста.

5. **Производительность:** Для большого количества фигур рекомендуется использовать методы расширения для модификации существующих фигур вместо создания новых.

6. **DispatcherManager:** Все операции используют централизованный `DispatcherManager` для работы с UI потоком. `DispatcherManager` автоматически инициализируется при создании контекста выполнения.