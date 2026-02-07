using System;
using System.Threading;
using System.Windows.Shapes;
using KID;

// Sprites + collisions (Sprite API).
// Teaches: OOP (Sprite class), movement, game loop, collision detection.

double playerX = 120;
double playerY = 120;
double vx = 220; // pixels per second
double vy = 160;

double enemyX = 360;
double enemyY = 200;
double ex = -140;

var last = DateTime.UtcNow;

// --- UI (create once, then only move/recolor) ---

Graphics.Color = "DodgerBlue";
var playerBody = Graphics.Circle(0, 0, 18);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 16);
var playerLabel = Graphics.Text(-6, -10, "P");

var player = new Sprite(playerX, playerY, playerBody!, playerLabel!);

Graphics.FillColor = "Orange";
Graphics.StrokeColor = "Black";
var enemyRect = Graphics.Rectangle(-30, -20, 60, 40);

Graphics.StrokeColor = "Black";
var enemyLine = Graphics.Line(-30, -20, 30, 20); // Note: Line has no Canvas.Left/Top, but Sprite.Move still works

var enemy = new Sprite(enemyX, enemyY, enemyRect!, enemyLine!);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 14);
Graphics.Text(10, 10, "Sprite API: sprites and collisions\nPress Stop to exit");

while (true)
{
    StopManager.StopIfButtonPressed();

    var now = DateTime.UtcNow;
    double dt = (now - last).TotalSeconds;
    last = now;
    if (dt > 0.1) dt = 0.1;

    (double w, double h) = Graphics.GetCanvasSize();

    // --- player movement (bounce on walls) ---
    playerX += vx * dt;
    playerY += vy * dt;

    if (playerX < 0) { playerX = 0; vx = -vx; }
    if (playerY < 0) { playerY = 0; vy = -vy; }
    if (playerX > w) { playerX = w; vx = -vx; }
    if (playerY > h) { playerY = h; vy = -vy; }

    player.X = playerX;
    player.Y = playerY;

    // --- enemy movement ---
    enemyX += ex * dt;
    if (enemyX < 60) { enemyX = 60; ex = -ex; }
    if (enemyX > w - 60) { enemyX = w - 60; ex = -ex; }

    enemy.X = enemyX;
    enemy.Y = enemyY;

    // --- collisions ---
    var hits = player.DetectCollisions(new() { enemy });
    bool collided = hits.Count > 0;

    if (playerBody is Ellipse ellipse)
        ellipse.SetFillColor(collided ? "Red" : "DodgerBlue");

    if (enemyRect is Rectangle rect)
        rect.SetFillColor(collided ? "Yellow" : "Orange");

    if (collided)
    {
        // You can attach custom info to the collision:
        hits[0].AdditionalInfo = new { Score = 1, When = DateTime.UtcNow };
    }

    Thread.Sleep(16); // ~60 FPS
}

