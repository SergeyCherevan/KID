using System;
using System.Globalization;
using KID;

// Safe input template: ReadInt / ReadDouble / ReadColor
// Idea: keep asking until the user enters correct data.

int age = ReadInt("Enter your age");
double price = ReadDouble("Enter the price (example: 12.5)");
string color = ReadColor("Enter a color (name like Red, or hex like #FF00AA)");

Console.WriteLine($"OK! age={age}, price={price}, color={color}");

Graphics.Color = color;
Graphics.SetFont("Consolas", 18);
Graphics.Text(10, 10, $"Color preview: {color}");
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

        Console.WriteLine("Please enter an integer number (example: 42).");
    }
}

static double ReadDouble(string prompt)
{
    while (true)
    {
        Console.Write($"{prompt}: ");
        string s = (Console.ReadLine() ?? "").Trim();

        // Allow both comma and dot from users, but parse invariantly
        s = s.Replace(',', '.');

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return value;

        Console.WriteLine("Please enter a number (example: 3.14).");
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
            Console.WriteLine("Color cannot be empty.");
            continue;
        }

        // ColorType supports names like \"Red\" and hex-like strings supported by BrushConverter.
        // We'll just try to use it; if conversion fails, KID will fallback to black.
        // To detect failure, we do a small trick: accept common formats and names.
        if (LooksLikeColor(s))
            return s;

        Console.WriteLine("Unknown color format. Examples: Red, Green, #FF00AA.");
    }
}

static bool LooksLikeColor(string s)
{
    // Name
    if (!s.StartsWith("#") && s.IndexOf(' ') < 0 && s.Length <= 32)
        return true;

    // Hex
    if (s.StartsWith("#") && (s.Length == 7 || s.Length == 9))
        return true;

    return false;
}

