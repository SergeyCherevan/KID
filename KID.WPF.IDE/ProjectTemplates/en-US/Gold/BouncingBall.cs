using System;
using System.Threading;
using KID;

// Bouncing ball animation.
// What this teaches: coordinates, speed, collisions, simple game loop.

var rng = new Random(DateTime.Now.Millisecond);

double radius = 18;
(double w, double h) = Graphics.GetCanvasSize();
double x = rng.Next(0, (int)w);
double y = rng.Next(0, (int)h);

// pixels per second
double vx = rng.Next(140, 260) * (rng.Next(0, 2) == 0 ? 1 : -1);
double vy = rng.Next(120, 220) * (rng.Next(0, 2) == 0 ? 1 : -1);

var last = DateTime.UtcNow;

// --- UI (create once, then update) ---
Graphics.Color = "Orange";
var ball = Graphics.Circle(x, y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 16);
Graphics.Text(10, 10, "Bouncing ball\nPress Stop to exit");

while (true)
{
    StopManager.StopIfButtonPressed();

    var now = DateTime.UtcNow;
    double dt = (now - last).TotalSeconds;
    last = now;
    if (dt > 0.1) dt = 0.1;

    (w, h) = Graphics.GetCanvasSize();

    x += vx * dt;
    y += vy * dt;

    // bounce
    if (x - radius < 0) { x = radius; vx = -vx; }
    if (y - radius < 0) { y = radius; vy = -vy; }
    if (x + radius > w) { x = w - radius; vx = -vx; }
    if (y + radius > h) { y = h - radius; vy = -vy; }

    ball?.SetCenterXY(x, y);

    Thread.Sleep(16); // ~60 FPS
}

