using System;
using System.Threading;
using KID;

Console.WriteLine("Please write word:");

for (char c = '\0'; c != '\n';)
{
    CancellationManager.CheckCancellation();

    c = (char)Console.Read();
}

Console.WriteLine("Thank you!");

Console.WriteLine("Please write name:");

string name = Console.ReadLine();

Console.WriteLine($"Thank you {name}!");