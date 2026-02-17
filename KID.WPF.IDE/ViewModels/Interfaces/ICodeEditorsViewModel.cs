using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using KID.Models;
using KID.ViewModels.Infrastructure;

namespace KID.ViewModels.Interfaces
{
    /// <summary>
    /// Интерфейс ViewModel для панели редакторов кода с вкладками.
    /// </summary>
    public interface ICodeEditorsViewModel
    {
        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        ObservableCollection<OpenedFileTab> OpenedFiles { get; }

        /// <summary>
        /// Текущая вкладка.
        /// </summary>
        OpenedFileTab? CurrentFileTab { get; set; }

        FontFamily FontFamily { get; }
        double FontSize { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        RelayCommand UndoCommand { get; }
        RelayCommand RedoCommand { get; }
        RelayCommand<OpenedFileTab> CloseFileCommand { get; }
        RelayCommand<OpenedFileTab> SelectFileCommand { get; }
        RelayCommand<OpenedFileTab> SaveFileCommand { get; }
        RelayCommand<OpenedFileTab> SaveAsFileCommand { get; }
        RelayCommand<OpenedFileTab> SaveAndSetAsTemplateCommand { get; }
        RelayCommand<OpenedFileTab> MoveTabLeftCommand { get; }
        RelayCommand<OpenedFileTab> MoveTabRightCommand { get; }

        /// <summary>
        /// Добавляет файл в новую вкладку или переключается на уже открытый.
        /// </summary>
        void AddFileTab(string path, string content);

        /// <summary>
        /// Закрывает вкладку.
        /// </summary>
        void CloseFileTab(OpenedFileTab tab);

        /// <summary>
        /// Делает вкладку текущей.
        /// </summary>
        void SelectFileTab(OpenedFileTab tab);

        /// <summary>
        /// Уведомляет о сохранении текущей вкладки (обновляет SavedContent).
        /// </summary>
        void NotifyCurrentFileTabSaved(string content);
    }
}
