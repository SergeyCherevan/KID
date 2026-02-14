using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using KID.Services.Initialize.Interfaces;
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
        private readonly IWindowConfigurationService windowConfigurationService;

        public TextBox ConsoleOutputControl { get; private set; }

        public ConsoleOutputViewModel(IWindowConfigurationService windowConfigurationService)
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            windowConfigurationService.FontSettingsChanged += OnFontSettingsChanged;
        }

        public void Initialize(TextBox consoleOutputControl)
        {
            ConsoleOutputControl = consoleOutputControl ?? throw new ArgumentNullException(nameof(consoleOutputControl));
            OnFontSettingsChanged(this, EventArgs.Empty);
        }

        private void OnFontSettingsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(FontSize));
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

        /// <inheritdoc />
        public FontFamily FontFamily => new FontFamily(
            windowConfigurationService.Settings.FontFamily ?? "Consolas");

        /// <inheritdoc />
        public double FontSize => windowConfigurationService.Settings.FontSize > 0
            ? windowConfigurationService.Settings.FontSize
            : 14;
    }
}
