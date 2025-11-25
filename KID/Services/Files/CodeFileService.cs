using System.Threading.Tasks;
using KID.Services.Files.Interfaces;

namespace KID.Services.Files
{
    public class CodeFileService : ICodeFileService
    {
        private const string CSharpFileFilter = "C# файлы (*.cs)|*.cs|Все файлы (*.*)|*.*";
        private const string DefaultFileName = "Program.cs";

        private readonly IFileService fileService;
        private readonly IFileDialogService fileDialogService;

        public CodeFileService(IFileService fileService, IFileDialogService fileDialogService)
        {
            this.fileService = fileService ?? throw new System.ArgumentNullException(nameof(fileService));
            this.fileDialogService = fileDialogService ?? throw new System.ArgumentNullException(nameof(fileDialogService));
        }

        public async Task<string?> OpenCodeFileAsync()
        {
            var filePath = fileDialogService.ShowOpenDialog(CSharpFileFilter);
            if (filePath == null)
                return null;

            return await fileService.ReadFileAsync(filePath);
        }

        public async Task SaveCodeFileAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return;

            var filePath = fileDialogService.ShowSaveDialog(CSharpFileFilter, DefaultFileName);
            if (filePath == null)
                return;

            await fileService.WriteFileAsync(filePath, code);
        }
    }
}

