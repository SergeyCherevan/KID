using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KID.Services.Initialize
{
    public class WindowConfigurationData
    {
        public string Language { get; set; } = "C#";
        public string FontFamily { get; set; } = "Consolas";
        public double FontSize { get; set; } = 14;
        public string ConsoleMessage { get; set; } = "Консольный вывод...";
        public string TemplateName { get; set; } = "HelloWorld. Console & Graphics.cs";
        public string TemplateCode { get; set; } =
@"System.Console.WriteLine(""Hello World!"");

KID.Graphics.SetColor(255, 0, 0);
KID.Graphics.Circle(150, 150, 125);

KID.Graphics.SetColor(0x0000FF);
KID.Graphics.Rectangle(150, 150, 100, 100);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(150, 150, ""Hello\nWorld!"");"
        ;
    }
}
