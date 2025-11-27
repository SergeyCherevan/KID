using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.ViewModels
{
    public class GraphicsOutputViewModel : ViewModelBase, IGraphicsOutputViewModel
    {
        public Canvas GraphicsCanvasControl { get; private set; }

        public void Initialize(Canvas graphicsCanvasControl)
        {
            GraphicsCanvasControl = graphicsCanvasControl ?? throw new ArgumentNullException(nameof(graphicsCanvasControl));
        }

        public void Clear()
        {
            GraphicsCanvasControl?.Children.Clear();
        }
    }
}
