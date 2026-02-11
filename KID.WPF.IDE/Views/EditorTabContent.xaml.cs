using System.Windows;
using System.Windows.Controls;
using KID.Models;

namespace KID.Views
{
    /// <summary>
    /// UserControl с AvalonEdit для контента вкладки открытого файла.
    /// При загрузке присваивает TextEditor в OpenedFileTab и синхронизирует содержимое.
    /// </summary>
    public partial class EditorTabContent : UserControl
    {
        public EditorTabContent()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not OpenedFileTab tab)
                return;

            tab.TextEditor = TextEditor;
            TextEditor.Text = tab.Content;

            TextEditor.TextChanged += (s, args) =>
            {
                if (tab.TextEditor == TextEditor)
                    tab.Content = TextEditor.Text;
            };
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OpenedFileTab tab && tab.TextEditor == TextEditor)
            {
                tab.Content = TextEditor.Text;
                tab.TextEditor = null;
            }
        }
    }
}
