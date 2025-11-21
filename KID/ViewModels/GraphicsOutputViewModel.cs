using KID.ViewModels.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.ViewModels
{
    public class GraphicsOutputViewModel : IGraphicsOutputViewModel
    {
        public Canvas GraphicsCanvasControl { get; private set; }

        public void Initialize(Canvas graphicsCanvasControl)
        {
            GraphicsCanvasControl = graphicsCanvasControl;
        }

        public void Clear() => GraphicsCanvasControl.Children.Clear();
    }
}
