using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace KID.ViewModels
{
    public class ConsoleOutputViewModel : ViewModelBase, IConsoleOutputViewModel
    {
        private FontFamily _fontFamily = new FontFamily("Consolas");
        private double _fontSize = 14;

        public TextBox ConsoleOutputControl { get; private set; }

        void IConsoleOutputViewModel.Initialize(TextBox consoleOutputControl)
        {
            ConsoleOutputControl = consoleOutputControl ?? throw new ArgumentNullException(nameof(consoleOutputControl));
        }

        public void Clear()
        {
            ConsoleOutputControl?.Clear();
        }
        
        public string Text
        {
            get => ConsoleOutputControl?.Text ?? string.Empty;
            set
            {
                if (ConsoleOutputControl != null)
                {
                    ConsoleOutputControl.Text = value;
                    ConsoleOutputControl.ScrollToEnd();
                }
            }
        }

        public FontFamily FontFamily
        {
            get => _fontFamily;
            set
            {
                if (SetProperty(ref _fontFamily, value) && ConsoleOutputControl != null)
                    ConsoleOutputControl.FontFamily = value;
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (SetProperty(ref _fontSize, value) && ConsoleOutputControl != null)
                    ConsoleOutputControl.FontSize = value;
            }
        }
    }
}
