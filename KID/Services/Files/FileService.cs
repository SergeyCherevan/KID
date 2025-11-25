using System;
using System.IO;
using System.Threading.Tasks;
using KID.Services.Files.Interfaces;
using KID.Services.Localization.Interfaces;

namespace KID.Services.Files
{
    public class FileService : IFileService
    {
        private readonly ILocalizationService _localizationService;

        public FileService(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public async Task<string?> ReadFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            try
            {
                return await File.ReadAllTextAsync(filePath);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public async Task WriteFileAsync(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(_localizationService.GetString("Error_FilePathEmpty"), nameof(filePath));

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            try
            {
                await File.WriteAllTextAsync(filePath, content);
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
    }
}

