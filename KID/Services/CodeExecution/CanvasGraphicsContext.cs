using KID.Services.CodeExecution.Interfaces;
using System.Windows.Controls;

namespace KID.Services.CodeExecution
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
