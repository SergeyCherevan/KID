using System.Collections.ObjectModel;
using KID.Models;
using KID.ViewModels.Infrastructure;
using System.Windows.Input;

namespace KID.ViewModels.Interfaces
{
    public interface IMenuViewModel
    {
        bool CanStop { get; set; }
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
