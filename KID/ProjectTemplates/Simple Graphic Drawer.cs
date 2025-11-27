using System;
using System.Threading;
using KID;

int x, y, x2, y2, radius, width, heigth;
string type;

for (int i = 1; true; i++)
{
	Console.WriteLine($"Figure {i}:");
	
	Console.Write($"type: "); type = Console.ReadLine();
	
	Console.Write($"color: "); KID.Graphics.Color = Console.ReadLine();
	
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

		case "Line":
			Console.Write($"x1: "); x = Int32.Parse(Console.ReadLine());
			Console.Write($"y1: "); y = Int32.Parse(Console.ReadLine());
			Console.Write($"x2: "); x2 = Int32.Parse(Console.ReadLine());
			Console.Write($"y2: "); y2 = Int32.Parse(Console.ReadLine());
		
			KID.Graphics.Line(x, y, x2, y2);
			break;
	}

	Console.WriteLine("-----");
}