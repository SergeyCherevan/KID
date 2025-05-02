// Этот код можно вставить в редактор вашей среды KID

// Бесконечный вывод в консоль
for (int i = 0; i < 100; i++)
{
    System.Console.WriteLine($"Счётчик: {i}");
    KID.Graphics.SetColor("Red");
    KID.Graphics.Circle(50 + i * 2, 100, 20);
    System.Threading.Thread.Sleep(100); // имитируем долгую операцию
}

System.Console.WriteLine("Готово!");
KID.Graphics.SetColor("Green");
KID.Graphics.Rectangle(200, 200, 100, 100);

KID.Graphics.SetColor("White");
KID.Graphics.SetFont("Arial", 30);
KID.Graphics.Text(50, 100, "Тест окончен!");