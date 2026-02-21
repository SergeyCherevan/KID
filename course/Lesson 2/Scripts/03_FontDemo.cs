using System;
using KID;

Console.WriteLine("Разные шрифты и размеры");

Graphics.Color = "Black";

Graphics.SetFont("Arial", 16);
Graphics.Text(20, 30, "Маленький Arial 16");

Graphics.SetFont("Arial", 28);
Graphics.Text(20, 70, "Крупный Arial 28");

Graphics.SetFont("Times New Roman", 22);
Graphics.Text(20, 120, "Times New Roman 22");

Graphics.SetFont("Segoe UI", 24);
Graphics.Text(20, 170, "Segoe UI — привет!");
