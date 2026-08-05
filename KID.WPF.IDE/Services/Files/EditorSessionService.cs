using System;
using System.IO;
using System.Text.Json;
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
        private readonly string sessionDirectory;
        private readonly string sessionPath;
        private readonly SemaphoreSlim fileLock = new(1, 1);
        private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

        public EditorSessionService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            sessionDirectory = Path.Combine(appDataPath, "KID");
            sessionPath = Path.Combine(sessionDirectory, "editor-session.json");
        }

        public async Task<EditorSessionData?> LoadAsync()
        {
            if (!File.Exists(sessionPath))
                return null;

            await fileLock.WaitAsync();
            try
            {
                await using var stream = new FileStream(
                    sessionPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);
                var session = await JsonSerializer.DeserializeAsync<EditorSessionData>(stream, jsonOptions);
                if (session != null && session.Version != EditorSessionData.CurrentVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported editor session version: {session.Version}.");
                }

                return session;
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async Task SaveAsync(EditorSessionData session)
        {
            ArgumentNullException.ThrowIfNull(session);

            Directory.CreateDirectory(sessionDirectory);
            var temporaryPath = Path.Combine(
                sessionDirectory,
                $"editor-session-{Guid.NewGuid():N}.tmp");

            await fileLock.WaitAsync();
            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(stream, session, jsonOptions);
                    await stream.FlushAsync();
                }

                File.Move(temporaryPath, sessionPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                finally
                {
                    fileLock.Release();
                }
            }
        }
    }
}
