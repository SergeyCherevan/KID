using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.ViewModels.Interfaces
{
    public interface IConsoleOutputViewModel
    {
        TextBox ConsoleOutputControl { get; }

        void Initialize(TextBox consoleOutputControl);

        void Clear();
        string Text { get; set; }
    }
}
