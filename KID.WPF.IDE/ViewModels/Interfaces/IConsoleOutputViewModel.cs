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
        /// <summary>
        /// Текстовое поле вывода пользовательской программы; отсутствует до передачи контрола из View.
        /// </summary>
        TextBox? ConsoleOutputControl { get; }

        /// <summary>
        /// Связывает ViewModel с текстовым полем, созданным из XAML.
        /// </summary>
        /// <param name="consoleOutputControl">Текстовое поле для вывода сообщений программы.</param>
        void Initialize(TextBox consoleOutputControl);

        void Clear();
        string Text { get; set; }

        /// <summary>
        /// Шрифт для отображения текста в консоли.
        /// </summary>
        FontFamily FontFamily { get; }

        /// <summary>
        /// Размер шрифта в консоли.
        /// </summary>
        double FontSize { get; }
    }
}
