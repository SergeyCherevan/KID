using System;
using KID;
using System.Windows;

Console.WriteLine("Квадратичная и кубическая кривые Безье");

// Квадратичная кривая: начало, одна контрольная точка и конец
Graphics.Color = "Red";
Graphics.QuadraticBezier(new Point[]
{
    new Point(50, 100),
    new Point(150, 170),
    new Point(250, 100)
});

// Кубическая кривая: начало, две контрольные точки и конец
Graphics.Color = "ForestGreen";
Graphics.CubicBezier(new Point[]
{
    new Point(50, 250),
    new Point(100, 180),
    new Point(200, 320),
    new Point(250, 250)
});
