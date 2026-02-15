using ICSharpCode.AvalonEdit;
using KID.ViewModels.Infrastructure;

namespace KID.Models
{
    /// <summary>
    /// Модель вкладки открытого файла в редакторе кода.
    /// </summary>
    public class OpenedFileTab : ViewModelBase
    {
        private string filePath = string.Empty;
        private string savedContent = string.Empty;
        private TextEditor? codeEditor;

        /// <summary>
        /// Путь к файлу. Для нового файла — /NewFile.cs.
        /// </summary>
        public string FilePath
        {
            get => filePath;
            set => SetProperty(ref filePath, value ?? string.Empty);
        }

        /// <summary>
        /// Текст на момент последнего сохранения или открытия. Для NewFile — шаблон по умолчанию.
        /// </summary>
        public string SavedContent
        {
            get => savedContent;
            private set => SetProperty(ref savedContent, value ?? string.Empty);
        }

        /// <summary>
        /// Текущее содержимое вкладки.
        /// </summary>
        public string CurrentContent => CodeEditor?.Text ?? string.Empty;

        /// <summary>
        /// true, если текущее содержимое отличается от SavedContent (есть несохранённые изменения).
        /// </summary>
        public bool IsModified => (CodeEditor?.Text ?? string.Empty) != SavedContent;

        /// <summary>
        /// Вызывается при изменении текста в редакторе для обновления IsModified.
        /// </summary>
        public void NotifyContentChanged()
        {
            OnPropertyChanged(nameof(IsModified));
        }

        /// <summary>
        /// Обновляет SavedContent после успешного сохранения.
        /// </summary>
        public void UpdateSavedContent(string content)
        {
            SavedContent = content ?? string.Empty;
            OnPropertyChanged(nameof(IsModified));
        }

        /// <summary>
        /// Экземпляр AvalonEdit, создаётся в AddFile() и присваивается вкладке.
        /// </summary>
        public TextEditor? CodeEditor
        {
            get => codeEditor;
            set => SetProperty(ref codeEditor, value);
        }

        /// <summary>
        /// Имя файла для отображения во вкладке (без пути).
        /// </summary>
        public string FileName => System.IO.Path.GetFileName(FilePath);
    }
}
