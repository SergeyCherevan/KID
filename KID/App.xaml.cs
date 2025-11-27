using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using KID.Services.DI;
using KID.Services.Initialize.Interfaces;

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
            // Сохраняем настройки перед выходом
            try
            {
                var settingsService = ServiceProvider.GetRequiredService<IWindowConfigurationService>();
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
