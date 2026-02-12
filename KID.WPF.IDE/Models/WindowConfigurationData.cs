namespace KID.Models
{
    /// <summary>
    /// Модель данных настроек окна приложения (тема, язык, шрифт, шаблон кода).
    /// </summary>
    public class WindowConfigurationData
    {
        // Цветовая тема
        public string ColorTheme { get; set; } = "Light"; // Light, Dark

        // Язык интерфейса
        public string UILanguage { get; set; } = "ru-RU"; // ru-RU, en-US, uk-UA

        // Настройки редактора кода
        public string ProgrammingLanguage { get; set; } = "C#";
        public string FontFamily { get; set; } = "Consolas";
        public double FontSize { get; set; } = 14;
        public string TemplateName { get; set; } = "HelloWorld.cs";
        public string TemplateCode { get; set; } =
@"using System;
using KID;

Console.WriteLine(""Hello World!"");

Graphics.Color = (255, 0, 0);
Graphics.Circle(150, 150, 125);

Graphics.Color = 0x0000FF;
Graphics.Rectangle(150, 150, 100, 100);

Graphics.Color = ""White"";
Graphics.SetFont(""Arial"", 25);
Graphics.Text(150, 150, ""Hello\nWorld!"");"
        ;
    }
}
