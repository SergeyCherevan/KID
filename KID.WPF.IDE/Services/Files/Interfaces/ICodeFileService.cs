using System.Threading.Tasks;
using KID.Services.Files;

namespace KID.Services.Files.Interfaces
{
    /// <summary>
    /// Сервис работы с файлами кода.
    /// </summary>
    public interface ICodeFileService
    {
        /// <summary>
        /// Путь для нового несохранённого файла.
        /// </summary>
        string NewFilePath { get; }

        /// <summary>
        /// Фильтр файлов кода для диалогов открытия и сохранения.
        /// </summary>
        string CodeFileFilter { get; }

        /// <summary>
        /// Открывает файл через диалог и возвращает содержимое и путь.
        /// </summary>
        Task<OpenFileResult?> OpenCodeFileWithPathAsync(string fileFilter);

        /// <summary>
        /// Сохраняет код в указанный файл без диалога.
        /// </summary>
        Task SaveToPathAsync(string filePath, string code);

        /// <summary>
        /// Сохраняет код через диалог выбора пути (Сохранить как).
        /// Возвращает путь сохранённого файла или null, если пользователь отменил.
        /// </summary>
        Task<string?> SaveCodeFileAsync(string code, string fileFilter, string defaultFileName);

        /// <summary>
        /// Возвращает true, если путь указывает на новый несохранённый файл.
        /// </summary>
        bool IsNewFilePath(string path);
    }
}

