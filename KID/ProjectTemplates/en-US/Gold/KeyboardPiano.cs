using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;

// Keyboard piano.
// Press keys A S D F G H J K to play notes.
// Tip: Music.Sound is blocking, so we play it in a background task.

var keys = new (Key key, string name, double freq)[]
{
    (Key.A, "C4", 261.63),
    (Key.S, "D4", 293.66),
    (Key.D, "E4", 329.63),
    (Key.F, "F4", 349.23),
    (Key.G, "G4", 392.00),
    (Key.H, "A4", 440.00),
    (Key.J, "B4", 493.88),
    (Key.K, "C5", 523.25),
};

string last = "-";

// --- UI (create once, then update) ---
Graphics.Color = "White";
Graphics.SetFont("Consolas", 18);

Graphics.Text(10, 10, "Keyboard piano");
Graphics.Text(10, 34, "A S D F G H J K = notes");
Graphics.Text(10, 58, "Esc = exit, Stop = stop program");
var lastText = Graphics.Text(10, 92, $"Last: {last}");

while (true)
{
    StopManager.StopIfButtonPressed();

    if (Keyboard.WasPressed(Key.Escape))
        break;

    for (int i = 0; i < keys.Length; i++)
    {
        var k = keys[i];
        if (Keyboard.WasPressed(k.key))
        {
            last = $"{k.key} → {k.name} ({k.freq:0.##} Hz)";
            double f = k.freq;
            _ = Task.Run(() => Music.Sound(f, 120));
        }
    }

    lastText?.SetText($"Last: {last}");

    Thread.Sleep(16);
}

