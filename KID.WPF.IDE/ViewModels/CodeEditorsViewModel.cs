using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using KID.Models;
using KID.Services.DI;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
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
        /// Сервис для работы с файлами кода.
        /// </summary>
        private readonly ICodeFileService codeFileService;

        /// <summary>
        /// Сервис локализации.
        /// </summary>
        private readonly ILocalizationService localizationService;

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
                    OnPropertyChanged(nameof(Text));
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(CodeEditor));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// CodeEditor активной вкладки (для обратной совместимости с меню).
        /// </summary>
        public TextEditor? CodeEditor => ActiveFile?.CodeEditor;

        public CodeEditorsViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeFileService codeFileService,
            ILocalizationService localizationService)
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.codeFileService = codeFileService ?? throw new ArgumentNullException(nameof(codeFileService));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
            CloseFileCommand = new RelayCommand<OpenedFileTab>(ExecuteCloseFile);
            SelectFileCommand = new RelayCommand<OpenedFileTab>(ExecuteSelectFile);
            SaveFileCommand = new RelayCommand<OpenedFileTab>(ExecuteSaveFile, CanSaveTab);
            SaveAsFileCommand = new RelayCommand<OpenedFileTab>(ExecuteSaveAsFile);
            SaveAndSetAsTemplateCommand = new RelayCommand<OpenedFileTab>(ExecuteSaveAndSetAsTemplate, CanSaveAndSetAsTemplate);
            MoveTabLeftCommand = new RelayCommand<OpenedFileTab>(ExecuteMoveTabLeft, CanMoveTabLeft);
            MoveTabRightCommand = new RelayCommand<OpenedFileTab>(ExecuteMoveTabRight, CanMoveTabRight);
        }

        /// <inheritdoc />
        public void AddFile(string path, string content)
        {
            var normalizedPath = path ?? NewFilePath;
            var existing = FindTabByPath(normalizedPath);
            if (existing != null && !(normalizedPath == NewFilePath && existing.IsModified))
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
            tab.UpdateSavedContent(content);

            codeEditor.TextChanged += (s, e) =>
            {
                tab.NotifyContentChanged();
                if (tab == ActiveFile)
                {
                    OnPropertyChanged(nameof(Text));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    CommandManager.InvalidateRequerySuggested();
                }
            };

            OpenedFiles.Add(tab);
            indexOfActiveFile = OpenedFiles.Count - 1;
            OnPropertyChanged(nameof(ActiveFile));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(CodeEditor));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasUnsavedChanges));
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
                var templateCode = windowConfigurationService?.Settings?.TemplateCode ?? string.Empty;
                AddFile(NewFilePath, templateCode);
            }
            else
            {
                if (indexOfActiveFile >= OpenedFiles.Count)
                    indexOfActiveFile = OpenedFiles.Count - 1;
                else if (index < indexOfActiveFile)
                    indexOfActiveFile--;
                OnPropertyChanged(nameof(ActiveFile));
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(FilePath));
                OnPropertyChanged(nameof(CodeEditor));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                OnPropertyChanged(nameof(HasUnsavedChanges));
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

        /// <summary>
        /// true, если в активной вкладке есть несохранённые изменения.
        /// </summary>
        public bool HasUnsavedChanges => ActiveFile?.IsModified ?? false;

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand CloseFileCommand { get; }
        public ICommand SelectFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand SaveAsFileCommand { get; }
        public ICommand SaveAndSetAsTemplateCommand { get; }
        public ICommand MoveTabLeftCommand { get; }
        public ICommand MoveTabRightCommand { get; }

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

        private static bool CanSaveTab(OpenedFileTab? tab) => tab?.IsModified == true;

        private static bool CanSaveAndSetAsTemplate(OpenedFileTab? tab) =>
            tab != null && !string.IsNullOrEmpty(GetTabContent(tab));

        private async void ExecuteSaveAndSetAsTemplate(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null ||
                windowConfigurationService?.Settings == null || localizationService == null)
                return;

            var content = GetTabContent(tab);
            if (string.IsNullOrEmpty(content))
                return;

            if (IsNewFilePath(tab.FilePath))
            {
                var defaultFileName = "NewFile.cs";
                var fileFilter = localizationService.GetString("FileFilter_CSharp") ?? "C# Files (*.cs)|*.cs|All Files (*.*)|*.*";
                var savedPath = await codeFileService.SaveCodeFileAsync(content, fileFilter, defaultFileName);
                if (savedPath == null)
                    return;
                tab.FilePath = savedPath;
                tab.UpdateSavedContent(content);
            }
            else if (tab.IsModified)
            {
                await codeFileService.SaveToPathAsync(tab.FilePath, content);
                tab.UpdateSavedContent(content);
            }

            windowConfigurationService.Settings.TemplateCode = content;
            windowConfigurationService.Settings.TemplateName = tab.FilePath;
            windowConfigurationService.SaveSettings();
        }

        private async void ExecuteSaveFile(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null)
                return;

            var content = GetTabContent(tab);
            if (string.IsNullOrEmpty(content))
                return;

            if (IsNewFilePath(tab.FilePath))
            {
                ExecuteSaveAsFile(tab);
                return;
            }

            await codeFileService.SaveToPathAsync(tab.FilePath, content);
            tab.UpdateSavedContent(content);
        }

        private async void ExecuteSaveAsFile(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null || localizationService == null)
                return;

            var content = GetTabContent(tab);
            if (string.IsNullOrEmpty(content))
                return;

            var defaultFileName = IsNewFilePath(tab.FilePath)
                ? "NewFile.cs"
                : Path.GetFileName(tab.FilePath);

            var fileFilter = localizationService.GetString("FileFilter_CSharp") ?? "C# Files (*.cs)|*.cs|All Files (*.*)|*.*";
            var savedPath = await codeFileService.SaveCodeFileAsync(content, fileFilter, defaultFileName);
            if (savedPath != null)
            {
                tab.FilePath = savedPath;
                tab.UpdateSavedContent(content);
            }
        }

        private bool CanMoveTabLeft(OpenedFileTab? tab) =>
            tab != null && OpenedFiles.Contains(tab) && OpenedFiles.IndexOf(tab) > 0;

        private bool CanMoveTabRight(OpenedFileTab? tab) =>
            tab != null && OpenedFiles.Contains(tab) && OpenedFiles.IndexOf(tab) < OpenedFiles.Count - 1;

        private void ExecuteMoveTabLeft(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab))
                return;

            var index = OpenedFiles.IndexOf(tab);
            if (index <= 0)
                return;

            OpenedFiles.Move(index, index - 1);
            UpdateActiveFileIndexAfterMove(index, index - 1);
            CommandManager.InvalidateRequerySuggested();
        }

        private void ExecuteMoveTabRight(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab))
                return;

            var index = OpenedFiles.IndexOf(tab);
            if (index < 0 || index >= OpenedFiles.Count - 1)
                return;

            OpenedFiles.Move(index, index + 1);
            UpdateActiveFileIndexAfterMove(index, index + 1);
            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateActiveFileIndexAfterMove(int oldIndex, int newIndex)
        {
            if (indexOfActiveFile == oldIndex)
                indexOfActiveFile = newIndex;
            else if (oldIndex < indexOfActiveFile && newIndex >= indexOfActiveFile)
                indexOfActiveFile--;
            else if (oldIndex > indexOfActiveFile && newIndex <= indexOfActiveFile)
                indexOfActiveFile++;
            OnPropertyChanged(nameof(ActiveFile));
        }

        private static string GetTabContent(OpenedFileTab tab) =>
            tab.CodeEditor?.Text ?? tab.Content ?? string.Empty;

        private static bool IsNewFilePath(string path) =>
            path.EndsWith("NewFile.cs", StringComparison.OrdinalIgnoreCase) ||
            path == "/NewFile.cs";

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
        public void NotifyActiveFileSaved(string content)
        {
            ActiveFile?.UpdateSavedContent(content);
            OnPropertyChanged(nameof(HasUnsavedChanges));
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
