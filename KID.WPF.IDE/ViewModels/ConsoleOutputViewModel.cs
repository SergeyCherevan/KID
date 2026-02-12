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
        private FontFamily _fontFamily = new FontFamily("Consolas");
        private double _fontSize = 14;

        public TextBox ConsoleOutputControl { get; private set; }

        public ConsoleOutputViewModel(IWindowConfigurationService windowConfigurationService)
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            windowConfigurationService.FontSettingsChanged += OnFontSettingsChanged;
        }

        void IConsoleOutputViewModel.Initialize(TextBox consoleOutputControl)
        {
            ConsoleOutputControl = consoleOutputControl ?? throw new ArgumentNullException(nameof(consoleOutputControl));
            ApplyFontFromSettings();
        }

        private void OnFontSettingsChanged(object? sender, EventArgs e) => ApplyFontFromSettings();

        private void ApplyFontFromSettings()
        {
            var settings = windowConfigurationService?.Settings;
            if (settings == null) return;

            if (!string.IsNullOrEmpty(settings.FontFamily))
                FontFamily = new FontFamily(settings.FontFamily);
            if (settings.FontSize > 0)
                FontSize = settings.FontSize;
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
