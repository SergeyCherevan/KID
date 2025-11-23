using KID.Services.CodeExecution.Interfaces;
using System.Windows.Controls;

namespace KID.Services.CodeExecution
{
    public class TextBoxConsoleContext : IConsoleContext
    {
        public object ConsoleTarget { get; set; }

        public TextBoxConsoleContext(TextBox graphicsCanvas)
        {
            ConsoleTarget = graphicsCanvas;
        }
    }
}
