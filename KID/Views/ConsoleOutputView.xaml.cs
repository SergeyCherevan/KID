using ICSharpCode.AvalonEdit;
using KID.ViewModels.Interfaces;
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

namespace KID.Views
{
    /// <summary>
    /// Логика взаимодействия для ConsoleOutputView.xaml
    /// </summary>
    public partial class ConsoleOutputView : UserControl
    {
        public ConsoleOutputView()
        {
            InitializeComponent();

            if (DataContext is IConsoleOutputViewModel vm)
            {
                vm.Initialize(ConsoleOutputControl);
            }
        }
    }
}
