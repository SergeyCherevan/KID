using System;
using System.Threading;
using KID;

// Тестовый скрипт для проверки Mouse API
// Рисует точку (круг радиуса 1) на позиции курсора мыши

while (true)
{
    // Проверка на остановку выполнения
    StopManager.StopIfButtonPressed();
    
    // Получаем текущую позицию мыши
    var position = Mouse.CurrentPosition;
    
    // Если курсор на Canvas, рисуем точку
    if (position.HasValue)
    {
        Graphics.Color = "Green";
        Graphics.Circle(position.Value.X, position.Value.Y, 1);
        Console.WriteLine($"Position: {position.Value.X}, {position.Value.Y}");
    }

    var click = Mouse.CurrentClick;
    if (click.Status != ClickStatus.NoClick)
    {
        Graphics.Color = "Red";
        Graphics.Circle(click.Position.Value.X, click.Position.Value.Y, 5);
        Console.WriteLine($"Click: {click.Status}, {click.Position.Value.X}, {click.Position.Value.Y}");
    }
    
    // Небольшая задержка для плавности
    Thread.Sleep(10);
}
