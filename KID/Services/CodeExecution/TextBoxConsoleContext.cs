using KID.Services.CodeExecution.Interfaces;
using System.IO;
using System.Windows.Controls;

namespace KID.Services.CodeExecution
{
    public class TextBoxConsoleContext : IConsoleContext
    {
        private TextWriter originalConsole;
        public object ConsoleTarget { get; set; }

        public TextBoxConsoleContext(TextBox graphicsCanvas)
        {
            ConsoleTarget = graphicsCanvas;
        }

        public void Init()
        {
            originalConsole = Console.Out;
            Console.SetOut(new ConsoleRedirector(ConsoleTarget as TextBox));
        }

        public void Dispose()
        {
            Console.SetOut(originalConsole);
        }
    }
}
