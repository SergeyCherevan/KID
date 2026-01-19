using System;

// Guess the number (console).
// What this teaches: loop, if/else, counters, random numbers, input validation.

var rng = new Random();
int secret = rng.Next(1, 101); // 1..100
int tries = 0;

Console.WriteLine("=== Guess the number ===");
Console.WriteLine("I picked a number from 1 to 100.");
Console.WriteLine("Try to guess it!");
Console.WriteLine();

while (true)
{
    int guess = ReadInt($"Attempt #{tries + 1}. Your guess");
    tries++;

    if (guess < secret)
    {
        Console.WriteLine("My number is bigger.");
    }
    else if (guess > secret)
    {
        Console.WriteLine("My number is smaller.");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine($"Correct! You guessed it in {tries} tries.");
        break;
    }
}

static int ReadInt(string prompt)
{
    while (true)
    {
        Console.Write($"{prompt}: ");
        string s = (Console.ReadLine() ?? "").Trim();

        if (int.TryParse(s, out int value))
            return value;

        Console.WriteLine("Please enter an integer number (example: 42).");
    }
}

