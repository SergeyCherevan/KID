using System;
using System.Globalization;
using KID;

// Шаблон безпечного введення: ReadInt / ReadDouble / ReadColor
// Ідея: повторювати запит, поки користувач не введе коректні дані.

int age = ReadInt("Введіть вік");
double price = ReadDouble("Введіть ціну (приклад: 12.5)");
string color = ReadColor("Введіть колір (ім'я, наприклад Red, або hex, наприклад #FF00AA)");

Console.WriteLine($"Готово! age={age}, price={price}, color={color}");

Graphics.Color = color;
Graphics.SetFont("Consolas", 18);
Graphics.Text(10, 10, $"Перегляд кольору: {color}");
Graphics.Circle(200, 140, 60);

// ---- helpers ----

static int ReadInt(string prompt)
{
    while (true)
    {
        Console.Write($"{prompt}: ");
        string s = (Console.ReadLine() ?? "").Trim();

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            return value;

        Console.WriteLine("Введіть ціле число (приклад: 42).");
    }
}

static double ReadDouble(string prompt)
{
    while (true)
    {
        Console.Write($"{prompt}: ");
        string s = (Console.ReadLine() ?? "").Trim();

        // Дозволимо кому, але парсимо інваріантно
        s = s.Replace(',', '.');

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return value;

        Console.WriteLine("Введіть число (приклад: 3.14).");
    }
}

static string ReadColor(string prompt)
{
    while (true)
    {
        Console.Write($"{prompt}: ");
        string s = (Console.ReadLine() ?? "").Trim();

        if (string.IsNullOrWhiteSpace(s))
        {
            Console.WriteLine("Колір не може бути порожнім.");
            continue;
        }

        // ColorType розуміє імена (\"Red\") та рядки, які підтримує BrushConverter (наприклад #RRGGBB).
        // Тут перевіримо найчастіші формати, а далі передамо рядок у Graphics.Color.
        if (LooksLikeColor(s))
            return s;

        Console.WriteLine("Невідомий формат кольору. Приклади: Red, Green, #FF00AA.");
    }
}

static bool LooksLikeColor(string s)
{
    // Ім'я
    if (!s.StartsWith("#") && s.IndexOf(' ') < 0 && s.Length <= 32)
        return true;

    // Hex
    if (s.StartsWith("#") && (s.Length == 7 || s.Length == 9))
        return true;

    return false;
}

