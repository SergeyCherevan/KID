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



        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        public ObservableCollection<OpenedFileTab> OpenedFiles { get; } = new();

        private int indexOfCurrentFileTab;
        /// <summary>
        /// Текущая вкладка.
        /// </summary>
        public OpenedFileTab? CurrentFileTab
        {
            get => OpenedFiles.Count > 0 && indexOfCurrentFileTab >= 0 && indexOfCurrentFileTab < OpenedFiles.Count
                ? OpenedFiles[indexOfCurrentFileTab]
                : null;
            set
            {
                var newIndex = value != null ? OpenedFiles.IndexOf(value) : 0;
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
            ILocalizationService localizationService,
            ICodeEditorFactory codeEditorFactory
        )
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
            try
            {
                await ExecuteSaveAndSetAsTemplateAsync(tab);
            }
            catch (Exception ex)
            {
                ShowSaveError(ex);
            }
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
            UpdateCurrentFileTabIndexAfterMove(index, index - 1);
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
            UpdateCurrentFileTabIndexAfterMove(index, index + 1);
            RaiseMoveTabCommandsCanExecute();
        }

        /*
         * Зачем нужен этот метод
         * ----------------------
         * Текущая вкладка хранится не ссылкой на объект, а индексом `indexOfCurrentFileTab`.
         * Свойство `CurrentFileTab` вычисляется как `OpenedFiles[indexOfCurrentFileTab]`.
         *
         * Когда мы меняем порядок вкладок через `OpenedFiles.Move(oldIndex, newIndex)`:
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


        public void AddFileTab(string path, string content)
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

            OpenedFiles.Add(tab);
            indexOfCurrentFileTab = OpenedFiles.Count - 1;
            OnPropertyChanged(nameof(CurrentFileTab));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        /// <inheritdoc />
        public void CloseFileTab(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab))
                return;

            var index = OpenedFiles.IndexOf(tab);
            OpenedFiles.Remove(tab);

            if (OpenedFiles.Count == 0)
            {
                var templateCode = windowConfigurationService?.Settings?.TemplateCode ?? string.Empty;
                AddFileTab(codeFileService.NewFilePath, templateCode);
            }
            else
            {
                if (indexOfCurrentFileTab >= OpenedFiles.Count)
                    indexOfCurrentFileTab = OpenedFiles.Count - 1;
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
            if (tab != null && OpenedFiles.Contains(tab))
                CurrentFileTab = tab;
        }

        /// <inheritdoc />
        public void NotifyCurrentFileTabSaved(string content)
        {
            CurrentFileTab?.UpdateSavedContent(content);
            OnPropertyChanged(nameof(CurrentFileTab));
        }

        private void ShowSaveError(Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                string.Format(localizationService.GetString("Error_FileSaveFailed") ?? "Failed to save: {0}", ex.Message),
                localizationService.GetString("Error_Title") ?? "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
        }

        private async Task ExecuteSaveAndSetAsTemplateAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null ||
                windowConfigurationService?.Settings == null || localizationService == null)
                return;

            var content = tab.CurrentContent;
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

        private async Task ExecuteSaveFileAsync(OpenedFileTab tab)
        {
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null)
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
            if (tab == null || !OpenedFiles.Contains(tab) || codeFileService == null || localizationService == null)
                return;

            var content = tab.CurrentContent;
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

        private void OnFontSettingsChanged(object? sender, EventArgs e)
        {
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
    }
}
