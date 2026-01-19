using System;
using System.Threading;
using System.Windows;
using KID;
using Mouse = KID.Mouse;

// Мини-игра «поймай круг».
// Правила: круг появляется в случайном месте. Кликни по нему — получишь +1.
// Время: 30 секунд.

var rng = new Random();
double radius = 28;

int score = 0;
int durationSeconds = 30;

Point target = new Point(150, 150);
Respawn();

var start = DateTime.UtcNow;

// --- UI (создаём один раз, дальше только обновляем) ---
Graphics.Color = "Lime";
var targetCircle = Graphics.Circle(target.X, target.Y, radius);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 18);
var scoreText = Graphics.Text(10, 10, $"Счёт: {score}");
var timeText = Graphics.Text(10, 34, $"Время: {durationSeconds} с");
var hintText = Graphics.Text(10, 58, "Кликни по кругу! Нажми «Стоп», чтобы выйти");

while (true)
{
    StopManager.StopIfButtonPressed();

    double elapsed = (DateTime.UtcNow - start).TotalSeconds;
    double left = durationSeconds - elapsed;
    if (left <= 0)
        break;

    // Обрабатываем клик (Mouse.CurrentClick — короткий «пульс»)
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
    scoreText?.SetText($"Счёт: {score}");
    timeText?.SetText($"Время: {Math.Ceiling(left)} с");

    Thread.Sleep(16);
}

targetCircle?.RemoveFromCanvas();
scoreText?.RemoveFromCanvas();
timeText?.RemoveFromCanvas();
hintText?.RemoveFromCanvas();

Graphics.Color = "White";
Graphics.SetFont("Consolas", 24);
Graphics.Text(10, 10, $"Время вышло! Итоговый счёт: {score}");

void Respawn()
{
    (double w, double h) = DispatcherManager.InvokeOnUI(() =>
        (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));

    // Запасные значения, если окно слишком маленькое / ещё не измерено
    if (w < 200) w = 400;
    if (h < 200) h = 300;

    double x = radius + rng.NextDouble() * Math.Max(1, (w - 2 * radius));
    double y = radius + rng.NextDouble() * Math.Max(1, (h - 2 * radius));
    target = new Point(x, y);
}

