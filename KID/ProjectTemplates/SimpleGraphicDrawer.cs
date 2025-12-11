using System;
using System.Threading;
using KID;

for (int i = 1; true; i++)
{
	Console.WriteLine($"Figure {i}:");
	
	Console.Write($"type: "); string type = Console.ReadLine();
	
	Console.Write($"color: "); Graphics.Color = Console.ReadLine();
	
	switch (type)
	{
		case "Circle":
			{
				Console.Write($"x: "); int x = Int32.Parse(Console.ReadLine());
				Console.Write($"y: "); int y = Int32.Parse(Console.ReadLine());
				Console.Write($"radius: "); int radius = Int32.Parse(Console.ReadLine());
			
				Graphics.Circle(x, y, radius);
			}
			break;

		case "Ellipse":
			{
				Console.Write($"x: "); int x = Int32.Parse(Console.ReadLine());
				Console.Write($"y: "); int y = Int32.Parse(Console.ReadLine());
				Console.Write($"radiusX: "); int radiusX = Int32.Parse(Console.ReadLine());
				Console.Write($"radiusY: "); int radiusY = Int32.Parse(Console.ReadLine());
			
				Graphics.Ellipse(x, y, radiusX, radiusY);
			}
			break;
		
		case "Rect":
			{
				Console.Write($"x: "); int x = Int32.Parse(Console.ReadLine());
				Console.Write($"y: "); int y = Int32.Parse(Console.ReadLine());
				Console.Write($"width: "); int width = Int32.Parse(Console.ReadLine());
				Console.Write($"heigth: "); int heigth = Int32.Parse(Console.ReadLine());
			
				Graphics.Rectangle(x, y, width, heigth);
			}
			break;

		case "Polygon":
			{
				Console.Write($"count of points: "); int countOfPoints = Int32.Parse(Console.ReadLine());
				Console.Write($"points: ");
				(double x, double y)[] points = new (double x, double y)[countOfPoints];
				for (int i = 0; i < countOfPoints; i++)
				{
					Console.Write($"x{i}: "); points[i].x = Double.Parse(Console.ReadLine());
					Console.Write($"y{i}: "); points[i].y = Double.Parse(Console.ReadLine());
				}
			}
			break;

		case "Line":
			{
				Console.Write($"x1: "); int x1 = Int32.Parse(Console.ReadLine());
				Console.Write($"y1: "); int y1 = Int32.Parse(Console.ReadLine());
				Console.Write($"x2: "); int x2 = Int32.Parse(Console.ReadLine());
				Console.Write($"y2: "); int y2 = Int32.Parse(Console.ReadLine());
			
				Graphics.Line(x1, y1, x2, y2);
			}
			break;
	}

	Console.WriteLine("-----");
}