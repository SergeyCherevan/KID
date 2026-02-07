using System;
using System.Threading;
using System.Windows.Shapes;
using KID;

// Спрайти + зіткнення (Sprite API).
// Вчить: ООП (клас Sprite), рух, ігровий цикл, визначення зіткнень.

double playerX = 120;
double playerY = 120;
double vx = 220; // пікселів за секунду
double vy = 160;

double enemyX = 360;
double enemyY = 200;
double ex = -140;

var last = DateTime.UtcNow;

// --- UI (створюємо один раз, далі тільки рухаємо/перефарбовуємо) ---

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
var enemyLine = Graphics.Line(-30, -20, 30, 20); // Важливо: Line не має Canvas.Left/Top — але Sprite.Move працює

var enemy = new Sprite(enemyX, enemyY, enemyRect!, enemyLine!);

Graphics.Color = "White";
Graphics.SetFont("Consolas", 14);
Graphics.Text(10, 10, "Sprite API: спрайти та зіткнення\nНатисни Stop, щоб вийти");

while (true)
{
    StopManager.StopIfButtonPressed();

    var now = DateTime.UtcNow;
    double dt = (now - last).TotalSeconds;
    last = now;
    if (dt > 0.1) dt = 0.1;

    (double w, double h) = Graphics.GetCanvasSize();

    // --- рух гравця (відскоки від стін) ---
    playerX += vx * dt;
    playerY += vy * dt;

    if (playerX < 0) { playerX = 0; vx = -vx; }
    if (playerY < 0) { playerY = 0; vy = -vy; }
    if (playerX > w) { playerX = w; vx = -vx; }
    if (playerY > h) { playerY = h; vy = -vy; }

    player.X = playerX;
    player.Y = playerY;

    // --- рух ворога ---
    enemyX += ex * dt;
    if (enemyX < 60) { enemyX = 60; ex = -ex; }
    if (enemyX > w - 60) { enemyX = w - 60; ex = -ex; }

    enemy.X = enemyX;
    enemy.Y = enemyY;

    // --- зіткнення ---
    var hits = player.DetectCollisions(new() { enemy });
    bool collided = hits.Count > 0;

    if (playerBody is Ellipse ellipse)
        ellipse.SetFillColor(collided ? "Red" : "DodgerBlue");

    if (enemyRect is Rectangle rect)
        rect.SetFillColor(collided ? "Yellow" : "Orange");

    if (collided)
    {
        // Можна додати свою інформацію до зіткнення:
        hits[0].AdditionalInfo = new { Score = 1, When = DateTime.UtcNow };
    }

    Thread.Sleep(16); // ~60 FPS
}

