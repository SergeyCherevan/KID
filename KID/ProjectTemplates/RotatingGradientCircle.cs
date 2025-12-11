using System;
using System.Threading;
using KID;

for (int i = 0; i < 1000000; i++)
{
    StopManager.StopIfButtonPressed();

    Console.WriteLine($"Обработка {i}");
    
    Graphics.Color = (
        (byte)((Math.Sin(2*Math.PI * i/0x100 / 3 + 0 * 2*Math.PI/3) + 1) * 0xFF/2),
        (byte)((Math.Sin(2*Math.PI * i/0x100 / 3 + 1 * 2*Math.PI/3) + 1) * 0xFF/2),
        (byte)((Math.Sin(2*Math.PI * i/0x100 / 3 + 2 * 2*Math.PI/3) + 1) * 0xFF/2)
    );
    
	Graphics.Circle(
        150 + Math.Sin(2*Math.PI / 3 * i/0x100) * 100,
        150 + Math.Cos(2*Math.PI / 3 * i/0x100) * 100,
        10
    );
    
    Thread.Sleep(50);
}