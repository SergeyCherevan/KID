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

            CodeEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C#");
            CodeEditor.Text =
@"System.Console.WriteLine(""Hello World!"");

KID.Graphics.SetColor(""Red"");
KID.Graphics.Circle(150, 150, 125);

KID.Graphics.SetColor(""Blue"");
KID.Graphics.Rectangle(150, 150, 100, 100);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(150, 150, ""C#"");";

            ConsoleOutput.Text = "Консольный вывод...";
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                this.MaximizeButton.Content = "☐";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                this.MaximizeButton.Content = "❐";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
@"System.Console.WriteLine(""Hello World!"");

KID.Graphics.SetColor(""Red"");
KID.Graphics.Circle(150, 150, 125);

KID.Graphics.SetColor(""Blue"");
KID.Graphics.Rectangle(150, 150, 100, 100);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(150, 150, ""C#"")";


            ConsoleOutput.Text = "Консольный вывод...";
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

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (CodeEditor.CanUndo)
            {
                CodeEditor.Undo();
            }
        }

        private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (CodeEditor.CanRedo)
            {
                CodeEditor.Redo();
            }
        }

    }
}