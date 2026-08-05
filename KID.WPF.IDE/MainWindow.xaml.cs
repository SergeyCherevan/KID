using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using KID.Services.Errors.Interfaces;
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
        private readonly IAsyncOperationErrorHandler _asyncOperationErrorHandler;
        private readonly IMainWindowWinAPIInteropService _windowInteropService;
        private readonly ICodeEditorsViewModel _codeEditorsViewModel;
        private bool _isCloseApproved;
        private bool _isCloseCheckInProgress;

        public MainWindow()
        {
            InitializeComponent();

            // Получаем сервисы из DI контейнера
            if (App.ServiceProvider == null)
                throw new InvalidOperationException("ServiceProvider is not initialized");

            _windowInitializationService = App.ServiceProvider.GetRequiredService<IWindowInitializationService>();
            _asyncOperationErrorHandler = App.ServiceProvider.GetRequiredService<IAsyncOperationErrorHandler>();
            _windowInteropService = App.ServiceProvider.GetRequiredService<IMainWindowWinAPIInteropService>();
            _codeEditorsViewModel = App.ServiceProvider.GetRequiredService<ICodeEditorsViewModel>();

            SourceInitialized += OnSourceInitialized;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _ = InitializeAfterLoadedAsync();
        }

        private async Task InitializeAfterLoadedAsync()
        {
            try
            {
                await _windowInitializationService.InitializeAsync();

                if (DataContext is IMainViewModel mainViewModel)
                {
                    mainViewModel.RequestDragMove += DragMove;
                }
            }
            catch (Exception ex)
            {
                await _asyncOperationErrorHandler.ExecuteAsync(
                    () => Task.FromException(ex),
                    "Error_InitializationFailed");
            }
        }

        void IClosable.Close()
        {
            base.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isCloseApproved)
            {
                e.Cancel = true;
                if (!_isCloseCheckInProgress)
                {
                    _isCloseCheckInProgress = true;
                    _ = CompleteCloseAsync();
                }
            }

            base.OnClosing(e);
        }

        private async Task CompleteCloseAsync()
        {
            try
            {
                if (await _codeEditorsViewModel.PrepareForApplicationCloseAsync())
                {
                    _isCloseApproved = true;
                    _ = Dispatcher.BeginInvoke(new Action(Close));
                }
            }
            catch (Exception ex)
            {
                await _asyncOperationErrorHandler.ExecuteAsync(
                    () => Task.FromException(ex),
                    "Error_ClosePreparationFailed");
            }
            finally
            {
                _isCloseCheckInProgress = false;
            }
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
