using System.IO;
using KID.Models;
using KID.Services.Files;
using KID.Services.Files.Interfaces;
using KID.Tests.TestDoubles;

namespace KID.Tests.Files;

public sealed class EditorSessionServiceTests
{
    [Fact]
    public async Task LoadAsync_DelegatesToFileService()
    {
        var sessionDirectory = CreateSessionDirectoryPath();
        var expectedSession = new EditorSessionData { ActiveTabIndex = 2 };
        var fileService = new RecordingFileService
        {
            ReadJsonImplementation = (_, type) =>
            {
                Assert.Equal(typeof(EditorSessionData), type);
                return expectedSession;
            }
        };
        var service = new EditorSessionService(fileService, sessionDirectory);

        var actualSession = await service.LoadAsync();

        Assert.Same(expectedSession, actualSession);
        Assert.Equal(
            Path.Combine(sessionDirectory, "editor-session.json"),
            fileService.ReadJsonPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WhenSessionStorageIsMissing_ReturnsNull(bool directoryIsMissing)
    {
        Exception exception = directoryIsMissing
            ? new DirectoryNotFoundException()
            : new FileNotFoundException();
        var fileService = new RecordingFileService
        {
            ReadJsonImplementation = (_, _) => throw exception
        };
        var service = new EditorSessionService(fileService, CreateSessionDirectoryPath());

        var session = await service.LoadAsync();

        Assert.Null(session);
    }

    [Fact]
    public async Task LoadAsync_WhenVersionIsUnsupported_ThrowsInvalidDataException()
    {
        var fileService = new RecordingFileService
        {
            ReadJsonImplementation = (_, _) => new EditorSessionData
            {
                Version = EditorSessionData.CurrentVersion + 1
            }
        };
        var service = new EditorSessionService(fileService, CreateSessionDirectoryPath());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(service.LoadAsync);

        Assert.Contains("Unsupported editor session version", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_DelegatesToFileService()
    {
        var sessionDirectory = CreateSessionDirectoryPath();
        var session = new EditorSessionData { ActiveTabIndex = 1 };
        var fileService = new RecordingFileService();
        var service = new EditorSessionService(fileService, sessionDirectory);

        await service.SaveAsync(session);

        Assert.Equal(
            Path.Combine(sessionDirectory, "editor-session.json"),
            fileService.WrittenJsonPath);
        Assert.Same(session, fileService.WrittenJsonData);
        Assert.Equal(typeof(EditorSessionData), fileService.WrittenJsonType);
    }

    [Fact]
    public async Task SaveAndLoadAsync_WithFileService_RoundTripsSession()
    {
        var sessionDirectory = CreateSessionDirectoryPath();
        var session = new EditorSessionData
        {
            ActiveTabIndex = 0,
            Tabs =
            [
                new EditorSessionTabData
                {
                    FilePath = "lesson.cs",
                    Content = "Console.WriteLine(1);",
                    SavedContent = "Console.WriteLine(1);"
                }
            ]
        };
        var fileService = new FileService(new StubLocalizationService());
        var service = new EditorSessionService(fileService, sessionDirectory);

        try
        {
            await service.SaveAsync(session);

            var loadedSession = await service.LoadAsync();

            Assert.NotNull(loadedSession);
            Assert.Equal(session.Version, loadedSession.Version);
            Assert.Equal(session.ActiveTabIndex, loadedSession.ActiveTabIndex);
            var loadedTab = Assert.Single(loadedSession.Tabs);
            Assert.Equal(session.Tabs[0].FilePath, loadedTab.FilePath);
            Assert.Equal(session.Tabs[0].Content, loadedTab.Content);
            Assert.Equal(session.Tabs[0].SavedContent, loadedTab.SavedContent);
        }
        finally
        {
            if (Directory.Exists(sessionDirectory))
                Directory.Delete(sessionDirectory, recursive: true);
        }
    }

    private static string CreateSessionDirectoryPath() =>
        Path.Combine(Path.GetTempPath(), $"kid-editor-session-tests-{Guid.NewGuid():N}");

    private sealed class RecordingFileService : IFileService
    {
        internal Func<string, Type, object?> ReadJsonImplementation { get; init; } =
            (_, _) => null;

        internal string? ReadJsonPath { get; private set; }
        internal string? WrittenJsonPath { get; private set; }
        internal object? WrittenJsonData { get; private set; }
        internal Type? WrittenJsonType { get; private set; }

        public bool FileExists(string filePath) => false;

        public Task<string> ReadFileAsync(string filePath) =>
            throw new NotSupportedException();

        public Task WriteFileAsync(string filePath, string content) =>
            throw new NotSupportedException();

        public Task<T?> ReadJsonAsync<T>(string filePath)
        {
            ReadJsonPath = filePath;
            return Task.FromResult((T?)ReadJsonImplementation(filePath, typeof(T)));
        }

        public Task WriteJsonAsync<T>(string filePath, T data)
        {
            WrittenJsonPath = filePath;
            WrittenJsonData = data;
            WrittenJsonType = typeof(T);
            return Task.CompletedTask;
        }
    }
}
