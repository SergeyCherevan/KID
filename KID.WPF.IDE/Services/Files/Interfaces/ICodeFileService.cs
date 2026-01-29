using System.Threading.Tasks;

namespace KID.Services.Files.Interfaces
{
    public interface ICodeFileService
    {
        Task<string?> OpenCodeFileAsync(string fileFilter);
        Task SaveCodeFileAsync(string code, string fileFilter);
    }
}

