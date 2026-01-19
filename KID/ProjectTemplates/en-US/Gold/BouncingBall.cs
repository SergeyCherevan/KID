using System;
using System.Threading;
using KID;

// Bouncing ball animation.
// What this teaches: coordinates, speed, collisions, simple game loop.

var rng = new Random();

double radius = 18;
double x = 120;
double y = 80;

// pixels per second
double vx = rng.Next(140, 260) * (rng.Next(0, 2) == 0 ? 1 : -1);
double vy = rng.Next(120, 220) * (rng.Next(0, 2) == 0 ? 1 : -1);

var last = DateTime.UtcNow;

while (true)
{
    StopManager.StopIfButtonPressed();

    var now = DateTime.UtcNow;
    double dt = (now - last).TotalSeconds;
    last = now;
    if (dt > 0.1) dt = 0.1;

    (double w, double h) = DispatcherManager.InvokeOnUI(() =>
        (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));

    x += vx * dt;
    y += vy * dt;

    // bounce
    if (x - radius < 0) { x = radius; vx = -vx; }
    if (y - radius < 0) { y = radius; vy = -vy; }
    if (x + radius > w) { x = w - radius; vx = -vx; }
    if (y + radius > h) { y = h - radius; vy = -vy; }

    Graphics.Clear();

    Graphics.Color = "Orange";
    Graphics.Circle(x, y, radius);

    Graphics.Color = "White";
    Graphics.SetFont("Consolas", 16);
    Graphics.Text(10, 10, "Bouncing ball\nPress Stop to exit");

    Thread.Sleep(16); // ~60 FPS
}

