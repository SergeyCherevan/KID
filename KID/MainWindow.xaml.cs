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
using KID.Services.Initialize;
using KID.Services.Initialize.Interfaces;

namespace KID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IClosable
    {
        public static MainWindow Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            IWindowConfigurationService windowConfigurationService = new WindowConfigurationService();

            IWindowInitializationService windowInitializationService = new WindowInitializationService(
                this,
                CodeEditorView,
                ConsoleOutputView,
                windowConfigurationService
            );

            windowInitializationService.Initialize();
        }

        void IClosable.Close()
        {
            base.Close();
        }
    }
}