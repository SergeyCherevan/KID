using System;
using System.Threading;
using KID;

// Тестовый скрипт для проверки Keyboard API
// Показывает:
// - polling (IsDown/WasPressed)
// - текстовый ввод (ReadText)
// - хоткей (Ctrl+R)

double x = 150;
double y = 150;
string text = "Type here (Console) \n or press keys!";

// Ctrl+R — сброс
Keyboard.RegisterShortcut(new Shortcut(new KeyChord(KeyModifiers.Ctrl, KeyboardKeys.R), name: "Reset"));
Keyboard.ShortcutEvent += s =>
{
    if (s.Shortcut?.Name == "Reset")
    {
        x = 150;
        y = 150;
        text = "";
        Graphics.Clear();
        Console.WriteLine("Reset!");
    }
};

while (true)
{
    StopManager.StopIfButtonPressed();

    // Движение: стрелки или WASD
    if (Keyboard.IsDown(KeyboardKeys.Left) || Keyboard.IsDown(KeyboardKeys.A)) x -= 5;
    if (Keyboard.IsDown(KeyboardKeys.Right) || Keyboard.IsDown(KeyboardKeys.D)) x += 5;
    if (Keyboard.IsDown(KeyboardKeys.Up) || Keyboard.IsDown(KeyboardKeys.W)) y -= 5;
    if (Keyboard.IsDown(KeyboardKeys.Down) || Keyboard.IsDown(KeyboardKeys.S)) y += 5;

    // «Нажали один раз»
    if (Keyboard.WasPressed(KeyboardKeys.Space))
        Console.WriteLine("Space pressed!");

    // Текстовый ввод (буфер)
    var newText = Keyboard.ReadText();
    if (!string.IsNullOrEmpty(newText))
        text += newText;

    // Отрисовка
    Graphics.Clear();
    Graphics.Color = "Blue";
    Graphics.Circle(x, y, 20);

    Graphics.Color = "Black";
    Graphics.SetFont("Arial", 18);
    Graphics.Text(10, 10, $"Down keys: {Keyboard.CurrentState.DownKeys.Length}");
    Graphics.Text(10, 35, $"Modifiers: {Keyboard.CurrentState.Modifiers}");
    Graphics.Text(10, 60, $"Text: {text}");

    Thread.Sleep(16);
}

          