using System.Collections.ObjectModel;
using KID.Models;
using KID.Services.CodeExecution;
using KID.ViewModels.Infrastructure;
using System.Windows.Input;

namespace KID.ViewModels.Interfaces
{
    public interface IMenuViewModel
    {
        ExecutionState ExecutionState { get; }
        bool IsExecutionActive { get; }
        bool CanRun { get; }
        bool CanRequestStop { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        ObservableCollection<ThemeDefinition> AvailableThemes { get; }

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
        RelayCommand<ThemeDefinition> ChangeThemeCommand { get; }
        ICommand ChangeFontCommand { get; }
        ICommand ChangeFontSizeCommand { get; }
    }
}
