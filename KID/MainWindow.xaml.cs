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

namespace KID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            // Здесь будет запуск пользовательского кода
            MessageBox.Show("Кнопка 'Запустить' нажата!");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Здесь будет остановка выполнения
            MessageBox.Show("Кнопка 'Стоп' нажата!");
        }
    }
}