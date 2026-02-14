using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using KID.Models;
using KID.Services.DI;
using KID.Services.CodeEditor.Interfaces;
using KID.Services.Files.Interfaces;
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
        private readonly IWindowConfigurationService windowConfigurationService;
        private readonly ICodeFileService codeFileService;
        private readonly ICodeEditorFactory codeEditorFactory;
        private readonly ILocalizationService localizationService;

        private int indexOfActiveFile;

        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        public ObservableCollection<OpenedFileTab> OpenedFiles { get; } = new();

        /// <summary>
        /// Активная вкладка.
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
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    RaiseTabCommandsCanExecute();
                }
            }
        }

        public CodeEditorsViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeFileService codeFileService,
            ILocalizationService localizationService,
            ICodeEditorFactory codeEditorFactory)
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.codeFileService = codeFileService ?? throw new ArgumentNullException(nameof(codeFileService));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            this.codeEditorFactory = codeEditorFactory ?? throw new ArgumentNullException(nameof(codeEditorFactory));

            windowConfigurationService.FontSettingsChanged += OnFontSettingsChanged;

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
            var normalizedPath = path ?? codeFileService.NewFilePath;
            var existing = FindTabByPath(normalizedPath);
            if (existing != null && !(codeFileService.IsNewFilePath(normalizedPath) && existing.IsModified))
            {
                ActiveFile = existing;
                return;
            }

            var codeEditor = codeEditorFactory.Create(content, windowConfigurationService.Settings.ProgrammingLanguage);
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
                    OnPropertyChanged(nameof(ActiveFile));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    RaiseTabCommandsCanExecute();
                }
            };

            OpenedFiles.Add(tab);
            indexOfActiveFile = OpenedFiles.Count - 1;
            OnPropertyChanged(nameof(ActiveFile));
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
                var templateCode = windowConfigurationService?.Settings?.TemplateCode ?? string.Empty;
                AddFile(codeFileService.NewFilePath, templateCode);
            }
            else
            {
                if (indexOfActiveFile >= OpenedFiles.Count)
                    indexOfActiveFile = OpenedFiles.Count - 1;
                else if (index < indexOfActiveFile)
                    indexOfActiveFile--;
                OnPropertyChanged(nameof(ActiveFile));
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

        /// <inheritdoc />
        public FontFamily FontFamily => new FontFamily(
            windowConfigurationService.Settings.FontFamily ?? "Consolas");

        /// <inheritdoc />
        public double FontSize => windowConfigurationService.Settings.FontSize > 0
            ? windowConfigurationService.Settings.FontSize
            : 14.0;

        public bool CanUndo => ActiveFile?.CodeEditor?.CanUndo ?? false;

        public bool CanRedo => ActiveFile?.CodeEditor?.CanRedo ?? false;

        public RelayCommand UndoCommand { get; }
        public RelayCommand RedoCommand { get; }
        public RelayCommand<OpenedFileTab> CloseFileCommand { get; }
        public RelayCommand<OpenedFileTab> SelectFileCommand { get; }
        public RelayCommand<OpenedFileTab> SaveFileCommand { get; }
        public RelayCommand<OpenedFileTab> SaveAsFileCommand { get; }
        public RelayCommand<OpenedFileTab> SaveAndSetAsTemplateCommand { get; }
        public RelayCommand<OpenedFileTab> MoveTabLeftCommand { get; }
        public RelayCommand<OpenedFileTab> MoveTabRightCommand { get; }

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

        private void ShowSaveError(Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                string.Format(localizationService.GetString("Error_FileSaveFailed") ?? "Failed to save: {0}", ex.Message),
                localizationService.GetString("Error_Title") ?? "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
        }

        private async void ExecuteSaveAndSetAsTemplate(OpenedFileTab tab)
        {
            try
            {
                await ExecuteSaveAndSetAsTemplateAsync(tab);
            }
            catch (Exception ex)
            {
                ShowSaveError(ex);
            }
        }

        private async Task ExecuteSaveAndSetAsTemplateAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null ||
                windowConfigurationService?.Settings == null || localizationService == null)
                return;

            var content = GetTabContent(tab);
            if (string.IsNullOrEmpty(content))
                return;

            if (codeFileService.IsNewFilePath(tab.FilePath))
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
            try
            {
                await ExecuteSaveFileAsync(tab);
            }
            catch (Exception ex)
            {
                ShowSaveError(ex);
            }
        }

        private async Task ExecuteSaveFileAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null)
                return;

            var content = GetTabContent(tab);
            if (string.IsNullOrEmpty(content))
                return;

            if (codeFileService.IsNewFilePath(tab.FilePath))
            {
                await ExecuteSaveAsFileAsync(tab);
                return;
            }

            await codeFileService.SaveToPathAsync(tab.FilePath, content);
            tab.UpdateSavedContent(content);
        }

        private async void ExecuteSaveAsFile(OpenedFileTab tab)
        {
            try
            {
                await ExecuteSaveAsFileAsync(tab);
            }
            catch (Exception ex)
            {
                ShowSaveError(ex);
            }
        }

        private async Task ExecuteSaveAsFileAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null || localizationService == null)
                return;

            var content = GetTabContent(tab);
            if (string.IsNullOrEmpty(content))
                return;

            var defaultFileName = codeFileService.IsNewFilePath(tab.FilePath)
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
            RaiseMoveTabCommandsCanExecute();
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
            RaiseMoveTabCommandsCanExecute();
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

        private void OnFontSettingsChanged(object? sender, EventArgs e)
        {
            var settings = windowConfigurationService?.Settings;
            if (settings == null) return;

            var fontFamily = new FontFamily(settings.FontFamily ?? "Consolas");
            var fontSize = settings.FontSize > 0 ? settings.FontSize : 14.0;

            foreach (var tab in OpenedFiles)
            {
                if (tab.CodeEditor != null)
                {
                    tab.CodeEditor.FontFamily = fontFamily;
                    tab.CodeEditor.FontSize = fontSize;
                }
            }
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(FontSize));
        }

        private void RaiseTabCommandsCanExecute()
        {
            SaveFileCommand.RaiseCanExecuteChanged();
            SaveAsFileCommand.RaiseCanExecuteChanged();
            SaveAndSetAsTemplateCommand.RaiseCanExecuteChanged();
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        private void RaiseMoveTabCommandsCanExecute()
        {
            MoveTabLeftCommand.RaiseCanExecuteChanged();
            MoveTabRightCommand.RaiseCanExecuteChanged();
        }

        private static string GetTabContent(OpenedFileTab tab) =>
            tab.CodeEditor?.Text ?? tab.Content ?? string.Empty;

        private OpenedFileTab? FindTabByPath(string path)
        {
            foreach (var tab in OpenedFiles)
            {
                if (string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    return tab;
            }
            return null;
        }

        /// <inheritdoc />
        public void NotifyActiveFileSaved(string content)
        {
            ActiveFile?.UpdateSavedContent(content);
            OnPropertyChanged(nameof(ActiveFile));
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
