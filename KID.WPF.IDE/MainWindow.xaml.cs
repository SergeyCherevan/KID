using System.Windows;
using System.Windows.Interop;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.WindowInterop.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IClosable
    {
        private readonly IWindowInitializationService _windowInitializationService;
        private readonly IMainWindowWinAPIInteropService _windowInteropService;

        public MainWindow()
        {
            InitializeComponent();

            // Получаем сервисы из DI контейнера
            if (App.ServiceProvider == null)
                throw new InvalidOperationException("ServiceProvider is not initialized");

            _windowInitializationService = App.ServiceProvider.GetRequiredService<IWindowInitializationService>();
            _windowInteropService = App.ServiceProvider.GetRequiredService<IMainWindowWinAPIInteropService>();

            SourceInitialized += OnSourceInitialized;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _windowInitializationService?.Initialize();

            if (DataContext is IMainViewModel mainViewModel)
            {
                mainViewModel.RequestDragMove += DragMove;
            }
        }

        void IClosable.Close()
        {
            base.Close();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _windowInteropService.AttachWindow(handle);
            SizeChanged += MainWindow_SizeChanged;
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _windowInteropService.OnWindowSizeChanged(handle);
        }
    }
}
