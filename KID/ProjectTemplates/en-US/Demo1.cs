using System;
using System.Windows;
using KID;

// Greeting in the console
Console.WriteLine("Hello World!");

// Greeting in the graphical output window
Graphics.Color = "Green";
Graphics.SetFont("Arial", 24);
Graphics.Text(0, 0, "Hello World!");

Console.WriteLine("- What is your name?");
Console.Write($"- ");
string name = Console.ReadLine();
Console.WriteLine($"- Hello {name}!");

// Greeting in the graphical output window by name
Graphics.Color = 0xFF0000;
Graphics.Text(0, 25, $"Hello {name}!");

// Draw a yellow smiley
// 1. Yellow face (large circle)
Graphics.Color = (255, 255, 0);
Graphics.Circle(150, 150, 100);

// 2. Left eye (black circle)
Graphics.Color = 0x000000;
Graphics.Circle(110, 120, 12);

// 3. Right eye (black circle)
Graphics.Circle(190, 120, 12);

// 4. Red smile (curved line)
Graphics.Color = "Red";
var smilePoints = new Point[]
{
    new Point(110, 180),  // Start of the smile (left)
    new Point(150, 220),  // Control point (below, creates a bend)
    new Point(190, 180)   // End of the smile (right)
};
Graphics.QuadraticBezier(smilePoints);

// Welcome melody: "Hello, I started!" (do-mi-sol-mi)
Music.Sound(
    new SoundNote(262, 150),  // Do
    new SoundNote(0, 30),     // Pause
    new SoundNote(330, 150),  // Mi
    new SoundNote(0, 30),     // Pause
    new SoundNote(392, 150),  // Sol
    new SoundNote(0, 30),     // Pause
    new SoundNote(330, 250)   // Mi (long)
);