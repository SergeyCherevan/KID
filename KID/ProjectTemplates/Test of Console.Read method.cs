using System;
using System.Threading;
using KID;

Console.WriteLine("Please write:");

for (char c = '\0'; c != '\n';)
{
    CancellationManager.CheckCancellation();

    c = (char)Console.Read();
}

Console.WriteLine("Thank you!");