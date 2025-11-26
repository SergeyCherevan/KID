using System;
using System.Threading;
using KID;

Console.WriteLine("Please write word (without backspace):");

string word = "";
for (char c = '\0'; c != '\n';)
{
    CancellationManager.CheckCancellation();

    c = (char)Console.Read();
    word += c;
    
    Graphics.Clear();
	Graphics.SetFont("Arial", 100);
    Graphics.Text(0, 0, c.ToString());
}

Console.WriteLine($"Thank you! Your word is {word}");

Console.WriteLine("Please write name:");

string name = Console.ReadLine();

Console.WriteLine($"Thank you {name}!");
    
Thread.Sleep(5000);

Console.Clear();