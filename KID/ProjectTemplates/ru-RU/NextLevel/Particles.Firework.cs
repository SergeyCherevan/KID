using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using KID;
using Mouse = KID.Mouse;

// Частицы / фейерверк.
// Кликни по Canvas, чтобы запустить «фейерверк».
// Чему учит: массивы/списки, цикл update+draw, простая физика, время жизни объектов.

var rng = new Random();
var particles = new List<Particle>(1024);

Graphics.SetFont("Consolas", 16);

var last = DateTime.UtcNow;

while (true)
{
    StopManager.StopIfButtonPressed();

    // время
    var now = DateTime.UtcNow;
    double dt = (now - last).TotalSeconds;
    last = now;
    if (dt > 0.05) dt = 0.05;

    // создаём по клику
    var click = Mouse.CurrentClick;
    if (click.Position.HasValue &&
        (click.Status == ClickStatus.OneLeftClick || click.Status == ClickStatus.DoubleLeftClick))
    {
        SpawnFirework(click.Position.Value);
    }

    // update
    for (int i = particles.Count - 1; i >= 0; i--)
    {
        var p = particles[i];
        p.Life -= dt;
        if (p.Life <= 0)
        {
            particles.RemoveAt(i);
            continue;
        }

        // физика
        p.Velocity = new Point(p.Velocity.X, p.Velocity.Y + 220 * dt); // гравитация
        p.Position = new Point(p.Position.X + p.Velocity.X * dt, p.Position.Y + p.Velocity.Y * dt);

        particles[i] = p;
    }

    // draw
    Graphics.Clear();

    for (int i = 0; i < particles.Count; i++)
    {
        var p = particles[i];
        Graphics.Color = p.Color;
        Graphics.Circle(p.Position.X, p.Position.Y, p.Radius);
    }

    Graphics.Color = "White";
    Graphics.Text(10, 10, "Частицы / Фейерверк");
    Graphics.Text(10, 30, "Кликни для запуска. «Стоп» = выход");
    Graphics.Text(10, 50, $"Частиц: {particles.Count}");

    Thread.Sleep(16);
}

void SpawnFirework(Point origin)
{
    string[] palette = { "DeepSkyBlue", "Lime", "Orange", "HotPink", "Yellow" };
    string color = palette[rng.Next(palette.Length)];

    int count = 120;
    double speedMin = 80;
    double speedMax = 280;

    for (int i = 0; i < count; i++)
    {
        double angle = rng.NextDouble() * Math.PI * 2;
        double speed = speedMin + rng.NextDouble() * (speedMax - speedMin);

        // Немного «вверх»
        double vx = Math.Cos(angle) * speed;
        double vy = Math.Sin(angle) * speed - 120;

        particles.Add(new Particle
        {
            Position = origin,
            Velocity = new Point(vx, vy),
            Life = 1.6 + rng.NextDouble() * 0.8,
            Radius = 2 + rng.NextDouble() * 2,
            Color = color
        });
    }

    // ограничим количество (чтобы не перегружать Canvas)
    if (particles.Count > 2500)
        particles.RemoveRange(0, particles.Count - 2500);
}

struct Particle
{
    public Point Position;
    public Point Velocity;
    public double Life;
    public double Radius;
    public string Color;
}

