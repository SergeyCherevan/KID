using System;
using System.Threading;
using System.Windows;
using KID;
using Mouse = KID.Mouse;

// Міні-гра «спіймай коло».
// Правила: коло з'являється у випадковому місці. Клікни по ньому — отримаєш +1.
// Час: 30 секунд.

var rng = new Random();
double radius = 28;

int score = 0;
int durationSeconds = 30;

Point target = new Point(150, 150);
Respawn();

var start = DateTime.UtcNow;

// --- UI (створюємо один раз, далі тільки оновлюємо) ---
Graphics.Color = "Lime";
var targetCircle = Graphics.Circle(target.X, target.Y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 18);
var scoreText = Graphics.Text(10, 10, $"Рахунок: {score}");
var timeText = Graphics.Text(10, 34, $"Час: {durationSeconds} с");
var hintText = Graphics.Text(10, 58, "Клікни по колу! Натисни «Стоп», щоб вийти");

while (true)
{
    StopManager.StopIfButtonPressed();

    double elapsed = (DateTime.UtcNow - start).TotalSeconds;
    double left = durationSeconds - elapsed;
    if (left <= 0)
        break;

    // Обробляємо клік (Mouse.CurrentClick — короткий «пульс»)
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
    scoreText?.SetText($"Рахунок: {score}");
    timeText?.SetText($"Час: {Math.Ceiling(left)} с");

    Thread.Sleep(16);
}

targetCircle?.RemoveFromCanvas();
scoreText?.RemoveFromCanvas();
timeText?.RemoveFromCanvas();
hintText?.RemoveFromCanvas();

Graphics.Color = "White";
Graphics.SetFont("Consolas", 24);
Graphics.Text(10, 10, $"Час вийшов! Підсумковий рахунок: {score}");

void Respawn()
{
    (double w, double h) = DispatcherManager.InvokeOnUI(() =>
        (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));

    // Запасні значення, якщо вікно замале / ще не виміряне
    if (w < 200) w = 400;
    if (h < 200) h = 300;

    double x = radius + rng.NextDouble() * Math.Max(1, (w - 2 * radius));
    double y = radius + rng.NextDouble() * Math.Max(1, (h - 2 * radius));
    target = new Point(x, y);
}

