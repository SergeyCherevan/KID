using KID.ViewModels.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KID.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private WindowState windowState = WindowState.Normal;
        public WindowState WindowState
        {
            get => windowState;
            set
            {
                if (SetProperty(ref windowState, value))
                {
                    MaximizeButtonContent = value == WindowState.Maximized ? "❐" : "☐";
                }
            }
        }

        private string maximizeButtonContent = "☐";
        public string MaximizeButtonContent
        {
            get => maximizeButtonContent;
            private set => SetProperty(ref maximizeButtonContent, value);
        }

        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DragMoveCommand { get; }

        public event Action RequestDragMove;

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
