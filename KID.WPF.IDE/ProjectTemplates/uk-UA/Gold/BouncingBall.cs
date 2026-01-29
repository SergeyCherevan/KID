using System;
using System.Threading;
using KID;

// Анімація «кулька стрибає та відскакує від стін».
// Чому навчає: координати, швидкість, зіткнення, простий ігровий цикл.

var rng = new Random();

double radius = 18;
double x = 120;
double y = 80;

// пікселів за секунду
double vx = rng.Next(140, 260) * (rng.Next(0, 2) == 0 ? 1 : -1);
double vy = rng.Next(120, 220) * (rng.Next(0, 2) == 0 ? 1 : -1);

var last = DateTime.UtcNow;

// --- UI (створюємо один раз, далі тільки оновлюємо) ---
Graphics.Color = "Orange";
var ball = Graphics.Circle(x, y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 16);
Graphics.Text(10, 10, "Кулька відскакує\nНатисни «Стоп», щоб вийти");

while (true)
{
    StopManager.StopIfButtonPressed();

    var now = DateTime.UtcNow;
    double dt = (now - last).TotalSeconds;
    last = now;
    if (dt > 0.1) dt = 0.1;

    (double w, double h) = Graphics.GetCanvasSize();

    x += vx * dt;
    y += vy * dt;

    // відскок
    if (x - radius < 0) { x = radius; vx = -vx; }
    if (y - radius < 0) { y = radius; vy = -vy; }
    if (x + radius > w) { x = w - radius; vx = -vx; }
    if (y + radius > h) { y = h - radius; vy = -vy; }

    ball?.SetCenterXY(x, y);

    Thread.Sleep(16); // ~60 FPS
}

