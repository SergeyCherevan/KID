using System;
using KID;

Console.WriteLine("Hello World!");

Graphics.Color = "Red";
Graphics.Circle(150, 150, 125);

Graphics.Color = "Blue";
Graphics.Rectangle(150, 150, 100, 100);

Graphics.Color = "White";
Graphics.SetFont("Arial", 25);
Graphics.Text(150, 150, "Hello\nWorld!");

