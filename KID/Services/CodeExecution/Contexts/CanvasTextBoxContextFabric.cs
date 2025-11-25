using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts
{
    public class CanvasTextBoxContextFabric
    {
        public CodeExecutionContext Create(Canvas canvas, TextBox textBox, CancellationToken cancellationToken)
        {
            return new CodeExecutionContext
            {
                GraphicsContext = new CanvasGraphicsContext(canvas),
                ConsoleContext = new TextBoxConsoleContext(textBox),
                CancellationToken = cancellationToken,
            };
        }
    }
}
