using System;
using System.Windows;
using KID;

// Приветствие в консоли
Console.WriteLine("Привет, Мир!");

// Приветствие в окне графического вывода
Graphics.Color = "Green";
Graphics.SetFont("Arial", 24);
Graphics.Text(0, 0, "Привет, Мир!");

Console.WriteLine("- Как тебя зовут?");
Console.Write($"- ");
string name = Console.ReadLine();
Console.WriteLine($"- Привет, {name}!");

// Приветствие в окне графического вывода по имени
Graphics.Color = 0xFF0000;
Graphics.Text(0, 25, $"Привет, {name}!");

// Рисуем жёлтый смайлик
// 1. Жёлтое лицо (большой круг)
Graphics.Color = (255, 255, 0);
Graphics.Circle(150, 150, 100);

// 2. Левый глаз (чёрный кружок)
Graphics.Color = 0x000000;
Graphics.Circle(110, 120, 12);

// 3. Правый глаз (чёрный кружок)
Graphics.Circle(190, 120, 12);

// 4. Красная улыбка (кривая линия)
Graphics.Color = "Red";
var smilePoints = new Point[]
{
    new Point(110, 180),  // Начало улыбки (слева)
    new Point(150, 220),  // Контрольная точка (внизу, создаёт изгиб)
    new Point(190, 180)   // Конец улыбки (справа)
};
Graphics.QuadraticBezier(smilePoints);