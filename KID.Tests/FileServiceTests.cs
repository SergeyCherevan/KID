using System.IO;
using System.Text.Json;
using KID.Models;
using KID.Services.Files;
using KID.Tests.TestDoubles;

namespace KID.Tests.Files;

public sealed class FileServiceTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{\"FontFamily\":null}")]
    [InlineData("{\"UILanguage\":null}")]
    [InlineData("{\"TemplateCode\":null}")]
    [InlineData("{\"FontSize\":14,\"FontSize\":22}")]
    [InlineData("{\"FontSize\":")]
    public async Task ReadJsonAsync_WhenSettingsJsonIsInvalid_RejectsWithoutChangingFile(string json)
    {
        await WithJsonFileAsync(json, async (service, path) =>
        {
            await Assert.ThrowsAsync<JsonException>(
                () => service.ReadJsonAsync<WindowConfigurationData>(path));
            Assert.Equal(json, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        });
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"Tabs\":null}")]
    [InlineData("{\"Tabs\":[{\"Content\":null}]}")]
    [InlineData("{\"Tabs\":[{\"FilePath\":null}]}")]
    [InlineData("{\"Tabs\":[{\"SavedContent\":null}]}")]
    [InlineData("{\"Version\":1,\"Version\":1}")]
    [InlineData("{\"Tabs\":[{\"Content\":\"a\",\"Content\":\"b\"}]}")]
    public async Task ReadJsonAsync_WhenSessionJsonIsInvalid_RejectsWithoutChangingFile(string json)
    {
        await WithJsonFileAsync(json, async (service, path) =>
        {
            await Assert.ThrowsAsync<JsonException>(
                () => service.ReadJsonAsync<EditorSessionData>(path));
            Assert.Equal(json, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task ReadJsonAsync_WhenSettingsPropertiesAreMissing_PreservesDefaultsAndIgnoresUnknownFields()
    {
        // Старый идентификатор темы допустим в JSON: миграцию выполняет provider, а не FileService.
        const string json = """{"ProgrammingLanguage":"C#","ColorTheme":"Theme_Light","FontFamily":"Consolas","FontSize":14,"TemplateName":"HelloWorld.cs","FutureSetting":true}""";
        await WithJsonFileAsync(json, async (service, path) =>
        {
            var settings = await service.ReadJsonAsync<WindowConfigurationData>(path);
            Assert.NotNull(settings);
            Assert.Equal("ru-RU", settings.UILanguage);
            Assert.True(settings.IsIconsVisible);
            Assert.Equal(new WindowConfigurationData().TemplateCode, settings.TemplateCode);
            Assert.Equal(14, settings.FontSize);
        });
    }

    [Fact]
    public async Task ReadJsonAsync_WhenSessionPropertiesAreMissing_PreservesDefaults()
    {
        await WithJsonFileAsync("""{"Tabs":[{}],"FutureSetting":true}""", async (service, path) =>
        {
            var session = await service.ReadJsonAsync<EditorSessionData>(path);
            Assert.NotNull(session);
            Assert.Equal(EditorSessionData.CurrentVersion, session.Version);
            var tab = Assert.Single(session.Tabs);
            Assert.Equal(string.Empty, tab.FilePath);
            Assert.Equal(string.Empty, tab.Content);
            Assert.Equal(string.Empty, tab.SavedContent);
        });
    }

    [Fact]
    public async Task ReadJsonAsync_ForOtherModels_RetainsPermissiveJsonContract()
    {
        await WithJsonFileAsync("""{"Value":"first","Value":null}""", async (service, path) =>
        {
            var data = await service.ReadJsonAsync<GenericJsonData>(path);
            Assert.NotNull(data);
            Assert.Null(data.Value);
        });
        await WithJsonFileAsync("null", async (service, path) =>
            Assert.Null(await service.ReadJsonAsync<GenericJsonData>(path)));
    }

    [Fact]
    public async Task WriteJsonAsync_ReusedOptions_PreserveIndentedDefaultFormat()
    {
        await WithJsonFileAsync("{}", async (service, path) =>
        {
            var data = new WindowConfigurationData();
            var expectedOptions = new JsonSerializerOptions { WriteIndented = true };
            var expected = JsonSerializer.Serialize(data, expectedOptions);
            await service.WriteJsonAsync(path, data);
            Assert.Equal(expected, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            data.FontSize = 22;
            data.IsIconsVisible = false;
            expected = JsonSerializer.Serialize(data, expectedOptions);
            await service.WriteJsonAsync(path, data);
            Assert.Equal(expected, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            var restored = await service.ReadJsonAsync<WindowConfigurationData>(path);
            Assert.NotNull(restored);
            Assert.False(restored.IsIconsVisible);
        });
    }

    private static async Task WithJsonFileAsync(string json, Func<FileService, string, Task> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kid-json-tests-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "data.json");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, json, TestContext.Current.CancellationToken);
            await action(new FileService(new StubLocalizationService()), path);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    public sealed class GenericJsonData
    {
        public string Value { get; set; } = "default";
    }

    [Fact]
    public async Task WriteFileAsync_CreatesMissingParentAndFileExistsReportsState()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"kid-file-service-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(testRoot, "nested", "file.txt");
        var missingPath = Path.Combine(testRoot, "missing.txt");
        var service = new FileService(new StubLocalizationService());

        try
        {
            await service.WriteFileAsync(filePath, "content");

            Assert.True(service.FileExists(filePath));
            Assert.False(service.FileExists(missingPath));
            Assert.Equal(
                "content",
                await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFileAsync_ReplacesExistingFileAndRemovesTemporaryFile()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"kid-file-service-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(testRoot, "file.txt");
        var service = new FileService(new StubLocalizationService());

        try
        {
            Directory.CreateDirectory(testRoot);
            await File.WriteAllTextAsync(
                filePath,
                "old content",
                TestContext.Current.CancellationToken);

            await service.WriteFileAsync(filePath, "new content");

            Assert.Equal(
                "new content",
                await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
            Assert.Empty(GetTemporaryFiles(testRoot, filePath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFileAsync_WhenReplacementFails_PreservesOriginalAndRemovesTemporaryFile()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"kid-file-service-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(testRoot, "file.txt");
        var service = new FileService(new StubLocalizationService());

        try
        {
            Directory.CreateDirectory(testRoot);
            await File.WriteAllTextAsync(
                filePath,
                "old content",
                TestContext.Current.CancellationToken);

            using (new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var exception = await Assert.ThrowsAnyAsync<Exception>(
                    () => service.WriteFileAsync(filePath, "new content"));
                Assert.True(exception is IOException or UnauthorizedAccessException);
            }

            Assert.Equal(
                "old content",
                await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
            Assert.Empty(GetTemporaryFiles(testRoot, filePath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    private static IEnumerable<string> GetTemporaryFiles(string directory, string filePath) =>
        Directory.EnumerateFiles(
            directory,
            $".{Path.GetFileName(filePath)}.*.tmp",
            SearchOption.TopDirectoryOnly);
}
