using KID.Models;
using KID.Services.Errors.Interfaces;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize;
using System.Threading.Channels;

namespace KID.Tests.Initialize;

public sealed class WindowConfigurationServiceTests
{
    [Fact]
    public async Task SetConfigurationFromFileAsync_UsesFileServiceForExistenceAndRead()
    {
        var expectedSettings = new WindowConfigurationData { UILanguage = "en-US" };
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = path => path.EndsWith("settings.json", StringComparison.Ordinal),
            ReadJsonImplementation = (_, type) =>
            {
                Assert.Equal(typeof(WindowConfigurationData), type);
                return expectedSettings;
            }
        };
        var service = new WindowConfigurationService(new RethrowingErrorHandler(), fileService);

        await service.SetConfigurationFromFileAsync();

        Assert.Same(expectedSettings, service.Settings);
        var settingsPath = Assert.Single(fileService.CheckedPaths);
        Assert.Equal(settingsPath, fileService.ReadJsonPath);
    }

    [Fact]
    public async Task SetDefaultCodeAsync_UsesFileServiceForExistenceAndRead()
    {
        const string templatePath = "template.cs";
        const string templateCode = "Console.WriteLine(42);";
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = path => path == templatePath,
            ReadFileResult = templateCode
        };
        var service = new WindowConfigurationService(new RethrowingErrorHandler(), fileService);
        service.Settings.TemplateName = templatePath;

        await service.SetDefaultCodeAsync();

        Assert.Equal(templateCode, service.Settings.TemplateCode);
        Assert.Equal([templatePath], fileService.CheckedPaths);
        Assert.Equal(templatePath, fileService.ReadFilePath);
    }

    [Fact]
    public async Task SaveSettingsAsync_QueuesConcurrentWritesAndPersistsLatestState()
    {
        var fileService = new BlockingWriteFileService();
        var service = new WindowConfigurationService(new RethrowingErrorHandler(), fileService);

        var fontChange = service.SetFontAsync("Fira Code", null);
        await fileService.FirstWriteStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var themeChange = service.SetColorThemeAsync("Theme_Dark");
        var writesStartedBeforeRelease = fileService.WriteCount;

        fileService.ReleaseFirstWrite();
        await Task.WhenAll(fontChange, themeChange).WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, writesStartedBeforeRelease);
        Assert.Equal(1, fileService.MaximumConcurrentWrites);
        Assert.Collection(
            fileService.WrittenSettings,
            first =>
            {
                Assert.Equal("Fira Code", first.FontFamily);
                Assert.Equal("Theme_Light", first.ColorTheme);
            },
            second =>
            {
                Assert.Equal("Fira Code", second.FontFamily);
                Assert.Equal("Theme_Dark", second.ColorTheme);
            });
    }

    private sealed class RecordingFileService : IFileService
    {
        internal Func<string, bool> FileExistsImplementation { get; init; } = _ => false;
        internal Func<string, Type, object?> ReadJsonImplementation { get; init; } =
            (_, _) => null;
        internal string ReadFileResult { get; init; } = string.Empty;

        internal List<string> CheckedPaths { get; } = [];
        internal string? ReadFilePath { get; private set; }
        internal string? ReadJsonPath { get; private set; }

        public bool FileExists(string filePath)
        {
            CheckedPaths.Add(filePath);
            return FileExistsImplementation(filePath);
        }

        public Task<string> ReadFileAsync(string filePath)
        {
            ReadFilePath = filePath;
            return Task.FromResult(ReadFileResult);
        }

        public Task WriteFileAsync(string filePath, string content) =>
            throw new NotSupportedException();

        public Task<T?> ReadJsonAsync<T>(string filePath)
        {
            ReadJsonPath = filePath;
            return Task.FromResult((T?)ReadJsonImplementation(filePath, typeof(T)));
        }

        public Task WriteJsonAsync<T>(string filePath, T data) =>
            throw new NotSupportedException();
    }

    private sealed class RethrowingErrorHandler : IAsyncOperationErrorHandler
    {
        public void Execute(Action action, string errorMessageKey) => action();

        public Task ExecuteAsync(Func<Task> asyncAction, string errorMessageKey) => asyncAction();
    }

    private sealed class BlockingWriteFileService : IFileService
    {
        private readonly object sync = new();
        private readonly Channel<bool> firstWriteReleaseQueue =
            Channel.CreateBounded<bool>(1);
        private readonly List<WindowConfigurationData> writtenSettings = [];
        private int activeWrites;
        private int maximumConcurrentWrites;
        private int writeCount;

        internal TaskCompletionSource FirstWriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int WriteCount
        {
            get
            {
                lock (sync)
                    return writeCount;
            }
        }

        internal int MaximumConcurrentWrites
        {
            get
            {
                lock (sync)
                    return maximumConcurrentWrites;
            }
        }

        internal IReadOnlyList<WindowConfigurationData> WrittenSettings
        {
            get
            {
                lock (sync)
                    return writtenSettings.ToArray();
            }
        }

        internal void ReleaseFirstWrite() =>
            firstWriteReleaseQueue.Writer.TryWrite(true);

        public bool FileExists(string filePath) => throw new NotSupportedException();

        public Task<string> ReadFileAsync(string filePath) => throw new NotSupportedException();

        public Task WriteFileAsync(string filePath, string content) => throw new NotSupportedException();

        public Task<T?> ReadJsonAsync<T>(string filePath) => throw new NotSupportedException();

        public async Task WriteJsonAsync<T>(string filePath, T data)
        {
            int currentWrite;
            lock (sync)
            {
                activeWrites++;
                maximumConcurrentWrites = Math.Max(maximumConcurrentWrites, activeWrites);
                currentWrite = ++writeCount;

                var settings = Assert.IsType<WindowConfigurationData>(data);
                writtenSettings.Add(new WindowConfigurationData
                {
                    ColorTheme = settings.ColorTheme,
                    UILanguage = settings.UILanguage,
                    ProgrammingLanguage = settings.ProgrammingLanguage,
                    FontFamily = settings.FontFamily,
                    FontSize = settings.FontSize,
                    TemplateName = settings.TemplateName,
                    TemplateCode = settings.TemplateCode
                });
            }

            try
            {
                if (currentWrite == 1)
                {
                    FirstWriteStarted.TrySetResult();
                    await firstWriteReleaseQueue.Reader.ReadAsync();
                }
            }
            finally
            {
                lock (sync)
                    activeWrites--;
            }
        }
    }
}
