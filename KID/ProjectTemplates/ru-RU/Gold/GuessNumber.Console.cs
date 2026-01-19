using System;

// «Угадай число» (консоль).
// Чему учит: цикл, if/else, счётчик попыток, случайные числа, проверка ввода.

var rng = new Random();
int secret = rng.Next(1, 101); // 1..100
int tries = 0;

Console.WriteLine("=== Угадай число ===");
Console.WriteLine("Я загадал число от 1 до 100.");
Console.WriteLine("Попробуй угадать!");
Console.WriteLine();

while (true)
{
    int guess = ReadInt($"Попытка #{tries + 1}. Твоё число");
    tries++;

    if (guess < secret)
    {
        Console.WriteLine("Моё число больше.");
    }
    else if (guess > secret)
    {
        Console.WriteLine("Моё число меньше.");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine($"Верно! Ты угадал за {tries} попыток.");
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

        Console.WriteLine("Введите целое число (пример: 42).");
    }
}

