using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KID.ViewModels
{
    public class MainViewModel : ViewModelBase, IMainViewModel
    {
        public WindowState WindowState
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                {
                    MaximizeButtonContent = value == WindowState.Maximized ? "❐" : "☐";
                }
            }
        } = WindowState.Normal;

        public string MaximizeButtonContent
        {
            get;
            private set => SetProperty(ref field, value);
        } = "☐";

        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DragMoveCommand { get; }

        public event Action? RequestDragMove;

        public MainViewModel()
        {
            MinimizeCommand = new RelayCommand(() => WindowState = WindowState.Minimized);
            MaximizeCommand = new RelayCommand(() =>
            {
                WindowState = WindowState == WindowState.Maximized 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            });
            CloseCommand = new RelayCommand<IClosable>(closable => closable?.Close());
            DragMoveCommand = new RelayCommand(() => RequestDragMove?.Invoke());
        }
    }
}
