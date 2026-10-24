using KID.Models;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization;
using KID.Services.Localization.Interfaces;
using KID.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO;
using System.Threading.Channels;
using System.Text.Json;
using KID.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KID.Tests.Initialize;

public sealed class WindowConfigurationServiceTests
{
    [Fact]
    public async Task SetConfigurationFromFileAsync_UsesFileServiceForExistenceAndRead()
    {
        var expectedSettings = new WindowConfigurationData { UILanguage = "en", IsIconsVisible = false };
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = path => path.EndsWith("settings.json", StringComparison.Ordinal),
            ReadJsonImplementation = (_, type) =>
            {
                Assert.Equal(typeof(WindowConfigurationData), type);
                return expectedSettings;
            }
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        bool? notifiedIconsVisibility = null;
        service.IconsVisibilityChanged += (_, _) => notifiedIconsVisibility = service.Settings.IsIconsVisible;

        await service.SetConfigurationFromFileAsync();

        Assert.Same(expectedSettings, service.Settings);
        Assert.Equal(false, notifiedIconsVisibility);
        var settingsPath = Assert.Single(fileService.CheckedPaths);
        Assert.Equal(settingsPath, fileService.ReadJsonPath);
    }

    [Fact]
    public async Task SetConfigurationFromFileAsync_WhenUserSettingsAreMissing_UsesDefaultConfigurationFromAppBaseDirectory()
    {
        var expectedSettings = new WindowConfigurationData { UILanguage = "en" };
        var expectedDefaultPath = Path.Combine(
            AppContext.BaseDirectory,
            "DefaultWindowConfiguration.json");
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => false,
            ReadJsonImplementation = (path, type) =>
            {
                Assert.Equal(expectedDefaultPath, path);
                Assert.Equal(typeof(WindowConfigurationData), type);
                return expectedSettings;
            }
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());

        await service.SetConfigurationFromFileAsync();

