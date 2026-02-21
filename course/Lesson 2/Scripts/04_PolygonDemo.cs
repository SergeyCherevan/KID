using System;
using KID;
using System.Windows;

Console.WriteLine("Треугольник и пятиугольник");

// Треугольник
Graphics.Color = "Tomato";
Graphics.Polygon(new Point[] {
    new Point(150, 40),
    new Point(80, 180),
    new Point(220, 180)
});

// Пятиугольник
Graphics.Color = "SteelBlue";
Graphics.Polygon(new Point[] {
    new Point(150, 60),
    new Point(200, 120),
    new Point(170, 200),
    new Point(130, 200),
    new Point(100, 120)
});
