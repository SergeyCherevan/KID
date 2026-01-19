using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;

// «Пианино на клавишах».
// Нажимай A S D F G H J K — будут звучать ноты.
// Совет: Music.Sound блокирует поток, поэтому запускаем звук в фоне.

var keys = new (Key key, string name, double freq)[]
{
    (Key.A, "До (C4)", 261.63),
    (Key.S, "Ре (D4)", 293.66),
    (Key.D, "Ми (E4)", 329.63),
    (Key.F, "Фа (F4)", 349.23),
    (Key.G, "Соль (G4)", 392.00),
    (Key.H, "Ля (A4)", 440.00),
    (Key.J, "Си (B4)", 493.88),
    (Key.K, "До (C5)", 523.25),
};

string last = "-";

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

    Graphics.Clear();
    Graphics.Color = "White";
    Graphics.SetFont("Consolas", 18);

    Graphics.Text(10, 10, "Пианино на клавишах");
    Graphics.Text(10, 34, "A S D F G H J K = ноты");
    Graphics.Text(10, 58, "Esc = выход, «Стоп» = остановить программу");
    Graphics.Text(10, 92, $"Последняя: {last}");

    Thread.Sleep(16);
}

