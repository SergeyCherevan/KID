using System;
using KID;

Console.WriteLine("Hello World!");

Graphics.Color = (255, 0, 0);
Graphics.Circle(150, 150, 125);

Graphics.Color = 0x0000FF;
Graphics.Rectangle(150, 150, 100, 100);

Graphics.Color = "White";
Graphics.SetFont("Arial", 25);
Graphics.Text(150, 150, "Hello\nWorld!");