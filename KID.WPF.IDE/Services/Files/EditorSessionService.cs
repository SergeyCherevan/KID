using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KID.Models;
using KID.Services.Files.Interfaces;

namespace KID.Services.Files
{
    /// <summary>
    /// Хранит последний снимок редактора в AppData. Замена файла выполняется атомарно,
    /// поэтому аварийное завершение во время autosave не повреждает предыдущий снимок.
    /// </summary>
    public sealed class EditorSessionService : IEditorSessionService
    {
        private readonly IFileService fileService;
        private readonly string sessionPath;
        private readonly SemaphoreSlim fileLock = new(1, 1);

        public EditorSessionService(IFileService fileService)
            : this(
                fileService,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "KID"))
        {
        }

        internal EditorSessionService(IFileService fileService, string sessionDirectory)
        {
            this.fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);

            sessionPath = Path.Combine(sessionDirectory, "editor-session.json");
        }

        public async Task<EditorSessionData?> LoadAsync()
        {
            await fileLock.WaitAsync();
            try
            {
                var session = await fileService.ReadJsonAsync<EditorSessionData>(sessionPath);
                if (session != null && session.Version != EditorSessionData.CurrentVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported editor session version: {session.Version}.");
                }

                return session;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async Task SaveAsync(EditorSessionData session)
        {
            ArgumentNullException.ThrowIfNull(session);

            await fileLock.WaitAsync();
            try
            {
                await fileService.WriteJsonAsync(sessionPath, session);
            }
            finally
            {
                fileLock.Release();
            }
        }
    }
}
