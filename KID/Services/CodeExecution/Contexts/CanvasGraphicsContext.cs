using KID.Services.CodeExecution.Contexts.Interfaces;
using System.Windows.Controls;

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
            }
        }

        public void Dispose() { }
    }
}
