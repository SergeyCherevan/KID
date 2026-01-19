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

    Graphics.Clear();

    // ціль
    Graphics.Color = "Lime";
    Graphics.Circle(target.X, target.Y, radius);

    // HUD
    Graphics.Color = "White";
    Graphics.SetFont("Consolas", 18);
    Graphics.Text(10, 10, $"Рахунок: {score}");
    Graphics.Text(10, 34, $"Час: {Math.Ceiling(left)} с");
    Graphics.Text(10, 58, "Клікни по колу! Натисни «Стоп», щоб вийти");

    Thread.Sleep(16);
}

Graphics.Clear();
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

