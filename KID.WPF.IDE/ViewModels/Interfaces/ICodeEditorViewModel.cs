using ICSharpCode.AvalonEdit;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using KID.Models;

namespace KID.ViewModels.Interfaces
{
    public interface ICodeEditorViewModel
    {
        TextEditor? TextEditor { get; }

        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        ObservableCollection<OpenedFileTab> OpenedFiles { get; }

        /// <summary>
        /// Активная вкладка.
        /// </summary>
        OpenedFileTab? ActiveFile { get; set; }

        void Initialize(ICSharpCode.AvalonEdit.TextEditor editor);

        string Text { get; set; }
        string FilePath { get; set; }
        FontFamily FontFamily { get; set; }
        double FontSize { get; set; }
        bool CanUndo { get; }
        bool CanRedo { get; }

        ICommand UndoCommand { get; }
        ICommand RedoCommand { get; }
        ICommand CloseFileCommand { get; }
        ICommand SelectFileCommand { get; }

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
    }
}
