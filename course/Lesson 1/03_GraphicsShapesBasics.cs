using System;
using KID;

Console.WriteLine("Рисуем три простые фигуры + текст");

// 1) Круг: x и y — центр, radius — радиус.
Graphics.Color = "Gold";
Graphics.Circle(120, 120, 60);

// 2) Прямоугольник: x и y — левый верхний угол, width/height — размеры.
Graphics.Color = "DodgerBlue";
Graphics.Rectangle(220, 80, 140, 100);

// 3) Текст: x и y — позиция текста (обычно левый верхний угол TextBlock).
Graphics.Color = "Black";
Graphics.SetFont("Arial", 18);
Graphics.Text(60, 220, "Круг, прямоугольник\nи текст — уже программа!");
