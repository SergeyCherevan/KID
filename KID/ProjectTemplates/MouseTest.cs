using System;
using System.Threading;
using KID;

// Тестовый скрипт для проверки Mouse API
// Рисует точку (круг радиуса 1) на позиции курсора мыши

while (true)
{
    // Проверка на остановку выполнения
    StopManager.StopIfButtonPressed();
    
    // Получаем текущую информацию о курсоре
    var cursor = Mouse.CurrentCursor;
    
    // Если курсор на Canvas, рисуем точку
    if (cursor.Position.HasValue)
    {
        Graphics.Color = "Green";
        Graphics.Circle(cursor.Position.Value.X, cursor.Position.Value.Y, 1);
        Console.WriteLine($"Position: {cursor.Position.Value.X}, {cursor.Position.Value.Y}");
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
