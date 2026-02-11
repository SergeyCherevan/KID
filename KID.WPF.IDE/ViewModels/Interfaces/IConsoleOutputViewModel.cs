using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace KID.ViewModels.Interfaces
{
    public interface IConsoleOutputViewModel
    {
        TextBox ConsoleOutputControl { get; }

        void Initialize(TextBox consoleOutputControl);

        void Clear();
        string Text { get; set; }

        /// <summary>
        /// Шрифт для отображения текста в консоли.
        /// </summary>
        FontFamily FontFamily { get; set; }

        /// <summary>
        /// Размер шрифта в консоли.
        /// </summary>
        double FontSize { get; set; }
    }
}
