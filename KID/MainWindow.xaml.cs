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

namespace KID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }

        public ICSharpCode.AvalonEdit.TextEditor CodeEditor => ((CodeEditorView)this.FindName("CodeEditorView")).CodeEditorControl;
        public TextBox ConsoleOutput => ((ConsoleOutputView)this.FindName("ConsoleOutputView")).ConsoleOutputControl;
        public Canvas GraphicsOutput => ((GraphicsOutputView)this.FindName("GraphicsOutputView")).GraphicsCanvasControl;

        public CodeRunner CodeRunner;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            CodeRunner = new CodeRunner(AppendConsoleOutput);

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
    }
}