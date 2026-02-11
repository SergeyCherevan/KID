using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using KID.Services.DI;
using KID.Services.Initialize.Interfaces;
using KID.ViewModels.Interfaces;

namespace KID
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddKIDServices();
            ServiceProvider = serviceCollection.BuildServiceProvider();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Синхронизируем и сохраняем настройки перед выходом
            try
            {
                var settingsService = ServiceProvider.GetRequiredService<IWindowConfigurationService>();
                var codeEditorsViewModel = ServiceProvider.GetService<ICodeEditorsViewModel>();
                
                if (codeEditorsViewModel != null && settingsService.Settings != null)
                {
                    var fontFamily = codeEditorsViewModel.FontFamily;
                    if (fontFamily != null && !string.IsNullOrEmpty(fontFamily.Source))
                        settingsService.Settings.FontFamily = fontFamily.Source;
                    if (codeEditorsViewModel.FontSize > 0)
                        settingsService.Settings.FontSize = codeEditorsViewModel.FontSize;
                }
                
                settingsService.SaveSettings();
            }
            catch
            {
                // Игнорируем ошибки при сохранении при выходе
            }
            
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            base.OnExit(e);
        }
    }
}
