using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using KID.Models;
using KID.ViewModels.Infrastructure;
using RoslynPad.Editor;

namespace KID.ViewModels.Interfaces
{
    /// <summary>
    /// Интерфейс ViewModel для панели редакторов кода с вкладками.
    /// </summary>
    public interface ICodeEditorsViewModel
    {
        /// <summary>
        /// Коллекция открытых вкладок.
        /// </summary>
        ObservableCollection<OpenedFileTab> OpenedFileTabs { get; }

        /// <summary>
        /// Текущая вкладка.
        /// </summary>
        OpenedFileTab? CurrentFileTab { get; set; }

        FontFamily FontFamily { get; }
        double FontSize { get; }
        /// <summary>
        /// Палитра цветов синтаксической подсветки редактора в зависимости от текущей темы (светлая/тёмная).
        /// </summary>
        IClassificationHighlightColors ClassificationHighlightColors { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        RelayCommand UndoCommand { get; }
        RelayCommand RedoCommand { get; }
        RelayCommand<OpenedFileTab> CloseFileCommand { get; }
        RelayCommand<OpenedFileTab> SelectFileCommand { get; }
        RelayCommand<OpenedFileTab> SaveFileCommand { get; }
        RelayCommand<OpenedFileTab> SaveAsFileCommand { get; }
        RelayCommand<OpenedFileTab> SaveAndSetAsTemplateCommand { get; }
        RelayCommand<OpenedFileTab> MoveTabLeftCommand { get; }
        RelayCommand<OpenedFileTab> MoveTabRightCommand { get; }

        /// <summary>
        /// Добавляет файл в новую вкладку или переключается на уже открытый.
        /// </summary>
        Task CreateAndAddFileTabAsync(string path, string content, string? savedContent = null);

        /// <summary>
        /// Закрывает вкладку после обработки несохранённых изменений.
        /// Возвращает false, если пользователь отменил закрытие.
        /// </summary>
        Task<bool> CloseFileTabAsync(OpenedFileTab tab);

        /// <summary>
        /// Проверяет все вкладки перед закрытием приложения и сохраняет снимок сессии.
        /// </summary>
        Task<bool> PrepareForApplicationCloseAsync();

        /// <summary>
        /// Восстанавливает вкладки из последнего autosave-снимка.
        /// </summary>
        Task<bool> RestoreSessionAsync();

        /// <summary>
        /// Делает вкладку текущей.
        /// </summary>
        void SelectFileTab(OpenedFileTab tab);
    }
}
