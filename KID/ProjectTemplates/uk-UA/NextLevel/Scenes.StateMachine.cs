using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;
using Mouse = KID.Mouse;

// Приклад «Сцени / меню».
// Мета: показати, як організувати код через «сцени»:
// Стартовий екран -> Гра -> Екран результату.

var ctx = new GameContext();
IScene scene = new StartScene();

var last = DateTime.UtcNow;

while (true)
{
    StopManager.StopIfButtonPressed();

    if (Keyboard.WasPressed(Key.Escape))
        break;

    var now = DateTime.UtcNow;
    double dt = (now - last).TotalSeconds;
    last = now;
    if (dt > 0.1) dt = 0.1;

    var next = scene.Update(ctx, dt);
    if (next != null)
    {
        scene = next;
        // Сцену змінено -> очищаємо Canvas один раз, далі оновлюємо UI-елементи.
        Graphics.Clear();
    }

    scene.Draw(ctx);
    Thread.Sleep(16);
}

// ---- спільний стан ----

class GameContext
{
    public int Score { get; set; }
    public int BestScore { get; set; }
    public DateTime GameStartUtc { get; set; }
}

interface IScene
{
    IScene? Update(GameContext ctx, double dt);
    void Draw(GameContext ctx);
}

// ---- сцени ----

class StartScene : IScene
{
    private System.Windows.Controls.TextBlock? _bestText;
    private bool _uiReady;

    public IScene? Update(GameContext ctx, double dt)
    {
        if (Keyboard.WasPressed(Key.Enter))
        {
            ctx.Score = 0;
            ctx.GameStartUtc = DateTime.UtcNow;
            return new PlayScene();
        }

        return null;
    }

    public void Draw(GameContext ctx)
    {
        EnsureUi();
        _bestText?.SetText(ctx.BestScore > 0 ? $"Найкращий: {ctx.BestScore}" : "");
    }

    private void EnsureUi()
    {
        if (_uiReady) return;

        Graphics.Color = "White";
        Graphics.SetFont("Consolas", 20);

        Graphics.Text(10, 10, "Приклад «Сцени»");
        Graphics.Text(10, 40, "Enter = почати");
        Graphics.Text(10, 70, "Esc = вихід");
        _bestText = Graphics.Text(10, 110, "");

        _uiReady = true;
    }
}

class PlayScene : IScene
{
    private readonly Random _rng = new Random();
    private Point _target = new Point(150, 150);
    private double _r = 26;
    private const int DurationSeconds = 15;
    private FrameworkElement? _targetCircle;
    private System.Windows.Controls.TextBlock? _scoreText;
    private System.Windows.Controls.TextBlock? _timeText;
    private bool _uiReady;

    public PlayScene()
    {
        Respawn();
    }

    public IScene? Update(GameContext ctx, double dt)
    {
        double elapsed = (DateTime.UtcNow - ctx.GameStartUtc).TotalSeconds;
        double left = DurationSeconds - elapsed;
        if (left <= 0)
            return new ResultScene();

        var click = Mouse.CurrentClick;
        if (click.Position.HasValue &&
            (click.Status == ClickStatus.OneLeftClick || click.Status == ClickStatus.DoubleLeftClick))
        {
            var p = click.Position.Value;
            double dx = p.X - _target.X;
            double dy = p.Y - _target.Y;
            if (dx * dx + dy * dy <= _r * _r)
            {
                ctx.Score++;
                Respawn();
            }
        }

        return null;
    }

    public void Draw(GameContext ctx)
    {
        EnsureUi();

        double elapsed = (DateTime.UtcNow - ctx.GameStartUtc).TotalSeconds;
        double left = DurationSeconds - elapsed;
        _targetCircle?.SetCenterXY(_target.X, _target.Y);
        _scoreText?.SetText($"Рахунок: {ctx.Score}");
        _timeText?.SetText($"Час: {Math.Ceiling(Math.Max(0, left))} с");
    }

    private void EnsureUi()
    {
        if (_uiReady) return;

        Graphics.Color = "Lime";
        _targetCircle = Graphics.Circle(_target.X, _target.Y, _r);

        Graphics.Color = "White";
        Graphics.SetFont("Consolas", 18);
        _scoreText = Graphics.Text(10, 10, "Рахунок: 0");
        _timeText = Graphics.Text(10, 34, $"Час: {DurationSeconds} с");
        Graphics.Text(10, 58, "Клікни по колу!");

        _uiReady = true;
    }

    private void Respawn()
    {
        (double w, double h) = Graphics.GetCanvasSize();

        if (w < 200) w = 400;
        if (h < 200) h = 300;

        double x = _r + _rng.NextDouble() * Math.Max(1, (w - 2 * _r));
        double y = _r + _rng.NextDouble() * Math.Max(1, (h - 2 * _r));
        _target = new Point(x, y);
    }
}

class ResultScene : IScene
{
    private System.Windows.Controls.TextBlock? _scoreText;
    private System.Windows.Controls.TextBlock? _bestText;
    private bool _uiReady;

    public IScene? Update(GameContext ctx, double dt)
    {
        if (ctx.Score > ctx.BestScore)
            ctx.BestScore = ctx.Score;

        if (Keyboard.WasPressed(Key.Enter))
            return new StartScene();

        return null;
    }

    public void Draw(GameContext ctx)
    {
        EnsureUi();
        _scoreText?.SetText($"Рахунок: {ctx.Score}");
        _bestText?.SetText($"Найкращий: {ctx.BestScore}");
    }

    private void EnsureUi()
    {
        if (_uiReady) return;

        Graphics.Color = "White";
        Graphics.SetFont("Consolas", 20);

        Graphics.Text(10, 10, "Результат");
        _scoreText = Graphics.Text(10, 40, "Рахунок: 0");
        _bestText = Graphics.Text(10, 70, "Найкращий: 0");
        Graphics.Text(10, 110, "Enter = у меню");
        Graphics.Text(10, 140, "Esc = вихід");

        _uiReady = true;
    }
}

