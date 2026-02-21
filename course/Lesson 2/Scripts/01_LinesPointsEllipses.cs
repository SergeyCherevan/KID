using System;
using KID;

Console.WriteLine("Линии, точки и эллипсы");

// Линия от (50, 50) до (250, 50)
Graphics.Color = "DarkBlue";
Graphics.Line(50, 50, 250, 50);

// Несколько «точек» — маленьких кругов
Graphics.Color = "Red";
Graphics.Circle(80, 120, 2);
Graphics.Circle(150, 120, 2);
Graphics.Circle(220, 120, 2);

// Эллипс (овал)
Graphics.Color = "ForestGreen";
Graphics.Ellipse(150, 200, 80, 40);

// Круг для сравнения (та же «высота», но равные радиусы)
Graphics.Color = "Gray";
Graphics.Circle(150, 280, 40);
