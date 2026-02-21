using System;
using KID;

Console.WriteLine("Три способа задать цвет");

// 1) Название
Graphics.Color = "Coral";
Graphics.Circle(80, 80, 30);

// 2) Кортеж (R, G, B) — числа от 0 до 255
Graphics.Color = ((byte)255, (byte)165, (byte)0);   // оранжевый
Graphics.Circle(160, 80, 30);

Graphics.Color = ((byte)100, (byte)149, (byte)237);  // голубой
Graphics.Circle(240, 80, 30);

// 3) HEX: число 0xRRGGBB или строка "#RRGGBB"
Graphics.Color = 0x9370DB;   // фиолетовый
Graphics.Rectangle(50, 150, 80, 50);

Graphics.Color = "#20B2AA";  // морской волны
Graphics.Rectangle(150, 150, 80, 50);

Graphics.Color = "#FFD700";  // золотой
Graphics.Rectangle(250, 150, 80, 50);
