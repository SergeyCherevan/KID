using KID.Services.CodeExecution.Contexts.Interfaces;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts
{
    public class CanvasGraphicsContext : IGraphicsContext
    {
        public object GraphicsTarget { get; set; }

        public CanvasGraphicsContext(Canvas graphicsCanvas)
        { 
            GraphicsTarget = graphicsCanvas;
        }

        public void Init()
        {
            Graphics.Init(GraphicsTarget as Canvas);
        }

        public void Dispose() { }
    }
}
