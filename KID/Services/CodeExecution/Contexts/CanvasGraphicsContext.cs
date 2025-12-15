using KID.Services.CodeExecution.Contexts.Interfaces;
using System.Windows;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts
{
    public class CanvasGraphicsContext : IGraphicsContext
    {
        public object GraphicsTarget { get; set; }
        private readonly Window _mainWindow;

        public CanvasGraphicsContext(Canvas graphicsCanvas, Window mainWindow)
        {
            if (graphicsCanvas == null)
                throw new ArgumentNullException(nameof(graphicsCanvas));
            if (mainWindow == null)
                throw new ArgumentNullException(nameof(mainWindow));
            
            GraphicsTarget = graphicsCanvas;
            _mainWindow = mainWindow;
        }

        public void Init()
        {
            if (GraphicsTarget is Canvas canvas)
            {
                Graphics.Init(canvas);
                Mouse.Init(canvas);
                Keyboard.Init(_mainWindow);
                Music.Init();
            }
        }

        public void Dispose() { }
    }
}
