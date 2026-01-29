using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using KID;
using Keyboard = KID.Keyboard;
using Mouse = KID.Mouse;

// Рисовалка мышью.
// - Зажми ЛЕВУЮ кнопку мыши — рисуешь.
// - Зажми ПРАВУЮ кнопку мыши — стираешь.
// - C = сменить цвет
// - Up/Down = изменить толщину
// - R = очистить холст
//
// Важно: колёсика мыши нет в текущем Mouse API KID.

Console.WriteLine("Рисовалка мышью:");
Console.WriteLine("- Зажми ЛЕВУЮ кнопку — рисуешь");
Console.WriteLine("- Зажми ПРАВУЮ кнопку — стираешь");
Console.WriteLine("- C = цвет, Up/Down = толщина, R = очистить, Esc = выход");

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
        Console.WriteLine($"Цвет: {palette[colorIndex]}");
    }

    if (Keyboard.WasPressed(Key.Up))
    {
        thickness = Math.Min(30, thickness + 1);
        Console.WriteLine($"Толщина: {thickness:0}");
    }
    if (Keyboard.WasPressed(Key.Down))
    {
        thickness = Math.Max(1, thickness - 1);
        Console.WriteLine($"Толщина: {thickness:0}");
    }

    if (Keyboard.WasPressed(Key.R))
    {
        Graphics.Clear();
        segments.Clear();
        Console.WriteLine("Холст очищен.");
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
                // UI элемент -> толщину меняем в UI потоке
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

    // Идём с конца (безопасно удалять во время прохода)
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

