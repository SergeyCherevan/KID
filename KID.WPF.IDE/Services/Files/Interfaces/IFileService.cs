using System.Threading.Tasks;

namespace KID.Services.Files.Interfaces
{
    public interface IFileService
    {
        bool FileExists(string filePath);
        Task<string> ReadFileAsync(string filePath);
        Task WriteFileAsync(string filePath, string content);
        Task<T?> ReadJsonAsync<T>(string filePath);
        Task WriteJsonAsync<T>(string filePath, T data);
    }
}

