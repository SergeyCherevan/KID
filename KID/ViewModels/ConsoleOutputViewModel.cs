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
    public class ConsoleOutputViewModel : ViewModelBase, IConsoleOutputViewModel
    {
        public TextBox ConsoleOutputControl { get; private set; }

        void IConsoleOutputViewModel.Initialize(TextBox consoleOutputControl)
        {
            ConsoleOutputControl = consoleOutputControl;
        }

        public void Clear() => ConsoleOutputControl.Clear();
        public void AppendText(string text)
        {
            ConsoleOutputControl.AppendText(text);
            ConsoleOutputControl.ScrollToEnd();
        }
    }
}
