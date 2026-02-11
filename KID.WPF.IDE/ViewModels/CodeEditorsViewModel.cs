using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using KID.Models;
using KID.Services.DI;
using KID.Services.Initialize;
using KID.Services.Initialize.Interfaces;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;

namespace KID.ViewModels
{
    /// <summary>
    /// ViewModel для панели редакторов кода с вкладками.
    /// </summary>
    public class CodeEditorsViewModel : ViewModelBase, ICodeEditorsViewModel
    {
        /// <summary>
        /// Сервис для работы с настройками окна.
        /// </summary>
        private readonly IWindowConfigurationService windowConfigurationService;

        /// <summary>
        /// Путь для нового несохранённого файла.
        /// </summary>
        public const string NewFilePath = "/NewFile.cs";

        private int indexOfActiveFile;

        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        public ObservableCollection<OpenedFileTab> OpenedFiles { get; } = new();

        /// <summary>
        /// Активная вкладка. Делегирует Text, FilePath, CanUndo, CanRedo.
        /// </summary>
        public OpenedFileTab? ActiveFile
        {
            get => OpenedFiles.Count > 0 && indexOfActiveFile >= 0 && indexOfActiveFile < OpenedFiles.Count
                ? OpenedFiles[indexOfActiveFile]
                : null;
            set
            {
                var newIndex = value != null ? OpenedFiles.IndexOf(value) : 0;
                if (newIndex < 0) newIndex = 0;

                if (SetProperty(ref indexOfActiveFile, newIndex))
                {
                    UpdateIsActiveForAllTabs();
                    OnPropertyChanged(nameof(Text));
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(CodeEditor));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// CodeEditor активной вкладки (для обратной совместимости с меню).
        /// </summary>
        public TextEditor? CodeEditor => ActiveFile?.CodeEditor;

        public CodeEditorsViewModel(IWindowConfigurationService windowConfigurationService)
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
            CloseFileCommand = new RelayCommand<OpenedFileTab>(ExecuteCloseFile);
            SelectFileCommand = new RelayCommand<OpenedFileTab>(ExecuteSelectFile);
        }

        /// <inheritdoc />
        public void AddFile(string path, string content)
        {
            var normalizedPath = path ?? NewFilePath;
            var existing = FindTabByPath(normalizedPath);
            if (existing != null)
            {
                ActiveFile = existing;
                return;
            }

            var codeEditor = CreateCodeEditor(content, windowConfigurationService.Settings);
            var tab = new OpenedFileTab
            {
                FilePath = normalizedPath,
                Content = content,
                CodeEditor = codeEditor
            };

            codeEditor.TextChanged += (s, e) =>
            {
                if (tab == ActiveFile)
                {
                    OnPropertyChanged(nameof(Text));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    CommandManager.InvalidateRequerySuggested();
                }
            };

            OpenedFiles.Add(tab);
            indexOfActiveFile = OpenedFiles.Count - 1;
            UpdateIsActiveForAllTabs();
            OnPropertyChanged(nameof(ActiveFile));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(CodeEditor));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        /// <inheritdoc />
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
            else
            {
                if (indexOfActiveFile >= OpenedFiles.Count)
                    indexOfActiveFile = OpenedFiles.Count - 1;
                else if (index < indexOfActiveFile)
                    indexOfActiveFile--;
                UpdateIsActiveForAllTabs();
                OnPropertyChanged(nameof(ActiveFile));
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(FilePath));
                OnPropertyChanged(nameof(CodeEditor));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        /// <inheritdoc />
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
            get => ActiveFile?.CodeEditor?.FontFamily ?? new FontFamily("Consolas");
            set
            {
                foreach (var tab in OpenedFiles)
                {
                    if (tab.CodeEditor != null)
                        tab.CodeEditor.FontFamily = value;
                }
            }
        }

        public double FontSize
        {
            get => ActiveFile?.CodeEditor?.FontSize ?? 14.0;
            set
            {
                foreach (var tab in OpenedFiles)
                {
                    if (tab.CodeEditor != null)
                        tab.CodeEditor.FontSize = value;
                }
            }
        }

        public bool CanUndo => ActiveFile?.CodeEditor?.CanUndo ?? false;

        public bool CanRedo => ActiveFile?.CodeEditor?.CanRedo ?? false;

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand CloseFileCommand { get; }
        public ICommand SelectFileCommand { get; }

        private void ExecuteUndo()
        {
            if (ActiveFile?.CodeEditor?.CanUndo == true)
                ActiveFile.CodeEditor.Undo();
        }

        private void ExecuteRedo()
        {
            if (ActiveFile?.CodeEditor?.CanRedo == true)
                ActiveFile.CodeEditor.Redo();
        }

        private void ExecuteCloseFile(OpenedFileTab tab) => CloseFile(tab);

        private void ExecuteSelectFile(OpenedFileTab tab) => SelectFile(tab);

        private void UpdateIsActiveForAllTabs()
        {
            var active = ActiveFile;
            foreach (var tab in OpenedFiles)
                tab.IsActive = (tab == active);
        }

        private static TextEditor CreateCodeEditor(string content, WindowConfigurationData settings)
        {
            var codeEditor = new TextEditor
            {
                ShowLineNumbers = true,
                WordWrap = true,
                Text = content ?? string.Empty,
                FontSize = settings.FontSize,
                FontFamily = new FontFamily(settings.FontFamily),
                SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition(settings.ProgrammingLanguage),
            };

            try
            {
                codeEditor.Background = (Brush)Application.Current.FindResource("EditorBackgroundBrush");
                codeEditor.Foreground = (Brush)Application.Current.FindResource("EditorForegroundBrush");
            }
            catch
            {
                // Ресурсы темы могут быть ещё не загружены
            }

            return codeEditor;
        }

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
            if (ActiveFile?.CodeEditor != null)
                return ActiveFile.CodeEditor.Text;
            return ActiveFile?.Content ?? string.Empty;
        }

        private void SetActiveContent(string value)
        {
            if (ActiveFile?.CodeEditor != null)
                ActiveFile.CodeEditor.Text = value;
            else if (ActiveFile != null)
                ActiveFile.Content = value ?? string.Empty;
        }

        /// <inheritdoc />
        public void SetSyntaxHighlighting(string language)
        {
            if (ActiveFile?.CodeEditor == null || string.IsNullOrEmpty(language))
                return;

            ActiveFile.CodeEditor.SyntaxHighlighting =
                ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition(language);
        }
    }
}
