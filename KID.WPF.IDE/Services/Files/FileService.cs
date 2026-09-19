using KID.Services.Files.Interfaces;
using KID.Services.Diagnostics;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KID.Services.Files
{
    public class FileService : IFileService
    {
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<FileService>? _logger;

        public FileService(
            ILocalizationService localizationService,
            ILogger<FileService>? logger = null)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _logger = logger;
        }

        public bool FileExists(string filePath) => File.Exists(filePath);

        public async Task<string> ReadFileAsync(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));

            return await File.ReadAllTextAsync(filePath);
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
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException exception)
                {
                    _logger?.LogWarning(
                        DiagnosticEventIds.TemporaryFileCleanupFailed,
                        exception,
                        "Temporary file cleanup failed. Operation={Operation} TemporaryPath={TemporaryPath}",
                        "WriteFileAsync",
                        temporaryPath);
                }
                catch (UnauthorizedAccessException exception)
                {
                    _logger?.LogWarning(
                        DiagnosticEventIds.TemporaryFileCleanupFailed,
                        exception,
                        "Temporary file cleanup failed. Operation={Operation} TemporaryPath={TemporaryPath}",
                        "WriteFileAsync",
                        temporaryPath);
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

