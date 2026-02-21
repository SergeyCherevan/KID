# Sprite API - Документация

## Обзор

`Sprite` — это объект на графическом поле, который состоит из нескольких графических элементов (`UIElement`), созданных через `KID.Graphics` (фигуры, текст, изображения и т.п.).

С помощью `Sprite` можно:
- показывать/прятать набор элементов как единое целое;
- перемещать их в игровом цикле;
- детектировать столкновения со спрайтами.

## Быстрый старт

```csharp
using System;
using System.Threading;
using KID;

// Создаём элементы «в локальных координатах» (рядом с (0,0))
Graphics.Color = "DodgerBlue";
var body = Graphics.Circle(0, 0, 20);

Graphics.Color = "White";
var label = Graphics.Text(-8, -10, "A");

// Собираем из элементов спрайт и ставим его на поле
var player = new Sprite(200, 150, body, label);

// Двигаем в цикле
for (int i = 0; i < 120; i++)
{
    StopManager.StopIfButtonPressed();
    player.Move(2, 0);
    Thread.Sleep(16);
}
```

## Координаты: `X`, `Y`, `Position` (anchor)

- `Sprite.X` и `Sprite.Y` — это **абсолютная точка-опора (anchor)** спрайта.
- `Sprite.Position` (тип `Point`) — чтение и запись позиции anchor одним свойством; присвоение эквивалентно установке `X` и `Y`.
- Все элементы спрайта смещаются вместе с anchor.
- Перемещение реализовано через `RenderTransform`, поэтому корректно работает и для элементов, у которых не задан `Canvas.Left/Top` (например, `Line`, `Polygon`, `Path`).

## Конструкторы

- `Sprite()` — спрайт с anchor в (0, 0) и пустым списком элементов.
- `Sprite(double x, double y, params UIElement[] graphicElements)` — anchor в (x, y), заданные графические элементы; нулевые элементы отфильтровываются.
- `Sprite(double x, double y, IEnumerable<UIElement> graphicElements)` — то же с коллекцией элементов.
- `Sprite(double x, double y, string imagePath)` — спрайт с anchor в (x, y) и одним элементом — изображением по указанному пути (создаётся через `Graphics.Image`).

Во всех конструкторах список `GraphicElements` инициализируется единообразно (включая фильтрацию null).

Пример — спрайт из изображения и работа с `Position`:

```csharp
// Спрайт из одного изображения
var hero = new Sprite(100, 50, "hero.png");

// Чтение и запись позиции anchor через Position
var p = hero.Position;
hero.Position = new Point(p.X + 10, p.Y);
```

## Видимость: `Show()` / `Hide()` / `IsVisible`

```csharp
var s = new Sprite(100, 100, Graphics.Circle(0, 0, 15));
s.Hide();  // прячет все элементы
s.Show();  // показывает все элементы

// То же самое через свойство:
s.IsVisible = false;
s.IsVisible = true;
```

`Hide()` устанавливает `Visibility.Hidden`, `Show()` — `Visibility.Visible`.

## Перемещение: `Move(dx, dy)`

```csharp
var s = new Sprite(200, 200, Graphics.Rectangle(-20, -10, 40, 20));
s.Move(10, 0).Move(0, 10); // цепочки вызовов
```

## Столкновения: `DetectCollisions(...)`

`DetectCollisions` возвращает список столкновений текущего спрайта с другими спрайтами.

### Как работает (версия 1)
- Для каждого графического элемента вычисляется **bounding-box** (прямоугольник-рамка) в координатах `Graphics.Canvas`.
- Столкновение фиксируется, если bounding-box двух элементов пересекаются.

Это быстрый и простой способ, но он может давать **ложные столкновения** для “тонких” фигур (например, диагональная линия).

### Пример

```csharp
using System;
using System.Threading;
using KID;

var a = new Sprite(150, 150,
    Graphics.Circle(0, 0, 20));

var b = new Sprite(350, 150,
    Graphics.Rectangle(-25, -25, 50, 50));

while (true)
{
    StopManager.StopIfButtonPressed();

    a.Move(2, 0);

    var hits = a.DetectCollisions(new() { b });
    if (hits.Count > 0)
    {
        Console.WriteLine("Collision!");

        // Можно записать свою информацию:
        hits[0].AdditionalInfo = new { Score = 10, When = DateTime.Now };
        break;
    }

    Thread.Sleep(16);
}
```

## Класс `Collision`

`Collision` описывает один “контакт” между двумя элементами.

Свойства:
- `Type: string` — тип столкновения (сейчас используется `"Bounds"`).
- `SpritesPair: (Sprite First, Sprite Second)` — пара столкнувшихся спрайтов.
- `GraphicElementsPair: (UIElement First, UIElement Second)` — пара столкнувшихся графических элементов.
- `AdditionalInfo: dynamic` — произвольные дополнительные данные (для логики ученика).

## Советы по производительности

- В игровом цикле лучше **двигать** существующие элементы/спрайты (`Move`), чем постоянно создавать новые фигуры.
- `DetectCollisions` делает попарные проверки элементов, поэтому большое количество элементов в спрайте увеличивает время проверки.

