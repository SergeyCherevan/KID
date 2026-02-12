using System.Threading.Tasks;
using KID.Services.Files.Interfaces;

namespace KID.Services.Files
{
    /// <summary>
    /// Реализация сервиса работы с файлами кода.
    /// </summary>
    public class CodeFileService : ICodeFileService
    {
        private readonly IFileService fileService;
        private readonly IFileDialogService fileDialogService;

        public CodeFileService(IFileService fileService, IFileDialogService fileDialogService)
        {
            this.fileService = fileService ?? throw new System.ArgumentNullException(nameof(fileService));
            this.fileDialogService = fileDialogService ?? throw new System.ArgumentNullException(nameof(fileDialogService));
        }

        /// <inheritdoc />
        public async Task<OpenFileResult?> OpenCodeFileWithPathAsync(string fileFilter)
        {
            var filePath = fileDialogService.ShowOpenDialog(fileFilter);
            if (filePath == null)
                return null;

            var code = await fileService.ReadFileAsync(filePath);
            if (code == null)
                return null;

            return new OpenFileResult(code, filePath);
        }

        /// <inheritdoc />
        public async Task SaveToPathAsync(string filePath, string code)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            await fileService.WriteFileAsync(filePath, code);
        }

        /// <inheritdoc />
        public async Task<string?> SaveCodeFileAsync(string code, string fileFilter, string defaultFileName)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var filePath = fileDialogService.ShowSaveDialog(fileFilter, defaultFileName);
            if (filePath == null)
                return null;

            await fileService.WriteFileAsync(filePath, code);
            return filePath;
        }

        /// <inheritdoc />
        public bool IsNewFilePath(string path) => path == "/NewFile.cs";
    }
}

