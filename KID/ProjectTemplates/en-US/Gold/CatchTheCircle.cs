using System;
using System.Threading;
using System.Windows;
using KID;
using Mouse = KID.Mouse;

// Mini game: Catch the Circle!
// Rules: a circle appears at random positions. Click it to get +1 point.
// Time limit: 30 seconds.

var rng = new Random();
double radius = 28;

int score = 0;
int durationSeconds = 30;

Point target = new Point(150, 150);
Respawn();

var start = DateTime.UtcNow;

// --- UI (create once, then update) ---
Graphics.Color = "Lime";
var targetCircle = Graphics.Circle(target.X, target.Y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 18);
var scoreText = Graphics.Text(10, 10, $"Score: {score}");
var timeText = Graphics.Text(10, 34, $"Time: {durationSeconds} s");
var hintText = Graphics.Text(10, 58, "Click the circle! Press Stop to exit");

while (true)
{
    StopManager.StopIfButtonPressed();

    double elapsed = (DateTime.UtcNow - start).TotalSeconds;
    double left = durationSeconds - elapsed;
    if (left <= 0)
        break;

    // Handle click (Mouse.CurrentClick is a short \"pulse\")
    var click = Mouse.CurrentClick;
    if (click.Position.HasValue &&
        (click.Status == ClickStatus.OneLeftClick || click.Status == ClickStatus.DoubleLeftClick))
    {
        var p = click.Position.Value;
        double dx = p.X - target.X;
        double dy = p.Y - target.Y;
        if (dx * dx + dy * dy <= radius * radius)
        {
            score++;
            Respawn();
        }
    }

    targetCircle?.SetCenterXY(target.X, target.Y);
    scoreText?.SetText($"Score: {score}");
    timeText?.SetText($"Time: {Math.Ceiling(left)} s");

    Thread.Sleep(16);
}

targetCircle?.RemoveFromCanvas();
scoreText?.RemoveFromCanvas();
timeText?.RemoveFromCanvas();
hintText?.RemoveFromCanvas();

Graphics.Color = "White";
Graphics.SetFont("Consolas", 24);
Graphics.Text(10, 10, $"Time is up! Final score: {score}");

void Respawn()
{
    (double w, double h) = DispatcherManager.InvokeOnUI(() =>
        (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));

    // Safe defaults if the window is too small / not measured yet
    if (w < 200) w = 400;
    if (h < 200) h = 300;

    double x = radius + rng.NextDouble() * Math.Max(1, (w - 2 * radius));
    double y = radius + rng.NextDouble() * Math.Max(1, (h - 2 * radius));
    target = new Point(x, y);
}

