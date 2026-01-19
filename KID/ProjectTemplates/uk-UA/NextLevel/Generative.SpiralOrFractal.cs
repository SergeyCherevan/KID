using System;
using System.Threading;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;

// Генеративний малюнок (спіраль).
// Чому навчає: цикли, математика (sin/cos), параметри, експерименти.
//
// Керування:
// - Space = перемалювати
// - Up/Down = змінити кількість точок
// - Left/Right = змінити «закрученість»
// - Esc = вихід

int points = 1200;
double turns = 10; // скільки обертів

Draw();

while (true)
{
    StopManager.StopIfButtonPressed();

    if (Keyboard.WasPressed(Key.Escape))
        break;

    bool redraw = false;

    if (Keyboard.WasPressed(Key.Space))
        redraw = true;

    if (Keyboard.WasPressed(Key.Up))
    {
        points = Math.Min(6000, points + 200);
        redraw = true;
    }
    if (Keyboard.WasPressed(Key.Down))
    {
        points = Math.Max(200, points - 200);
        redraw = true;
    }

    if (Keyboard.WasPressed(Key.Right))
    {
        turns = Math.Min(40, turns + 1);
        redraw = true;
    }
    if (Keyboard.WasPressed(Key.Left))
    {
        turns = Math.Max(1, turns - 1);
        redraw = true;
    }

    if (redraw)
        Draw();

    Thread.Sleep(16);
}

void Draw()
{
    Graphics.Clear();

    (double w, double h) = DispatcherManager.InvokeOnUI(() =>
        (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));
    if (w < 200) w = 500;
    if (h < 200) h = 350;

    double cx = w / 2.0;
    double cy = h / 2.0;
    double maxR = Math.Min(w, h) * 0.45;

    // Палітра: змінюємо колір кожні N кроків
    string[] palette = { "DeepSkyBlue", "Lime", "Yellow", "Orange", "HotPink" };

    // Початкова точка
    double prevX = cx;
    double prevY = cy;

    for (int i = 1; i <= points; i++)
    {
        StopManager.StopIfButtonPressed();

        double t = (double)i / points;      // 0..1
        double angle = t * turns * Math.PI * 2;
        double r = t * maxR;

        double x = cx + Math.Cos(angle) * r;
        double y = cy + Math.Sin(angle) * r;

        Graphics.Color = palette[(i / 120) % palette.Length];
        var line = Graphics.Line(prevX, prevY, x, y);
        if (line != null)
            DispatcherManager.InvokeOnUI(() => line.StrokeThickness = 2);

        prevX = x;
        prevY = y;
    }

    Graphics.Color = "White";
    Graphics.SetFont("Consolas", 16);
    Graphics.Text(10, 10, $"Генеративна спіраль  points={points}  turns={turns}");
    Graphics.Text(10, 30, "Space=перемалювати  Up/Down=точки  Left/Right=оберти  Esc=вихід");
}

