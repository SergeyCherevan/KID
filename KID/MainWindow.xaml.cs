using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using KID.Views;
using KID.Services;
using KID.ViewModels;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using KID.Services.Initialize;
using KID.Services.Initialize.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IClosable
    {
        private readonly IWindowInitializationService _windowInitializationService;

        public MainWindow()
        {
            InitializeComponent();
            
            // Получаем сервисы из DI контейнера
            _windowInitializationService = App.ServiceProvider.GetRequiredService<IWindowInitializationService>();
            
            // Инициализируем окно после загрузки всех элементов
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _windowInitializationService.Initialize();
        }

        void IClosable.Close()
        {
            base.Close();
        }
    }
}