using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using KID;
using Keyboard = KID.Keyboard;
using Mouse = KID.Mouse;

// Scenes / menu example.
// Goal: show how to structure code with "scenes":
// Start screen -> Game -> Result screen.

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
        scene = next;

    scene.Draw(ctx);
    Thread.Sleep(16);
}

// ---- shared state ----

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

// ---- scenes ----

class StartScene : IScene
{
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
        Graphics.Clear();
        Graphics.Color = "White";
        Graphics.SetFont("Consolas", 20);

        Graphics.Text(10, 10, "Scenes example");
        Graphics.Text(10, 40, "Enter = start");
        Graphics.Text(10, 70, "Esc = exit");
        if (ctx.BestScore > 0)
            Graphics.Text(10, 110, $"Best: {ctx.BestScore}");
    }
}

class PlayScene : IScene
{
    private readonly Random _rng = new Random();
    private Point _target = new Point(150, 150);
    private double _r = 26;
    private const int DurationSeconds = 15;

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
        double elapsed = (DateTime.UtcNow - ctx.GameStartUtc).TotalSeconds;
        double left = DurationSeconds - elapsed;

        Graphics.Clear();

        Graphics.Color = "Lime";
        Graphics.Circle(_target.X, _target.Y, _r);

        Graphics.Color = "White";
        Graphics.SetFont("Consolas", 18);
        Graphics.Text(10, 10, $"Score: {ctx.Score}");
        Graphics.Text(10, 34, $"Time: {Math.Ceiling(Math.Max(0, left))} s");
        Graphics.Text(10, 58, "Click the circle!");
    }

    private void Respawn()
    {
        (double w, double h) = DispatcherManager.InvokeOnUI(() =>
            (Graphics.Canvas.ActualWidth, Graphics.Canvas.ActualHeight));

        if (w < 200) w = 400;
        if (h < 200) h = 300;

        double x = _r + _rng.NextDouble() * Math.Max(1, (w - 2 * _r));
        double y = _r + _rng.NextDouble() * Math.Max(1, (h - 2 * _r));
        _target = new Point(x, y);
    }
}

class ResultScene : IScene
{
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
        Graphics.Clear();
        Graphics.Color = "White";
        Graphics.SetFont("Consolas", 20);

        Graphics.Text(10, 10, "Result");
        Graphics.Text(10, 40, $"Score: {ctx.Score}");
        Graphics.Text(10, 70, $"Best: {ctx.BestScore}");
        Graphics.Text(10, 110, "Enter = back to menu");
        Graphics.Text(10, 140, "Esc = exit");
    }
}

