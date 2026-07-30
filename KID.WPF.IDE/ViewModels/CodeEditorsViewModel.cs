using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using KID.Models;
using KID.Services.CodeEditor.Interfaces;
using KID.Services.Errors.Interfaces;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Themes.Interfaces;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using RoslynPad.Editor;

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
        private readonly IClassificationHighlightColorsProvider classificationHighlightColorsProvider;
        private readonly IThemeService themeService;
        private readonly IAsyncOperationErrorHandler asyncOperationErrorHandler;

        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        public ObservableCollection<OpenedFileTab> OpenedFileTabs { get; } = new();

        private int indexOfCurrentFileTab;
        /// <summary>
        /// Текущая вкладка.
        /// </summary>
        public OpenedFileTab? CurrentFileTab
        {
            get => OpenedFileTabs.Count > 0 && indexOfCurrentFileTab >= 0 && indexOfCurrentFileTab < OpenedFileTabs.Count
                ? OpenedFileTabs[indexOfCurrentFileTab]
                : null;
            set
            {
                var newIndex = value != null ? OpenedFileTabs.IndexOf(value) : 0;
                if (newIndex < 0) newIndex = 0;

                if (SetProperty(ref indexOfCurrentFileTab, newIndex))
                {
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    RaiseTabCommandsCanExecute();
                }
            }
        }

        public FontFamily FontFamily => new FontFamily(
            windowConfigurationService.Settings.FontFamily ?? "Consolas");

        public double FontSize => windowConfigurationService.Settings.FontSize > 0
            ? windowConfigurationService.Settings.FontSize
            : 14.0;

        /// <inheritdoc />
        public IClassificationHighlightColors ClassificationHighlightColors =>
            classificationHighlightColorsProvider.GetColors();

        public bool CanUndo => CurrentFileTab?.CodeEditor?.CanUndo ?? false;

        public bool CanRedo => CurrentFileTab?.CodeEditor?.CanRedo ?? false;


        public RelayCommand UndoCommand { get; }
        public RelayCommand RedoCommand { get; }
        public RelayCommand<OpenedFileTab> CloseFileCommand { get; }
        public RelayCommand<OpenedFileTab> SelectFileCommand { get; }
        public RelayCommand<OpenedFileTab> SaveFileCommand { get; }
        public RelayCommand<OpenedFileTab> SaveAsFileCommand { get; }
        public RelayCommand<OpenedFileTab> SaveAndSetAsTemplateCommand { get; }
        public RelayCommand<OpenedFileTab> MoveTabLeftCommand { get; }
        public RelayCommand<OpenedFileTab> MoveTabRightCommand { get; }


        public CodeEditorsViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeFileService codeFileService,
            ICodeEditorFactory codeEditorFactory,
            IClassificationHighlightColorsProvider classificationHighlightColorsProvider,
            IThemeService themeService,
            IAsyncOperationErrorHandler asyncOperationErrorHandler
        )
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.codeFileService = codeFileService ?? throw new ArgumentNullException(nameof(codeFileService));
            this.codeEditorFactory = codeEditorFactory ?? throw new ArgumentNullException(nameof(codeEditorFactory));
            this.classificationHighlightColorsProvider = classificationHighlightColorsProvider ?? throw new ArgumentNullException(nameof(classificationHighlightColorsProvider));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.asyncOperationErrorHandler = asyncOperationErrorHandler ?? throw new ArgumentNullException(nameof(asyncOperationErrorHandler));

            windowConfigurationService.FontSettingsChanged += OnFontSettingsChanged;
            themeService.ThemeChanged += OnThemeChanged;

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


        private void ExecuteUndo()
        {
            if (CurrentFileTab?.CodeEditor?.CanUndo == true)
                CurrentFileTab.CodeEditor.Undo();
        }

        private void ExecuteRedo()
        {
            if (CurrentFileTab?.CodeEditor?.CanRedo == true)
                CurrentFileTab.CodeEditor.Redo();
        }

        private void ExecuteCloseFile(OpenedFileTab tab) => CloseFileTab(tab);

        private void ExecuteSelectFile(OpenedFileTab tab) => SelectFileTab(tab);

        private static bool CanSaveTab(OpenedFileTab? tab) => tab?.IsModified == true;

        private static bool CanSaveAndSetAsTemplate(OpenedFileTab? tab) =>
            tab != null && !string.IsNullOrEmpty(tab.CurrentContent);

        private async void ExecuteSaveAndSetAsTemplate(OpenedFileTab tab)
        {
            await asyncOperationErrorHandler.ExecuteAsync(
                () => ExecuteSaveAndSetAsTemplateAsync(tab),
                "Error_FileSaveFailed");
        }

        private async void ExecuteSaveFile(OpenedFileTab tab)
        {
            await asyncOperationErrorHandler.ExecuteAsync(
                () => ExecuteSaveFileAsync(tab),
                "Error_FileSaveFailed");
        }

        private async void ExecuteSaveAsFile(OpenedFileTab tab)
        {
            await asyncOperationErrorHandler.ExecuteAsync(
                () => ExecuteSaveAsFileAsync(tab),
                "Error_FileSaveFailed");
        }

        private bool CanMoveTabLeft(OpenedFileTab? tab) =>
            tab != null && OpenedFileTabs.Contains(tab) && OpenedFileTabs.IndexOf(tab) > 0;

        private bool CanMoveTabRight(OpenedFileTab? tab) =>
            tab != null && OpenedFileTabs.Contains(tab) && OpenedFileTabs.IndexOf(tab) < OpenedFileTabs.Count - 1;

        private void ExecuteMoveTabLeft(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFileTabs.Contains(tab))
                return;

            var index = OpenedFileTabs.IndexOf(tab);
            if (index <= 0)
                return;

            OpenedFileTabs.Move(index, index - 1);
            UpdateCurrentFileTabIndexAfterMove(index, index - 1);
            RaiseMoveTabCommandsCanExecute();
        }

        private void ExecuteMoveTabRight(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFileTabs.Contains(tab))
                return;

            var index = OpenedFileTabs.IndexOf(tab);
            if (index < 0 || index >= OpenedFileTabs.Count - 1)
                return;

            OpenedFileTabs.Move(index, index + 1);
            UpdateCurrentFileTabIndexAfterMove(index, index + 1);
            RaiseMoveTabCommandsCanExecute();
        }

        /*
         * Зачем нужен этот метод
         * ----------------------
         * Текущая вкладка хранится не ссылкой на объект, а индексом `indexOfCurrentFileTab`.
         * Свойство `CurrentFileTab` вычисляется как `OpenedFileTabs[indexOfCurrentFileTab]`.
         *
         * Когда мы меняем порядок вкладок через `OpenedFileTabs.Move(oldIndex, newIndex)`:
         * - перемещаемая вкладка меняет свой индекс;
         * - все вкладки между oldIndex и newIndex сдвигаются на 1 позицию.
         *
         * Поэтому после Move нужно скорректировать `indexOfCurrentFileTab`, иначе UI начнёт считать
         * "текущей" другую вкладку (или текущая останется той же, но её индекс окажется неверным).
         *
         * Принцип работы
         * --------------
         * Рассматриваем три сценария (метод корректен для любого Move, не только на 1 позицию):
         *
         * 1) Переместили текущую вкладку (indexOfCurrentFileTab == oldIndex)
         *    Тогда её новый индекс — это `newIndex`.
         *
         * 2) Переместили вкладку слева направо "через" текущую
         *    Условие: oldIndex < indexOfCurrentFileTab && newIndex >= indexOfCurrentFileTab
         *    Пример: [A, B, C, D], текущая C (2). Move B: 1 -> 3 => [A, C, D, B]
         *    Текущая C сдвигается на 1 влево: 2 -> 1, поэтому `indexOfCurrentFileTab--`.
         *
         * 3) Переместили вкладку справа налево "через" текущую
         *    Условие: oldIndex > indexOfCurrentFileTab && newIndex <= indexOfCurrentFileTab
         *    Пример: [A, B, C, D], текущая C (2). Move D: 3 -> 1 => [A, D, B, C]
         *    Текущая C сдвигается на 1 вправо: 2 -> 3, поэтому `indexOfCurrentFileTab++`.
         */
        private void UpdateCurrentFileTabIndexAfterMove(int oldIndex, int newIndex)
        {
            if (indexOfCurrentFileTab == oldIndex)
                indexOfCurrentFileTab = newIndex;
            else if (oldIndex < indexOfCurrentFileTab && newIndex >= indexOfCurrentFileTab)
                indexOfCurrentFileTab--;
            else if (oldIndex > indexOfCurrentFileTab && newIndex <= indexOfCurrentFileTab)
                indexOfCurrentFileTab++;

            OnPropertyChanged(nameof(CurrentFileTab));
        }


        public void CreateAndAddFileTab(string path, string content)
        {
            var normalizedPath = path ?? codeFileService.NewFilePath;

            var codeEditor = codeEditorFactory.Create(content, windowConfigurationService.Settings.ProgrammingLanguage);
            var tab = new OpenedFileTab
            {
                FilePath = normalizedPath,
                CodeEditor = codeEditor
            };
            tab.UpdateSavedContent(content);

            codeEditor.TextChanged += (s, e) =>
            {
                tab.NotifyContentChanged();
                if (tab == CurrentFileTab)
                {
                    OnPropertyChanged(nameof(CurrentFileTab));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    RaiseTabCommandsCanExecute();
                }
            };

            OpenedFileTabs.Add(tab);
            indexOfCurrentFileTab = OpenedFileTabs.Count - 1;
            OnPropertyChanged(nameof(CurrentFileTab));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        /// <inheritdoc />
        public void CloseFileTab(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFileTabs.Contains(tab))
                return;

            var index = OpenedFileTabs.IndexOf(tab);
            OpenedFileTabs.Remove(tab);

            if (OpenedFileTabs.Count == 0)
            {
                var templateCode = windowConfigurationService?.Settings?.TemplateCode ?? string.Empty;
                CreateAndAddFileTab(codeFileService.NewFilePath, templateCode);
            }
            else
            {
                if (indexOfCurrentFileTab >= OpenedFileTabs.Count)
                    indexOfCurrentFileTab = OpenedFileTabs.Count - 1;
                else if (index < indexOfCurrentFileTab)
                    indexOfCurrentFileTab--;
                OnPropertyChanged(nameof(CurrentFileTab));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        /// <inheritdoc />
        public void SelectFileTab(OpenedFileTab tab)
        {
            if (tab != null && OpenedFileTabs.Contains(tab))
                CurrentFileTab = tab;
        }

        private async Task ExecuteSaveAndSetAsTemplateAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFileTabs.Contains(tab) || codeFileService == null ||
                windowConfigurationService?.Settings == null)
                return;

            var content = tab.CurrentContent;
            if (string.IsNullOrEmpty(content))
                return;

            if (codeFileService.IsNewFilePath(tab.FilePath))
            {
                var defaultFileName = "NewFile.cs";
                var savedPath = await codeFileService.SaveCodeFileAsync(content, codeFileService.CodeFileFilter, defaultFileName);
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

        private async Task ExecuteSaveFileAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFileTabs.Contains(tab) || codeFileService == null)
                return;

            var content = tab.CurrentContent;
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

        private async Task ExecuteSaveAsFileAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFileTabs.Contains(tab) || codeFileService == null)
                return;

            var content = tab.CurrentContent;
            if (string.IsNullOrEmpty(content))
                return;

            var defaultFileName = codeFileService.IsNewFilePath(tab.FilePath)
                ? "NewFile.cs"
                : Path.GetFileName(tab.FilePath);

            var savedPath = await codeFileService.SaveCodeFileAsync(content, codeFileService.CodeFileFilter, defaultFileName);
            if (savedPath != null)
            {
                tab.FilePath = savedPath;
                tab.UpdateSavedContent(content);
            }
        }

        private void OnFontSettingsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(FontSize));
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(ClassificationHighlightColors));
            var colors = ClassificationHighlightColors;
            foreach (var tab in OpenedFiles)
            {
                if (tab.CodeEditor is RoslynCodeEditor roslynEditor)
                    roslynEditor.ClassificationHighlightColors = colors;
            }
            OnPropertyChanged(nameof(CurrentFileTab));
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
    }
}
