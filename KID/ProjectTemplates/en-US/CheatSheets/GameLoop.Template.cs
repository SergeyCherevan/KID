using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;
using Mouse = KID.Mouse;

// Game loop template (Update/Draw/Sleep).
// Tip: call StopManager.StopIfButtonPressed() often, so the Stop button works.

int targetFps = 60;
int frameMs = (int)Math.Round(1000.0 / targetFps);

// Example state (your variables live here)
double x = 100, y = 100;
double vx = 140, vy = 90; // pixels per second
double radius = 18;

var lastTime = DateTime.UtcNow;

// --- UI (create once, then update) ---
Graphics.Color = "DodgerBlue";
var ball = Graphics.Circle(x, y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 16);
Graphics.Text(10, 10, "Game loop template\nEsc = exit\nShift + mouse = teleport");

while (true)
{
    StopManager.StopIfButtonPressed();

    // --- timing ---
    var now = DateTime.UtcNow;
    double dt = (now - lastTime).TotalSeconds;
    lastTime = now;

    // Clamp dt (useful after a breakpoint)
    if (dt > 0.1) dt = 0.1;

    // --- input (examples) ---
    // Keyboard (polling)
    if (Keyboard.WasPressed(Key.Escape))
        break;

    // Mouse (polling)
    var cursor = Mouse.CurrentCursor;
    if (cursor.Position.HasValue && Keyboard.IsDown(Key.LeftShift))
    {
        // Example: teleport the object to the mouse when Shift is held
        x = cursor.Position.Value.X;
        y = cursor.Position.Value.Y;
    }

    // --- update (game logic) ---
    // Get canvas size (must be read on UI thread)
    (double w, double h) = DispatcherManager.InvokeOnUI(() =>
        (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));

    x += vx * dt;
    y += vy * dt;

    if (x - radius < 0) { x = radius; vx = -vx; }
    if (y - radius < 0) { y = radius; vy = -vy; }
    if (x + radius > w) { x = w - radius; vx = -vx; }
    if (y + radius > h) { y = h - radius; vy = -vy; }

    // --- draw ---
    ball?.SetCenterXY(x, y);

    // --- sleep / pacing ---
    Thread.Sleep(frameMs);
}

