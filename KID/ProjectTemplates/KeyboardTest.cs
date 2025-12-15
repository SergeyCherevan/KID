using System;
using System.Threading;
using KID;
using KEY = System.Windows.Input.Key;

// Тестовый скрипт для проверки Keyboard API
// Рисует красный круг и перемещает его по Canvas с помощью клавиш стрелок и WASD

// Очищаем Canvas
Graphics.Clear();

// Устанавливаем фокус на Canvas для получения событий клавиатуры
Keyboard.Focus();

// Устанавливаем красный цвет
Graphics.Color = "Red";

// Рисуем красный круг радиусом 50 пикселей в центре Canvas
double circleX = 150;
double circleY = 150;
double circleRadius = 50;
var circle = Graphics.Circle(circleX, circleY, circleRadius);

// Скорость перемещения
double speed = 5.0;

// Основной цикл отслеживания нажатий клавиш
while (true)
{
    // Проверка на остановку выполнения
    StopManager.StopIfButtonPressed();
    
    // Отслеживаем нажатия клавиш стрелок и WASD
    bool moved = false;
    
    // Стрелки и WASD для перемещения
    if (Keyboard.IsKeyPressed(KEY.Left) || Keyboard.IsKeyPressed(KEY.A))
    {
        circleX -= speed;
        moved = true;
    }
    
    if (Keyboard.IsKeyPressed(KEY.Right) || Keyboard.IsKeyPressed(KEY.D))
    {
        circleX += speed;
        moved = true;
    }
    
    if (Keyboard.IsKeyPressed(KEY.Up) || Keyboard.IsKeyPressed(KEY.W))
    {
        circleY -= speed;
        moved = true;
    }
    
    if (Keyboard.IsKeyPressed(KEY.Down) || Keyboard.IsKeyPressed(KEY.S))
    {
        circleY += speed;
        moved = true;
    }
    
    // Если круг переместился, обновляем его позицию
    if (moved && circle != null)
    {
        circle.SetCenterXY(circleX, circleY);
        
        // Выводим информацию в консоль
        Console.WriteLine($"Круг перемещен: X={circleX:F1}, Y={circleY:F1}");
    }
    
    // Небольшая задержка для плавности
    Thread.Sleep(16); // ~60 FPS
}

