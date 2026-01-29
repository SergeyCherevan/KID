using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;

// «Піаніно на клавішах».
// Натискай A W S E D F T G Y H U J K — звучатимуть ноти.
// Порада: Music.Sound блокує потік, тому запускаємо звук у фоні.

var keys = new (Key key, string name, double freq)[]
{
    (Key.A, "До (C4)", 261.63),
    (Key.W, "До# / Ре♭ (C#4/Db4)", 277.18),
    (Key.S, "Ре (D4)", 293.66),
    (Key.E, "Ре# / Мі♭ (D#4/Eb4)", 311.13),
    (Key.D, "Мі (E4)", 329.63),
    (Key.F, "Фа (F4)", 349.23),
    (Key.T, "Фа# / Соль♭ (F#4/Gb4)", 369.99),
    (Key.G, "Соль (G4)", 392.00),
    (Key.Y, "Соль# / Ля♭ (G#4/Ab4)", 415.30),
    (Key.H, "Ля (A4)", 440.00),
    (Key.U, "Ля# / Сі♭ (A#4/Bb4)", 466.16),
    (Key.J, "Сі (B4)", 493.88),
    (Key.K, "До (C5)", 523.25),
};

string last = "-";

// --- UI (створюємо один раз, далі тільки оновлюємо) ---
Graphics.Color = "White";
Graphics.SetFont("Consolas", 18);

Graphics.Text(10, 10, "Піаніно на клавішах");
Graphics.Text(10, 34, "A W S E D F T G Y H U J K = ноти");
Graphics.Text(10, 58, "Esc = вихід, «Стоп» = зупинити програму");
var lastText = Graphics.Text(10, 92, $"Остання: {last}");

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
            last = $"{k.key} → {k.name} ({k.freq:0.##} Гц)";
            double f = k.freq;
            _ = Task.Run(() => Music.Sound(f, 120));
        }
    }

    lastText?.SetText($"Остання: {last}");

    Thread.Sleep(16);
}

