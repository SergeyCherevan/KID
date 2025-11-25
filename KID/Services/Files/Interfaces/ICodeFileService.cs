using System.Threading.Tasks;

namespace KID.Services.Files.Interfaces
{
    public interface ICodeFileService
    {
        Task<string?> OpenCodeFileAsync();
        Task SaveCodeFileAsync(string code);
    }
}

