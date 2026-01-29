using System;
using System.Globalization;
using KID;

// Шаблон безопасного ввода: ReadInt / ReadDouble / ReadColor
// Идея: повторять вопрос, пока пользователь не введёт корректные данные.

int age = ReadInt("Введите возраст");
double price = ReadDouble("Введите цену (пример: 12.5)");
string color = ReadColor("Введите цвет (имя, например Red, или hex, например #FF00AA)");

Console.WriteLine($"Готово! age={age}, price={price}, color={color}");

Graphics.Color = color;
Graphics.SetFont("Consolas", 18);
Graphics.Text(10, 10, $"Превью цвета: {color}");
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

        Console.WriteLine("Введите целое число (пример: 42).");
    }
}

static double ReadDouble(string prompt)
{
    while (true)
    {
        Console.Write($"{prompt}: ");
        string s = (Console.ReadLine() ?? "").Trim();

        // Разрешим запятую, но парсим инвариантно
        s = s.Replace(',', '.');

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return value;

        Console.WriteLine("Введите число (пример: 3.14).");
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
            Console.WriteLine("Цвет не может быть пустым.");
            continue;
        }

        // ColorType понимает имена (\"Red\") и строки, которые поддерживает BrushConverter (например #RRGGBB).
        // Здесь мы просто проверим самый частый формат, а дальше передадим строку в Graphics.Color.
        if (LooksLikeColor(s))
            return s;

        Console.WriteLine("Неизвестный формат цвета. Примеры: Red, Green, #FF00AA.");
    }
}

static bool LooksLikeColor(string s)
{
    // Имя
    if (!s.StartsWith("#") && s.IndexOf(' ') < 0 && s.Length <= 32)
        return true;

    // Hex
    if (s.StartsWith("#") && (s.Length == 7 || s.Length == 9))
        return true;

    return false;
}

