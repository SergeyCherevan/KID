using System.Windows.Input;

namespace KID.ViewModels.Interfaces
{
    public interface IMenuViewModel
    {
        bool IsStopButtonEnabled { get; set; }
        bool CanUndo { get; }
        bool CanRedo { get; }

        ICommand NewFileCommand { get; }
        ICommand OpenFileCommand { get; }
        ICommand SaveFileCommand { get; }
        ICommand SaveAsFileCommand { get; }
        ICommand RunCommand { get; }
        ICommand StopCommand { get; }
        ICommand UndoCommand { get; }
        ICommand RedoCommand { get; }
    }
}
