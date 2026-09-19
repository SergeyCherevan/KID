using KID.Services.Files.Interfaces;
using KID.Services.Localization.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KID.Services.Files
{
    public class FileService : IFileService
    {
        private readonly ILocalizationService _localizationService;

        public FileService(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public bool FileExists(string filePath) => File.Exists(filePath);

        public async Task<string> ReadFileAsync(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));

            try
            {
                return await File.ReadAllTextAsync(filePath);
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
        }

        public async Task WriteFileAsync(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(_localizationService.GetString("Error_FilePathEmpty"), nameof(filePath));

            ArgumentNullException.ThrowIfNull(content, nameof(content));

            string fullPath = Path.GetFullPath(filePath);

            string targetDirectory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("The target file has no parent directory.");

            Directory.CreateDirectory(targetDirectory);

            string temporaryPath = Path.Combine(
                targetDirectory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await File.WriteAllTextAsync(temporaryPath, content);
                File.Move(temporaryPath, fullPath, overwrite: true);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Log the exception or handle it as needed, but do not throw it to avoid masking the original exception.
                }
                catch (UnauthorizedAccessException)
                {
                    // Log the exception or handle it as needed, but do not throw it to avoid masking the original exception.
                }
            }
        }

        public async Task<T?> ReadJsonAsync<T>(string filePath)
        {
            string jsonString = await ReadFileAsync(filePath);

            var jsonObject = JsonSerializer.Deserialize<T>(jsonString);

            return jsonObject;
        }

        public async Task WriteJsonAsync<T>(string filePath, T data)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string jsonString = JsonSerializer.Serialize(data, options);

            await WriteFileAsync(filePath, jsonString);
        }
    }
}

