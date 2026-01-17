using KID.Services.CodeExecution.Contexts.Interfaces;
using System.Windows.Controls;
using System.Windows;

namespace KID.Services.CodeExecution.Contexts
{
    public class CanvasGraphicsContext : IGraphicsContext
    {
        public object GraphicsTarget { get; set; }

        public CanvasGraphicsContext(Canvas graphicsCanvas)
        {
            if (graphicsCanvas == null)
                throw new ArgumentNullException(nameof(graphicsCanvas));
            
            GraphicsTarget = graphicsCanvas;
        }

        public void Init()
        {
            if (GraphicsTarget is Canvas canvas)
            {
                Graphics.Init(canvas);
                Mouse.Init(canvas);

                // Keyboard слушает уровень окна (Preview* события), чтобы работало даже не только на Canvas.
                var window = Window.GetWindow(canvas);
                if (window != null)
                    Keyboard.Init(window);

                Music.Init();
            }
        }

        public void Dispose() { }
    }
}
