using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KID.Views
{
    /// <summary>
    /// Логика взаимодействия для MenuView.xaml
    /// </summary>
    public partial class MenuView : UserControl
    {
        public MenuView()
        {
            InitializeComponent();
        }

        private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.CodeEditorView.Text =
@"System.Console.WriteLine(""Hello World!"");

KID.Graphics.SetColor(""Red"");
KID.Graphics.Circle(150, 150, 125);

KID.Graphics.SetColor(""Blue"");
KID.Graphics.Rectangle(150, 150, 100, 100);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(150, 150, ""C#"");";

            MainWindow.Instance.ConsoleOutputView.Clear();
            MainWindow.Instance.ConsoleOutputView.AppendText("Консольный вывод...");
            MainWindow.Instance.GraphicsOutputView.Clear();
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
                MainWindow.Instance.CodeEditorView.Text = code;
                MainWindow.Instance.ConsoleOutputView.Clear();
                MainWindow.Instance.GraphicsOutputView.Clear();
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
                File.WriteAllText(saveFileDialog.FileName, MainWindow.Instance.CodeEditorView.Text);
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = true;

            MainWindow.Instance.ConsoleOutputView.Clear();
            MainWindow.Instance.GraphicsOutputView.Clear();

            // ИНИЦИАЛИЗАЦИЯ графики
            Graphics.Init(MainWindow.Instance.GraphicsOutputView.GraphicsCanvasControl);

            var code = MainWindow.Instance.CodeEditorView.Text;
            MainWindow.Instance.CodeRunner.CompileAndRun(code);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = false;
        }

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.CodeEditorView.CanUndo())
            {
                MainWindow.Instance.CodeEditorView.Undo();
            }
        }

        private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.CodeEditorView.CanRedo())
            {
                MainWindow.Instance.CodeEditorView.Redo();
            }
        }
    }
}
