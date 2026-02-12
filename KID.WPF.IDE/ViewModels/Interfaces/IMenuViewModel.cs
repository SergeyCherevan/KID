using KID.ViewModels.Infrastructure;
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
        RelayCommand SaveFileCommand { get; }
        ICommand SaveAsFileCommand { get; }
        RelayCommand SaveAndSetAsTemplateCommand { get; }
        RelayCommand RunCommand { get; }
        RelayCommand StopCommand { get; }
        RelayCommand UndoCommand { get; }
        RelayCommand RedoCommand { get; }
        ICommand ChangeLanguageCommand { get; }
        ICommand ChangeThemeCommand { get; }
        ICommand ChangeFontCommand { get; }
        ICommand ChangeFontSizeCommand { get; }
    }
}
