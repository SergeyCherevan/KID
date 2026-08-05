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
            set
            {
                if (SetProperty(ref filePath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
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
            OnPropertyChanged(nameof(DisplayName));
        }

        /// <summary>
        /// Обновляет SavedContent после успешного сохранения.
        /// </summary>
        public void UpdateSavedContent(string content)
        {
            SavedContent = content ?? string.Empty;
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(DisplayName));
        }

        /// <summary>
        /// Отменяет несохранённые изменения, возвращая редактор к последней сохранённой версии.
        /// </summary>
        public void RestoreSavedContent()
        {
            if (CodeEditor != null)
                CodeEditor.Text = SavedContent;
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

        /// <summary>
        /// Имя вкладки со звёздочкой при наличии несохранённых изменений.
        /// </summary>
        public string DisplayName => IsModified ? $"{FileName}*" : FileName;
    }
}
