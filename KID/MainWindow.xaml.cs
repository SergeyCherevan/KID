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
        private CodeRunner codeRunner;

        public MainWindow()
        {
            InitializeComponent();
            codeRunner = new CodeRunner(AppendConsoleOutput);
        }

        private void AppendConsoleOutput(string text)
        {
            Dispatcher.Invoke(() =>
            {
                ConsoleOutput.AppendText(text + Environment.NewLine);
                ConsoleOutput.ScrollToEnd();
            });
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOutput.Clear();
            GraphicsCanvas.Children.Clear();

            // ИНИЦИАЛИЗАЦИЯ графики
            Graphics.Init(GraphicsCanvas);

            var code = CodeEditor.Text;
            codeRunner.CompileAndRun(code);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Пока оставим пустым, позже сделаем принудительную остановку
        }

    }
}