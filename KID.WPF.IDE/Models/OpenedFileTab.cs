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
        private string content = string.Empty;
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
        /// Текст содержимого файла (fallback до загрузки CodeEditor).
        /// </summary>
        public string Content
        {
            get => content;
            set => SetProperty(ref content, value ?? string.Empty);
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
