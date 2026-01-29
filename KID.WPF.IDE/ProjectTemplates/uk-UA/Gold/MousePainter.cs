using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using KID;
using Keyboard = KID.Keyboard;
using Mouse = KID.Mouse;

// Малювання мишею.
// - Затисни ЛІВУ кнопку миші — малюєш.
// - Затисни ПРАВУ кнопку миші — стираєш.
// - C = змінити колір
// - Up/Down = змінити товщину
// - R = очистити полотно
//
// Важливо: коліщатка миші немає в поточному Mouse API KID.

Console.WriteLine("Малювання мишею:");
Console.WriteLine("- Затисни ЛІВУ кнопку — малюєш");
Console.WriteLine("- Затисни ПРАВУ кнопку — стираєш");
Console.WriteLine("- C = колір, Up/Down = товщина, R = очистити, Esc = вихід");

string[] palette = { "DeepSkyBlue", "Lime", "Orange", "HotPink", "Yellow", "White" };
int colorIndex = 0;
double thickness = 4;
double eraserRadius = 20;

var segments = new List<Segment>();

bool isDrawing = false;
Point last = new Point(0, 0);

while (true)
{
    StopManager.StopIfButtonPressed();

    if (Keyboard.WasPressed(Key.Escape))
        break;

    if (Keyboard.WasPressed(Key.C))
    {
        colorIndex = (colorIndex + 1) % palette.Length;
        Console.WriteLine($"Колір: {palette[colorIndex]}");
    }

    if (Keyboard.WasPressed(Key.Up))
    {
        thickness = Math.Min(30, thickness + 1);
        Console.WriteLine($"Товщина: {thickness:0}");
    }
    if (Keyboard.WasPressed(Key.Down))
    {
        thickness = Math.Max(1, thickness - 1);
        Console.WriteLine($"Товщина: {thickness:0}");
    }

    if (Keyboard.WasPressed(Key.R))
    {
        Graphics.Clear();
        segments.Clear();
        Console.WriteLine("Полотно очищено.");
    }

    var cursor = Mouse.CurrentCursor;
    bool leftPressed = (cursor.PressedButton & PressButtonStatus.LeftButton) != 0;
    bool rightPressed = (cursor.PressedButton & PressButtonStatus.RightButton) != 0;

    if (!cursor.Position.HasValue)
    {
        isDrawing = false;
        continue;
    }

    var p = cursor.Position.Value;

    if (rightPressed)
    {
        EraseAt(p, eraserRadius);
    }

    if (leftPressed)
    {
        if (isDrawing)
        {
            Graphics.Color = palette[colorIndex];
            Line? line = Graphics.Line(last, p);
            if (line != null)
            {
                // UI елемент -> товщину змінюємо в UI-потоці
                DispatcherManager.InvokeOnUI(() => line.StrokeThickness = thickness);
                segments.Add(new Segment(line, last, p));
            }
        }

        isDrawing = true;
        last = p;
    }
    else
    {
        isDrawing = false;
    }
}

void EraseAt(Point center, double r)
{
    double r2 = r * r;

    // Йдемо з кінця (безпечно видаляти під час проходу)
    for (int i = segments.Count - 1; i >= 0; i--)
    {
        var s = segments[i];
        double d2 = DistancePointToSegmentSquared(center, s.A, s.B);
        if (d2 <= r2)
        {
            s.Line.RemoveFromCanvas();
            segments.RemoveAt(i);
        }
    }
}

static double DistancePointToSegmentSquared(Point p, Point a, Point b)
{
    double vx = b.X - a.X;
    double vy = b.Y - a.Y;
    double wx = p.X - a.X;
    double wy = p.Y - a.Y;

    double c1 = wx * vx + wy * vy;
    if (c1 <= 0)
        return (p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y);

    double c2 = vx * vx + vy * vy;
    if (c2 <= c1)
        return (p.X - b.X) * (p.X - b.X) + (p.Y - b.Y) * (p.Y - b.Y);

    double t = c1 / c2;
    double px = a.X + t * vx;
    double py = a.Y + t * vy;
    double dx = p.X - px;
    double dy = p.Y - py;
    return dx * dx + dy * dy;
}

readonly struct Segment
{
    public Segment(Line line, Point a, Point b)
    {
        Line = line;
        A = a;
        B = b;
    }

    public Line Line { get; }
    public Point A { get; }
    public Point B { get; }
}

