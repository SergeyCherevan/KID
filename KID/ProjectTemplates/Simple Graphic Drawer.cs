using System;
using System.Threading;
using KID;

int x, y, radius, width, heigth;
string type;

for (int i = 1; true; i++)
{
	Console.WriteLine($"Figure {i}:");
	
	Console.Write($"color: "); KID.Graphics.Color = Console.ReadLine();
	
	Console.Write($"type: "); type = Console.ReadLine();
	
	switch (type)
	{
		case "Circle":
			Console.Write($"x: "); x = Int32.Parse(Console.ReadLine());
			Console.Write($"y: "); y = Int32.Parse(Console.ReadLine());
			Console.Write($"radius: "); radius = Int32.Parse(Console.ReadLine());
		
			KID.Graphics.Circle(x, y, radius);
			break;
		
		case "Rect":
			Console.Write($"x: "); x = Int32.Parse(Console.ReadLine());
			Console.Write($"y: "); y = Int32.Parse(Console.ReadLine());
			Console.Write($"width: "); width = Int32.Parse(Console.ReadLine());
			Console.Write($"heigth: "); heigth = Int32.Parse(Console.ReadLine());
		
			KID.Graphics.Rectangle(x, y, width, heigth);
			break;
	}

	Console.WriteLine("-----");
}