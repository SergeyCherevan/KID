using ICSharpCode.AvalonEdit;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using KID.Models;

namespace KID.ViewModels.Interfaces
{
    /// <summary>
    /// Интерфейс ViewModel для панели редакторов кода с вкладками.
    /// </summary>
    public interface ICodeEditorsViewModel
    {
        /// <summary>
        /// CodeEditor активной вкладки (для совместимости с меню).
        /// </summary>
        TextEditor? CodeEditor { get; }

        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        ObservableCollection<OpenedFileTab> OpenedFiles { get; }

        /// <summary>
        /// Активная вкладка.
        /// </summary>
        OpenedFileTab? ActiveFile { get; set; }

        string Text { get; set; }
        string FilePath { get; set; }
        FontFamily FontFamily { get; set; }
        double FontSize { get; set; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        bool HasUnsavedChanges { get; }

        ICommand UndoCommand { get; }
        ICommand RedoCommand { get; }
        ICommand CloseFileCommand { get; }
        ICommand SelectFileCommand { get; }
        ICommand SaveFileCommand { get; }
        ICommand SaveAsFileCommand { get; }
        ICommand SaveAndSetAsTemplateCommand { get; }
        ICommand MoveTabLeftCommand { get; }
        ICommand MoveTabRightCommand { get; }

        void SetSyntaxHighlighting(string language);

        /// <summary>
        /// Добавляет файл в новую вкладку или переключается на уже открытый.
        /// </summary>
        void AddFile(string path, string content);

        /// <summary>
        /// Закрывает вкладку.
        /// </summary>
        void CloseFile(OpenedFileTab tab);

        /// <summary>
        /// Делает вкладку активной.
        /// </summary>
        void SelectFile(OpenedFileTab tab);

        /// <summary>
        /// Уведомляет о сохранении активного файла (обновляет SavedContent).
        /// </summary>
        void NotifyActiveFileSaved(string content);
    }
}
