// Здесь специально нет using.
// Мы пишем полные "адреса" классов: System.Console и KID.Graphics.

System.Console.WriteLine("Без using тоже можно... но выглядит тяжеловато :(");

KID.Graphics.Color = "Red";
KID.Graphics.Circle(150, 150, 125);

KID.Graphics.Color = "Blue";
KID.Graphics.Rectangle(150, 150, 100, 100);

KID.Graphics.Color = "White";
KID.Graphics.SetFont("Arial", 25);
KID.Graphics.Text(150, 150, "Hello\nWorld!");
