using System;
using System.Threading;
using KID;

for (int i = 0; i < 1000000; i++)
{
    CancellationManager.CheckCancellation();

    Console.WriteLine($"Обработка {i}");
    
    KID.Graphics.SetColor("White");
	KID.Graphics.SetFont("Arial", 25);
	KID.Graphics.Text(0, 0 + i * 20, $"Обработка {i}");
    
    Thread.Sleep(100);
}