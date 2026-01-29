using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KID.Services.Initialize
{
    public class WindowConfigurationData
    {
        // Цветовая тема
        public string ColorTheme { get; set; } = "Light"; // Light, Dark
        
        // Язык интерфейса
        public string UILanguage { get; set; } = "ru-RU"; // ru-RU, en-US, uk-UA
        
        // Настройки редактора кода
        public string Language { get; set; } = "C#";
        public string FontFamily { get; set; } = "Consolas";
        public double FontSize { get; set; } = 14;
        public string ConsoleMessage { get; set; } = "Консольный вывод...";
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
