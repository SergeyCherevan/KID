using System.Windows.Input;

namespace KID.ViewModels.Interfaces
{
    public interface IMenuViewModel
    {
        bool IsStopButtonEnabled { get; set; }

        ICommand NewFileCommand { get; }
        ICommand OpenFileCommand { get; }
        ICommand SaveFileCommand { get; }
        ICommand RunCommand { get; }
        ICommand StopCommand { get; }
        ICommand UndoCommand { get; }
        ICommand RedoCommand { get; }
    }
}
