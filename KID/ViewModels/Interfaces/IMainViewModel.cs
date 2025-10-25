using System.Windows;
using System.Windows.Input;

namespace KID.ViewModels.Interfaces
{
    public interface IMainViewModel
    {
        WindowState WindowState { get; set; }
        string MaximizeButtonContent { get; }
        ICommand MinimizeCommand { get; }
        ICommand MaximizeCommand { get; }
        ICommand CloseCommand { get; }
        ICommand DragMoveCommand { get; }
        event Action RequestDragMove;
    }
}
