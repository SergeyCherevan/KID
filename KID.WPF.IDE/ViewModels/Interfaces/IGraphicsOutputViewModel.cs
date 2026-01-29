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

        /// <summary>
        /// Значение MinWidth правой панели вывода, считанное из XAML при инициализации.
        /// </summary>
        double DefaultOutputViewMinWidth { get; }

        /// <summary>
        /// Значение MinHeight строки графики, считанное из XAML при инициализации.
        /// </summary>
        double DefaultOutputViewMinHeight { get; }

        void Initialize(Canvas graphicsCanvasControl);

        void Clear();

        /// <summary>
        /// Сбрасывает min-ограничения области вывода (как в XAML).
        /// </summary>
        void ResetOutputViewMinSizeToDefault();
    }
}
