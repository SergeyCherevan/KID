using System;
using System.Collections.Generic;
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
using KID.ViewModels;
using KID.ViewModels.Interfaces;

namespace KID.Views
{
    /// <summary>
    /// Логика взаимодействия для CodeEditorView.xaml
    /// </summary>
    public partial class CodeEditorView : UserControl
    {
        public CodeEditorView()
        {
            InitializeComponent();
            if (DataContext is ICodeEditorViewModel vm)
            {
                vm.Initialize(TextEditor);
            }
        }
    }
}
