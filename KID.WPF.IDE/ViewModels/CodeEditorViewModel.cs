using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using KID.Models;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;

namespace KID.ViewModels
{
    public class CodeEditorViewModel : ViewModelBase, ICodeEditorViewModel
    {
        /// <summary>
        /// Путь для нового несохранённого файла.
        /// </summary>
        public const string NewFilePath = "/NewFile.cs";

        private OpenedFileTab? activeFile;

        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        public ObservableCollection<OpenedFileTab> OpenedFiles { get; } = new();

        /// <summary>
        /// Активная вкладка. Делегирует Text, FilePath, CanUndo, CanRedo.
        /// </summary>
        public OpenedFileTab? ActiveFile
        {
            get => activeFile;
            set
            {
                if (SetProperty(ref activeFile, value))
                {
                    OnPropertyChanged(nameof(Text));
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(TextEditor));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Текущий TextEditor активной вкладки (для обратной совместимости с меню).
        /// </summary>
        public TextEditor? TextEditor => ActiveFile?.TextEditor;

        public CodeEditorViewModel()
        {
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
            CloseFileCommand = new RelayCommand<OpenedFileTab>(ExecuteCloseFile);
            SelectFileCommand = new RelayCommand<OpenedFileTab>(ExecuteSelectFile);
        }

        /// <inheritdoc />
        public void Initialize(TextEditor editor)
        {
            // Устаревший метод: многофайловая система создаёт редакторы через EditorTabContent.
            // Оставляем для совместимости с интерфейсом.
        }

        /// <summary>
        /// Добавляет файл в новую вкладку или переключается на уже открытый.
        /// </summary>
        public void AddFile(string path, string content)
        {
            var normalizedPath = path ?? NewFilePath;
            var existing = FindTabByPath(normalizedPath);
            if (existing != null)
            {
                ActiveFile = existing;
                return;
            }

            var tab = new OpenedFileTab
            {
                FilePath = normalizedPath,
                Content = content
            };
            tab.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OpenedFileTab.Content) ||
                    e.PropertyName == nameof(OpenedFileTab.TextEditor))
                {
                    if (s == ActiveFile)
                    {
                        OnPropertyChanged(nameof(Text));
                        OnPropertyChanged(nameof(CanUndo));
                        OnPropertyChanged(nameof(CanRedo));
                    }
                }
            };

            OpenedFiles.Add(tab);
            ActiveFile = tab;
        }

        /// <summary>
        /// Закрывает вкладку. При закрытии последней создаёт новую пустую.
        /// </summary>
        public void CloseFile(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab))
                return;

            var index = OpenedFiles.IndexOf(tab);
            OpenedFiles.Remove(tab);

            if (OpenedFiles.Count == 0)
            {
                AddFile(NewFilePath, string.Empty);
            }
            else if (ActiveFile == tab)
            {
                var newIndex = Math.Min(index, OpenedFiles.Count - 1);
                ActiveFile = OpenedFiles[newIndex];
            }
        }

        /// <summary>
        /// Делает вкладку активной.
        /// </summary>
        public void SelectFile(OpenedFileTab tab)
        {
            if (tab != null && OpenedFiles.Contains(tab))
                ActiveFile = tab;
        }

        public string Text
        {
            get => GetActiveContent();
            set => SetActiveContent(value);
        }

        /// <inheritdoc />
        public string FilePath
        {
            get => ActiveFile?.FilePath ?? NewFilePath;
            set
            {
                if (ActiveFile != null)
                    ActiveFile.FilePath = value ?? NewFilePath;
            }
        }

        public FontFamily FontFamily
        {
            get => ActiveFile?.TextEditor?.FontFamily ?? new FontFamily("Consolas");
            set
            {
                if (ActiveFile?.TextEditor != null)
                    ActiveFile.TextEditor.FontFamily = value;
            }
        }

        public double FontSize
        {
            get => ActiveFile?.TextEditor?.FontSize ?? 12.0;
            set
            {
                if (ActiveFile?.TextEditor != null)
                    ActiveFile.TextEditor.FontSize = value;
            }
        }

        public bool CanUndo => ActiveFile?.TextEditor?.CanUndo ?? false;

        public bool CanRedo => ActiveFile?.TextEditor?.CanRedo ?? false;

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand CloseFileCommand { get; }
        public ICommand SelectFileCommand { get; }

        private void ExecuteUndo()
        {
            if (ActiveFile?.TextEditor?.CanUndo == true)
                ActiveFile.TextEditor.Undo();
        }

        private void ExecuteRedo()
        {
            if (ActiveFile?.TextEditor?.CanRedo == true)
                ActiveFile.TextEditor.Redo();
        }

        private void ExecuteCloseFile(OpenedFileTab tab) => CloseFile(tab);

        private void ExecuteSelectFile(OpenedFileTab tab) => SelectFile(tab);

        private OpenedFileTab? FindTabByPath(string path)
        {
            foreach (var tab in OpenedFiles)
            {
                if (string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    return tab;
            }
            return null;
        }

        private string GetActiveContent()
        {
            if (ActiveFile?.TextEditor != null)
                return ActiveFile.TextEditor.Text;
            return ActiveFile?.Content ?? string.Empty;
        }

        private void SetActiveContent(string value)
        {
            if (ActiveFile?.TextEditor != null)
                ActiveFile.TextEditor.Text = value;
            else if (ActiveFile != null)
                ActiveFile.Content = value ?? string.Empty;
        }

        /// <inheritdoc />
        public void SetSyntaxHighlighting(string language)
        {
            if (ActiveFile?.TextEditor == null || string.IsNullOrEmpty(language))
                return;

            ActiveFile.TextEditor.SyntaxHighlighting =
                ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition(language);
        }
    }
}
