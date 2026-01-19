using System;

// «Вгадай число» (консоль).
// Чому навчає: цикл, if/else, лічильник спроб, випадкові числа, перевірка введення.

var rng = new Random();
int secret = rng.Next(1, 101); // 1..100
int tries = 0;

Console.WriteLine("=== Вгадай число ===");
Console.WriteLine("Я загадав число від 1 до 100.");
Console.WriteLine("Спробуй вгадати!");
Console.WriteLine();

while (true)
{
    int guess = ReadInt($"Спроба #{tries + 1}. Твоє число");
    tries++;

    if (guess < secret)
    {
        Console.WriteLine("Моє число більше.");
    }
    else if (guess > secret)
    {
        Console.WriteLine("Моє число менше.");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine($"Правильно! Ти вгадав за {tries} спроб.");
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

        Console.WriteLine("Введіть ціле число (приклад: 42).");
    }
}

