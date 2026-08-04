using System;
using KID;
using System.Windows;

Graphics.Color = "SaddleBrown";
Graphics.Rectangle(100, 180, 200, 120);

Graphics.Color = "DarkRed";
Graphics.Polygon(new Point[] {
    new Point(80, 180),
    new Point(200, 100),
    new Point(320, 180)
});

Graphics.Color = "White";
Graphics.Rectangle(160, 230, 40, 70);

Graphics.Color = "SkyBlue";
Graphics.Rectangle(220, 210, 50, 40);
