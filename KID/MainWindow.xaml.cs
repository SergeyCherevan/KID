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

            CodeEditor.Text =
@"System.Console.WriteLine(""Hello from KID!"");

KID.Graphics.SetColor(""Red"");
KID.Graphics.Circle(200, 200, 150);

KID.Graphics.SetColor(""Blue"");
KID.Graphics.Rectangle(200, 200, 130, 75);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(200, 200, ""C# for Kids!"");";
        }

        private void AppendConsoleOutput(string text)
        {
            Dispatcher.Invoke(() =>
            {
                ConsoleOutput.AppendText(text + Environment.NewLine);
                ConsoleOutput.ScrollToEnd();
            });
        }

        private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Text =
@"System.Console.WriteLine(""Hello from KID!"");

KID.Graphics.SetColor(""Red"");
KID.Graphics.Circle(200, 200, 150);

KID.Graphics.SetColor(""Blue"");
KID.Graphics.Rectangle(200, 200, 130, 75);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(200, 200, ""C# for Kids!"");";

            ConsoleOutput.Clear();
            GraphicsCanvas.Children.Clear();
        }

        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "C# файлы (*.cs)|*.cs|Все файлы (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string code = File.ReadAllText(openFileDialog.FileName);
                CodeEditor.Text = code;
                ConsoleOutput.Clear();
                GraphicsCanvas.Children.Clear();
            }
        }

        private void SaveFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "C# файлы (*.cs)|*.cs|Все файлы (*.*)|*.*",
                FileName = "Program.cs"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, CodeEditor.Text);
            }
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