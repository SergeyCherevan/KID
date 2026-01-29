using System;
using System.Windows;
using KID;

namespace HelloWorldKID
{
	public class Program
	{
		public static void Main()
		{
            // Greeting "Hello, World!"
            HelloWorld();

            // Ask the user for their name, save it in the name variable
            string name = AskName();

            // Greeting by name
            HelloName(name);

            // Draw a yellow smiley
            DrawSmile();

            // Play the welcome melody
            PlayWelcomeMelody();
		}

		// Greeting in the console and graphical output window
		public static void HelloWorld()
		{
			// Greeting in the console
			Console.WriteLine("Hello World!");

			// Greeting in the graphical output window
			Graphics.Color = "Green";
			Graphics.SetFont("Arial", 24);
			Graphics.Text(0, 0, "Hello World!");
		}

		// Ask the user for their name
		public static string AskName()
		{
            // Ask the user for their name in the console
            Console.WriteLine("- What is your name?");
			Console.Write($"- ");

			return Console.ReadLine() ?? "";
		}

		// Greeting in the console and graphical output window by name
		public static void HelloName(string name){
			// Greeting in the console by name
			Console.WriteLine($"- Hello {name}!");

			// Приветствие в окне графического вывода по имени
			Graphics.Color = 0xFF0000;
			Graphics.Text(0, 25, $"Hello {name}!");
		}

        // Draw a yellow smiley
		public static void DrawSmile()
		{
			// 1. Yellow face (large circle)
			Graphics.Color = (255, 255, 0);
			Graphics.Circle(150, 150, 100);

			// 2. Left eye (black circle)
			Graphics.Color = 0x000000;
			Graphics.Circle(110, 120, 12);

			// 3. Right eye (black circle)
			Graphics.Circle(190, 120, 12);

			// 4. Red smile (curved line)
			Graphics.Color = "Red";
			var smilePoints = new Point[]
			{
				new Point(110, 180),  // Start of the smile (left)
				new Point(150, 220),  // Control point (below, creates a bend)
				new Point(190, 180)   // End of the smile (right)
			};
			Graphics.QuadraticBezier(smilePoints);
		}

	public static void PlayWelcomeMelody()
	{
		// Welcome melody: "Hello, I started!" (do-mi-sol-mi)
		Music.Sound(
			new SoundNote(262, 150),  // Do
			new SoundNote(0, 30),     // Pause
			new SoundNote(330, 150),  // Mi
			new SoundNote(0, 30),     // Pause
			new SoundNote(392, 150),  // Sol
			new SoundNote(0, 30),     // Pause
			new SoundNote(330, 250)   // Mi (long)
		);
	}
	}
}