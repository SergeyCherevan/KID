using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.ViewModels.Interfaces
{
    public interface IGraphicsOutputViewModel
    {
        Canvas GraphicsCanvasControl { get; }

        void Initialize(Canvas graphicsCanvasControl);

        void Clear();
    }
}
