using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts
{
    public class CanvasTextBoxContextFabric
    {
        private readonly App _app;
        private readonly MainWindow _mainWindow;

        public CanvasTextBoxContextFabric(App app, MainWindow mainWindow)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public CodeExecutionContext Create(Canvas canvas, TextBox textBox, CancellationToken cancellationToken)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));
            if (textBox == null)
                throw new ArgumentNullException(nameof(textBox));
            
            var context = new CodeExecutionContext
            {
                GraphicsContext = new CanvasGraphicsContext(canvas, _mainWindow),
                ConsoleContext = new TextBoxConsoleContext(textBox),
                CancellationToken = cancellationToken,
                Dispatcher = _app.Dispatcher
            };
            
            return context;
        }
    }
}
