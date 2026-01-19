using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;
using Mouse = KID.Mouse;

// Шаблон ігрового циклу (Update/Draw/Sleep).
// Важливо: часто викликайте StopManager.StopIfButtonPressed(), щоб кнопка «Стоп» працювала.

int targetFps = 60;
int frameMs = (int)Math.Round(1000.0 / targetFps);

// Приклад стану (ваші змінні живуть тут)
double x = 100, y = 100;
double vx = 140, vy = 90; // пікселів за секунду
double radius = 18;

var lastTime = DateTime.UtcNow;

// --- UI (створюємо один раз, далі тільки оновлюємо) ---
Graphics.Color = "DodgerBlue";
var ball = Graphics.Circle(x, y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 16);
Graphics.Text(10, 10, "Шаблон ігрового циклу\nEsc = вихід\nShift + миша = телепорт");

while (true)
{
    StopManager.StopIfButtonPressed();

    // --- час ---
    var now = DateTime.UtcNow;
    double dt = (now - lastTime).TotalSeconds;
    lastTime = now;

    // Обмежимо dt (корисно після паузи в дебагері)
    if (dt > 0.1) dt = 0.1;

    // --- введення (приклади) ---
    // Клавіатура (polling)
    if (Keyboard.WasPressed(Key.Escape))
        break;

    // Миша (polling)
    var cursor = Mouse.CurrentCursor;
    if (cursor.Position.HasValue && Keyboard.IsDown(Key.LeftShift))
    {
        // Приклад: телепортуємо об'єкт до миші, поки затиснуто Shift
        x = cursor.Position.Value.X;
        y = cursor.Position.Value.Y;
    }

    // --- оновлення (логіка гри) ---
    // Розмір Canvas читаємо в UI-потоці
    (double w, double h) = DispatcherManager.InvokeOnUI(() =>
        (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));

    x += vx * dt;
    y += vy * dt;

    if (x - radius < 0) { x = radius; vx = -vx; }
    if (y - radius < 0) { y = radius; vy = -vy; }
    if (x + radius > w) { x = w - radius; vx = -vx; }
    if (y + radius > h) { y = h - radius; vy = -vy; }

    // --- малювання ---
    ball?.SetCenterXY(x, y);

    // --- затримка / частота кадрів ---
    Thread.Sleep(frameMs);
}

