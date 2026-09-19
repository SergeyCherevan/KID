using System.IO;
using KID.Services.Files;
using KID.Tests.TestDoubles;

namespace KID.Tests.Files;

public sealed class FileServiceTests
{
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