        Assert.Same(expectedSettings, service.Settings);
        Assert.Equal(expectedDefaultPath, fileService.ReadJsonPath);
        Assert.Single(fileService.WrittenJsonPaths);
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("en-CA", "en")]
    [InlineData("ru-RU", "ru")]
    [InlineData("ru-BY", "ru")]
    [InlineData("ru-UA", "ru")]
    [InlineData("ru-KZ", "ru")]
    [InlineData("uk-UA", "uk")]
    [InlineData("uk-CA", "uk")]
    [InlineData("sk-SK", "en")]
    public async Task FirstLaunch_UsesSystemUiLanguageAndPersistsIt(string systemCulture, string expected)
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(systemCulture);
            using var provider = new ServiceCollection().BuildServiceProvider();
            var localization = new LocalizationService(provider);
            var fileService = new RecordingFileService
            {
                ReadJsonImplementation = (_, _) => JsonSerializer.Deserialize<WindowConfigurationData>(
                    """{"FontSize":22,"FontFamily":"Fira Code"}""")
            };
            var logger = new RecordingLogger();
            var service = new WindowConfigurationService(fileService, localization, logger);
            var notifiedLanguages = new List<string>();
            service.UILanguageSettingsChanged += (_, _) =>
                notifiedLanguages.Add(Assert.Single(fileService.WrittenLanguages));

            await service.SetConfigurationFromFileAsync();

            Assert.Equal(expected, service.Settings.UILanguage);
            Assert.Equal([expected], fileService.WrittenLanguages);
            Assert.Equal([expected], notifiedLanguages);
            Assert.Equal(22, service.Settings.FontSize);
            Assert.Equal("Fira Code", service.Settings.FontFamily);
            Assert.Empty(logger.Events);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstLaunch_WhenDefaultConfigurationIsUnavailable_StillUsesSystemUiLanguage(bool unreadable)
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("uk-UA");
            using var provider = new ServiceCollection().BuildServiceProvider();
            var fileService = new RecordingFileService
            {
                ReadJsonImplementation = (_, _) => unreadable
                    ? throw new JsonException("Invalid default settings JSON")
                    : null
            };
            var service = new WindowConfigurationService(fileService, new LocalizationService(provider));

            await service.SetConfigurationFromFileAsync();

            Assert.Equal("uk", service.Settings.UILanguage);
            Assert.Equal(["uk"], fileService.WrittenLanguages);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("en-US", "ru")]
    [InlineData("ru-RU", "uk")]
    [InlineData("uk-UA", "en")]
    [InlineData("sk-SK", "uk")]
    public async Task Load_SavedLanguageOverridesSystemUiLanguage(string systemCulture, string savedLanguage)
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(systemCulture);
            using var provider = new ServiceCollection().BuildServiceProvider();
            var fileService = new RecordingFileService
            {
                FileExistsImplementation = _ => true,
                ReadJsonImplementation = (_, _) => new WindowConfigurationData { UILanguage = savedLanguage }
            };
            var service = new WindowConfigurationService(fileService, new LocalizationService(provider));

            await service.SetConfigurationFromFileAsync();

            Assert.Equal(savedLanguage, service.Settings.UILanguage);
            Assert.Empty(fileService.WrittenJsonPaths);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task FirstLaunch_WhenSettingsSaveFails_DoesNotNotifyLanguageChange()
    {
        var fileService = new RecordingFileService
        {
            WriteJsonImplementation = () => throw new IOException("settings write failed")
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService { CurrentCulture = "uk" });
        var events = 0;
        service.UILanguageSettingsChanged += (_, _) => events++;

        await Assert.ThrowsAsync<IOException>(() => service.SetConfigurationFromFileAsync());

        Assert.Equal(0, events);
    }

    [Fact]
    public async Task SetConfigurationFromFileAsync_MigratesLegacyDefaultTemplateName()
    {
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = path => path.EndsWith("settings.json", StringComparison.Ordinal),
            ReadJsonImplementation = (_, _) => new WindowConfigurationData
            {
                TemplateName = "ProjectTemplates/ru-RU/HelloWorld.cs"
            }
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());

        await service.SetConfigurationFromFileAsync();

        Assert.Equal("HelloWorld.cs", service.Settings.TemplateName);
    }

    [Fact]
    public async Task SetDefaultCodeAsync_UsesAbsoluteTemplatePathAsIs()
    {
        var templatePath = Path.Combine(Path.GetTempPath(), "template.cs");
        const string templateCode = "Console.WriteLine(42);";
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = path => path == templatePath,
            ReadFileResult = templateCode
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        service.Settings.TemplateName = templatePath;

        await service.SetDefaultCodeAsync();

        Assert.Equal(templateCode, service.Settings.TemplateCode);
        Assert.Equal([templatePath], fileService.CheckedPaths);
        Assert.Equal(templatePath, fileService.ReadFilePath);
    }

    [Fact]
    public async Task SetDefaultCodeAsync_WhenDefaultTemplateIsMissing_SeedsItInAppData()
    {
        var expectedTemplatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KID",
            "HelloWorld.cs");
        var expectedTemplateCode = new WindowConfigurationData().TemplateCode;
        var templateExists = false;
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = path => path == expectedTemplatePath && templateExists,
            ReadFileResult = expectedTemplateCode,
            WriteFileImplementation = (path, content) =>
            {
                Assert.Equal(expectedTemplatePath, path);
                Assert.Equal(expectedTemplateCode, content);
                templateExists = true;
            }
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        service.Settings.TemplateName = "HelloWorld.cs";

        await service.SetDefaultCodeAsync();

        Assert.Equal(expectedTemplateCode, service.Settings.TemplateCode);
        Assert.Equal(expectedTemplatePath, fileService.ReadFilePath);
        Assert.Equal([expectedTemplatePath], fileService.WrittenFilePaths);
        Assert.Equal([expectedTemplateCode], fileService.WrittenFileContents);
    }

    [Fact]
    public async Task SetDefaultCodeAsync_DoesNotOverwriteExistingDefaultTemplate()
    {
        var expectedTemplatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KID",
            "HelloWorld.cs");
        const string existingTemplateCode = "Console.WriteLine(42);";
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = path => path == expectedTemplatePath,
            ReadFileResult = existingTemplateCode
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        service.Settings.TemplateName = "HelloWorld.cs";

        await service.SetDefaultCodeAsync();

        Assert.Equal(existingTemplateCode, service.Settings.TemplateCode);
        Assert.Equal(expectedTemplatePath, fileService.ReadFilePath);
        Assert.Empty(fileService.WrittenFilePaths);
    }

    [Fact]
    public async Task SetDefaultCodeAsync_WhenDefaultTemplateProvisioningFails_UsesBuiltInTemplate()
    {
        var expectedTemplateCode = new WindowConfigurationData().TemplateCode;
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => false,
            WriteFileImplementation = (_, _) => throw new IOException("template write failed")
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        service.Settings.TemplateName = "HelloWorld.cs";

        await service.SetDefaultCodeAsync();

        Assert.Equal(expectedTemplateCode, service.Settings.TemplateCode);
    }

    [Fact]
    public async Task SaveSettingsAsync_QueuesConcurrentWritesAndPersistsLatestState()
    {
        var fileService = new BlockingWriteFileService();
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());

        var fontChange = service.SetFontAsync("Fira Code", null);
        await fileService.FirstWriteStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var themeChange = service.SetColorThemeAsync("Theme_VS2022Dark");
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
                Assert.Equal("Theme_VS2022Light", first.ColorTheme);
            },
            second =>
            {
                Assert.Equal("Fira Code", second.FontFamily);
                Assert.Equal("Theme_VS2022Dark", second.ColorTheme);
        });
    }

    [Fact]
    public async Task SaveSettingsAsync_PropagatesWriteFailureInsteadOfReportingSuccess()
    {
        var service = new WindowConfigurationService(new FailingWriteFileService(), new StubLocalizationService());

        await Assert.ThrowsAsync<IOException>(() => service.SaveSettingsAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task SetConfigurationFromFileAsync_NormalizesInvalidFontSizeWithDiagnostic(double fontSize)
    {
        var settings = new WindowConfigurationData { FontSize = fontSize, FontFamily = "Fira Code" };
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => true,
            ReadJsonImplementation = (_, _) => settings
        };
        var logger = new RecordingLogger();
        var service = new WindowConfigurationService(fileService, new StubLocalizationService(), logger);

        await service.SetConfigurationFromFileAsync();

        Assert.Equal(14, service.Settings.FontSize);
        Assert.Equal("Fira Code", service.Settings.FontFamily);
        Assert.Single(logger.Events);
        Assert.Equal(DiagnosticEventIds.SettingsFallback, logger.Events[0]);
        Assert.Empty(fileService.WrittenJsonPaths);
    }

    [Fact]
    public async Task SetConfigurationFromFileAsync_NormalizesBlankFontAndInvalidCulture()
    {
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => true,
            ReadJsonImplementation = (_, _) => new WindowConfigurationData
            {
                FontFamily = " ",
                UILanguage = "not/a/culture",
                FontSize = 22
            }
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());

        await service.SetConfigurationFromFileAsync();

        Assert.Equal("Consolas", service.Settings.FontFamily);
        Assert.Equal("ru", service.Settings.UILanguage);
        Assert.Equal(22, service.Settings.FontSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetConfigurationFromFileAsync_WhenSettingsAreRejected_LogsBeforeLaterSave(bool nullProperty)
    {
        var logger = new RecordingLogger();
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => true,
            ReadJsonImplementation = (_, _) => nullProperty
                ? new WindowConfigurationData { FontFamily = null! }
                : throw new JsonException("Invalid settings JSON")
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService(), logger);

        await service.SetConfigurationFromFileAsync();

        Assert.Equal("Consolas", service.Settings.FontFamily);
        Assert.Single(logger.Events);
        Assert.Equal(DiagnosticEventIds.SettingsFallback, logger.Events[0]);
        Assert.Empty(fileService.WrittenJsonPaths);

        await service.SaveSettingsAsync();

        Assert.Single(fileService.WrittenJsonPaths);
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("ru-RU", "ru")]
    [InlineData("uk-UA", "uk")]
    [InlineData("en-GB", "en")]
    [InlineData("ru-BY", "ru")]
    [InlineData("RU", "ru")]
    public async Task Load_MigratesLegacyLanguageAndPersistsOnlyOnce(string input, string expected)
    {
        var settings = new WindowConfigurationData { UILanguage = input, FontSize = 22, FontFamily = "Fira Code" };
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => true,
            ReadJsonImplementation = (_, _) => settings
        };
        var logger = new RecordingLogger();
        using var provider = new ServiceCollection()
            .AddSingleton<ILocalizationService, LocalizationService>()
            .AddSingleton<IFileService>(fileService)
            .AddSingleton<IWindowConfigurationService>(services => new WindowConfigurationService(
                services.GetRequiredService<IFileService>(), services.GetRequiredService<ILocalizationService>(), logger))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var service = provider.GetRequiredService<IWindowConfigurationService>();
        var events = 0;
        service.UILanguageSettingsChanged += (_, _) => events++;

        await service.SetConfigurationFromFileAsync();
        await provider.GetRequiredService<ILocalizationService>().SetCultureAsync(input);
        await service.SetConfigurationFromFileAsync();

        Assert.Equal(expected, service.Settings.UILanguage);
        Assert.Equal([expected], fileService.WrittenLanguages);
        Assert.Equal(22, service.Settings.FontSize);
        Assert.Equal("Fira Code", service.Settings.FontFamily);
        Assert.Equal(1, events);
        Assert.Empty(logger.Events);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("not/a/culture")]
    [InlineData("")]
    public async Task Load_UnsupportedLanguageUsesAvailableDefault(string input)
    {
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => true,
            ReadJsonImplementation = (_, _) => new WindowConfigurationData { UILanguage = input }
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        await service.SetConfigurationFromFileAsync();
        Assert.Equal("ru", service.Settings.UILanguage);
        Assert.Equal(["ru"], fileService.WrittenLanguages);
    }

    [Fact]
    public async Task Load_EmergencyCatalogUsesEnglishDefault()
    {
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => false
        };
        var service = new WindowConfigurationService(fileService,
            new StubLocalizationService { SupportedCultures = ["en"] });
        await service.SetConfigurationFromFileAsync();
        Assert.Equal("en", service.Settings.UILanguage);
        Assert.Equal(["en"], fileService.WrittenLanguages);
    }

    [Fact]
    public async Task LanguageSaveFailure_CanBeRetriedWithoutAnotherLanguageChange()
    {
        var failWrite = true;
        var fileService = new RecordingFileService
        {
            WriteJsonImplementation = () => { if (failWrite) throw new IOException("settings write failed"); }
        };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        var events = 0;
        service.UILanguageSettingsChanged += (_, _) => events++;

        await Assert.ThrowsAsync<IOException>(() => service.SetUILanguageAsync("uk-UA"));
        Assert.Equal("uk", service.Settings.UILanguage);
        Assert.Equal(0, events);
        failWrite = false;
        await service.SetUILanguageAsync("uk-UA");
        await service.SetUILanguageAsync("uk");

        Assert.Equal(["uk", "uk"], fileService.WrittenLanguages);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task MigrationWriteFailure_PropagatesAndPreservesLoadedSettings()
    {
        var failWrite = true;
        var fileService = new RecordingFileService
        {
            FileExistsImplementation = _ => true,
            ReadJsonImplementation = (_, _) => new WindowConfigurationData { UILanguage = "ru-RU", FontSize = 22 },
            WriteJsonImplementation = () => { if (failWrite) throw new IOException("migration write failed"); }
        };
        var logger = new RecordingLogger();
        var service = new WindowConfigurationService(fileService, new StubLocalizationService(), logger);

        await Assert.ThrowsAsync<IOException>(() => service.SetConfigurationFromFileAsync());
        Assert.Equal("ru", service.Settings.UILanguage);
        Assert.Equal(22, service.Settings.FontSize);
        Assert.DoesNotContain(DiagnosticEventIds.SettingsFallback, logger.Events);
        failWrite = false;
        await service.SetUILanguageAsync("ru");
        Assert.Equal(2, fileService.WrittenLanguages.Count);
    }

    [Fact]
    public async Task ConcurrentLanguageChanges_OlderSaveDoesNotCompleteNewerChange()
    {
        var fileService = new BlockingWriteFileService();
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        var events = 0;
        service.UILanguageSettingsChanged += (_, _) => events++;
        var first = service.SetUILanguageAsync("en-US");
        await fileService.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var second = service.SetUILanguageAsync("uk-UA");
        fileService.ReleaseFirstWrite();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["en", "uk"], fileService.WrittenSettings.Select(settings => settings.UILanguage));
        Assert.Equal(1, events);
        Assert.Equal(1, fileService.MaximumConcurrentWrites);
        await service.SetUILanguageAsync("uk");
        Assert.Equal(2, fileService.WriteCount);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("not/a/culture")]
    public async Task SetUILanguageAsync_UnsupportedInputHasNoSideEffects(string input)
    {
        var fileService = new RecordingFileService();
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        var events = 0;
        service.UILanguageSettingsChanged += (_, _) => events++;

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetUILanguageAsync(input));

        Assert.Equal("ru", service.Settings.UILanguage);
        Assert.Empty(fileService.WrittenJsonPaths);
        Assert.Equal(0, events);
    }

    [Fact]
    public async Task OlderSuccessfulWrite_DoesNotHideNewerFailedLanguageSave()
    {
        var fileService = new BlockingWriteFileService { FailWriteNumber = 2 };
        var service = new WindowConfigurationService(fileService, new StubLocalizationService());
        var events = 0;
        service.UILanguageSettingsChanged += (_, _) => events++;
        var first = service.SetUILanguageAsync("en");
        await fileService.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var second = service.SetUILanguageAsync("uk");
        fileService.ReleaseFirstWrite();
        await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            await second;
            Assert.Fail("The second write must fail.");
        }
        catch (IOException) { }
        Assert.Equal(0, events);

        await service.SetUILanguageAsync("uk");

        Assert.Equal(3, fileService.WriteCount);
        Assert.Equal(1, events);
        Assert.Equal("uk", service.Settings.UILanguage);
    }

    private sealed class RecordingLogger : ILogger<WindowConfigurationService>
    {
        internal List<EventId> Events { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Events.Add(eventId);
    }

    private sealed class RecordingFileService : IFileService
    {
        internal Func<string, bool> FileExistsImplementation { get; init; } = _ => false;
        internal Func<string, Type, object?> ReadJsonImplementation { get; init; } =
            (_, _) => null;
        internal Func<string, string>? ReadFileImplementation { get; init; }
        internal Action<string, string>? WriteFileImplementation { get; init; }
        internal Action? WriteJsonImplementation { get; init; }
        internal string ReadFileResult { get; init; } = string.Empty;

        internal List<string> CheckedPaths { get; } = [];
        internal List<string> WrittenFilePaths { get; } = [];
        internal List<string> WrittenFileContents { get; } = [];
        internal List<string> WrittenJsonPaths { get; } = [];
        internal List<string> WrittenLanguages { get; } = [];
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
            return Task.FromResult(ReadFileImplementation?.Invoke(filePath) ?? ReadFileResult);
        }

        public Task WriteFileAsync(string filePath, string content)
        {
            WrittenFilePaths.Add(filePath);
            WrittenFileContents.Add(content);
            WriteFileImplementation?.Invoke(filePath, content);
            return Task.CompletedTask;
        }

        public Task<T?> ReadJsonAsync<T>(string filePath)
        {
            ReadJsonPath = filePath;
            return Task.FromResult((T?)ReadJsonImplementation(filePath, typeof(T)));
        }

        public Task WriteJsonAsync<T>(string filePath, T data)
        {
            WrittenJsonPaths.Add(filePath);
            if (data is WindowConfigurationData settings)
                WrittenLanguages.Add(settings.UILanguage);
            WriteJsonImplementation?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingWriteFileService : IFileService
    {
        internal int FailWriteNumber { get; init; }
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
                if (currentWrite == FailWriteNumber)
                    throw new IOException("queued write failed");
            }
            finally
            {
                lock (sync)
                    activeWrites--;
            }
        }
    }

    private sealed class FailingWriteFileService : IFileService
    {
        public bool FileExists(string filePath) => false;
        public Task<string> ReadFileAsync(string filePath) => throw new NotSupportedException();
        public Task WriteFileAsync(string filePath, string content) => throw new NotSupportedException();
        public Task<T?> ReadJsonAsync<T>(string filePath) => throw new NotSupportedException();
        public Task WriteJsonAsync<T>(string filePath, T data) =>
            Task.FromException(new IOException("settings write failed"));
    }
}
