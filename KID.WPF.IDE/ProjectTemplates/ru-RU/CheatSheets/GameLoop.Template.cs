using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;
using Mouse = KID.Mouse;

// Шаблон игрового цикла (Update/Draw/Sleep).
// Важно: часто вызывайте StopManager.StopIfButtonPressed(), чтобы кнопка «Стоп» работала.

int targetFps = 60;
int frameMs = (int)Math.Round(1000.0 / targetFps);

// Пример состояния (ваши переменные живут здесь)
double x = 100, y = 100;
double vx = 140, vy = 90; // пикселей в секунду
double radius = 18;

var lastTime = DateTime.UtcNow;

// --- UI (создаём один раз, дальше только обновляем) ---
Graphics.Color = "DodgerBlue";
var ball = Graphics.Circle(x, y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 16);
Graphics.Text(10, 10, "Шаблон игрового цикла\nEsc = выход\nShift + мышь = телепорт");

while (true)
{
    StopManager.StopIfButtonPressed();

    // --- время ---
    var now = DateTime.UtcNow;
    double dt = (now - lastTime).TotalSeconds;
    lastTime = now;

    // Ограничим dt (полезно после остановки в дебаггере)
    if (dt > 0.1) dt = 0.1;

    // --- ввод (примеры) ---
    // Клавиатура (polling)
    if (Keyboard.WasPressed(Key.Escape))
        break;

    // Мышь (polling)
    var cursor = Mouse.CurrentCursor;
    if (cursor.Position.HasValue && Keyboard.IsDown(Key.LeftShift))
    {
        // Пример: телепортируем объект к мыши, пока зажат Shift
        x = cursor.Position.Value.X;
        y = cursor.Position.Value.Y;
    }

    // --- обновление (логика игры) ---
    // Размер Canvas читаем в UI-потоке
    (double w, double h) = Graphics.GetCanvasSize();

    x += vx * dt;
    y += vy * dt;

    if (x - radius < 0) { x = radius; vx = -vx; }
    if (y - radius < 0) { y = radius; vy = -vy; }
    if (x + radius > w) { x = w - radius; vx = -vx; }
    if (y + radius > h) { y = h - radius; vy = -vy; }

    // --- рисование ---
    ball?.SetCenterXY(x, y);

    // --- задержка / частота кадров ---
    Thread.Sleep(frameMs);
}

