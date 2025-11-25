using System.Threading.Tasks;

namespace KID.Services.Files.Interfaces
{
    public interface IFileService
    {
        Task<string?> ReadFileAsync(string filePath);
        Task WriteFileAsync(string filePath, string content);
    }
}

