using System;
using System.Windows;
using KID;

// Привітання в консолі
Console.WriteLine("Привіт, Світ!");

// Привітання у вікні графічного виводу
Graphics.Color = "Green";
Graphics.SetFont("Arial", 24);
Graphics.Text(0, 0, "Привіт, Світ!");

Console.WriteLine("- Як тебе звати?");
Console.Write($"- ");
string name = Console.ReadLine();
Console.WriteLine($"- Привіт, {name}!");

// Привітання у вікні графічного виводу по імені
Graphics.Color = 0xFF0000;
Graphics.Text(0, 25, $"Привіт, {name}!");

// Малюємо жовтий смайлик
// 1. Жовте обличчя (велике коло)
Graphics.Color = (255, 255, 0);
Graphics.Circle(150, 150, 100);

// 2. Ліве око (чорне коло)
Graphics.Color = 0x000000;
Graphics.Circle(110, 120, 12);

// 3. Праве око (чорне коло)
Graphics.Circle(190, 120, 12);

// 4. Червона усмішка (крива лінія)
Graphics.Color = "Red";
var smilePoints = new Point[]
{
    new Point(110, 180),  // Початок усмішки (зліва)
    new Point(150, 220),  // Контрольна точка (внизу, створює вигин)
    new Point(190, 180)   // Кінець усмішки (справа)
};
Graphics.QuadraticBezier(smilePoints);

// Привітальна мелодія: "Привіт, я запустився!" (до-мі-соль-мі)
Music.Sound(
    new SoundNote(262, 150),  // До
    new SoundNote(0, 30),     // Пауза
    new SoundNote(330, 150),  // Мі
    new SoundNote(0, 30),     // Пауза
    new SoundNote(392, 150),  // Соль
    new SoundNote(0, 30),     // Пауза
    new SoundNote(330, 250)   // Мі (довга)
);

