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

namespace KID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            CodeEditorView.SetSyntaxHighlighting("C#");
            CodeEditorView.Text =
@"System.Console.WriteLine(""Hello World!"");

KID.Graphics.SetColor(255, 0, 0);
KID.Graphics.Circle(150, 150, 125);

KID.Graphics.SetColor(0x0000FF);
KID.Graphics.Rectangle(150, 150, 100, 100);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(150, 150, ""Hello\nWorld!"");";

            ConsoleOutputView.Clear();
            ConsoleOutputView.AppendText("Консольный вывод...");
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}