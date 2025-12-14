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
        private readonly App _app;

        public CanvasTextBoxContextFabric(App app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public CodeExecutionContext Create(Canvas canvas, TextBox textBox, CancellationToken cancellationToken)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));
            if (textBox == null)
                throw new ArgumentNullException(nameof(textBox));
            
            var context = new CodeExecutionContext
            {
                GraphicsContext = new CanvasGraphicsContext(canvas),
                ConsoleContext = new TextBoxConsoleContext(textBox),
                CancellationToken = cancellationToken,
                Dispatcher = _app.Dispatcher
            };
            
            return context;
        }
    }
}
