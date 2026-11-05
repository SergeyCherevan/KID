using System.IO;
using KID.Services.Files;

namespace KID.Tests.Persistence;

public sealed class ApplicationDataPathsTests
{
    [Fact]
    public void MissingOverride_PreservesNormalPathsWithoutWritingProfile()
    {
        Assert.Null(ApplicationDataPaths.ValidateOverride(null));
        if (Environment.GetEnvironmentVariable(ApplicationDataPaths.OverrideVariable) is not null) return;
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KID"), ApplicationDataPaths.SettingsDirectory);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(string.IsNullOrWhiteSpace(local) ? Path.Combine(AppContext.BaseDirectory, "Logs") : Path.Combine(local, "KID", "Logs"), ApplicationDataPaths.LogsDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative")]
    [InlineData("C:relative")]
    public void InvalidOverride_ThrowsWithoutFallback(string value) => Assert.Throws<ArgumentException>(() => ApplicationDataPaths.ValidateOverride(value));

    [Fact]
    public void FilesystemRootAndRealProfile_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => ApplicationDataPaths.ValidateOverride(Path.GetPathRoot(Environment.CurrentDirectory)));
        Assert.Throws<ArgumentException>(() => ApplicationDataPaths.ValidateOverride(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KID")));
    }

    [Fact]
    public void OwnedRoots_AreIndependent_AndMarkerIsRequired()
    {
        var parent = NewRoot();
        var a = Path.Combine(parent, "a"); var b = Path.Combine(parent, "b");
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        Assert.Throws<InvalidDataException>(() => ApplicationDataPaths.ValidateOverride(a));
        foreach (var root in new[] { a, b }) File.WriteAllText(Path.Combine(root, ApplicationDataPaths.OwnerFile), Guid.NewGuid().ToString());
        Assert.Equal(a, ApplicationDataPaths.ValidateOverride(a));
        Assert.Equal(b, ApplicationDataPaths.ValidateOverride(b));
        File.WriteAllText(Path.Combine(a, "settings.json"), "a");
        Assert.False(File.Exists(Path.Combine(b, "settings.json")));
        Assert.True(Directory.Exists(Path.Combine(a, "Logs")));
    }

    [Fact]
    public void MissingRootAndBlockedLogs_FailExplicitly()
    {
        var root = NewRoot();
        Assert.Throws<DirectoryNotFoundException>(() => ApplicationDataPaths.ValidateOverride(Path.Combine(root, "missing")));
        File.WriteAllText(Path.Combine(root, ApplicationDataPaths.OwnerFile), Guid.NewGuid().ToString());
        File.WriteAllText(Path.Combine(root, "Logs"), "blocked by a file");
        Assert.Throws<IOException>(() => ApplicationDataPaths.ValidateOverride(root));
    }

    private static string NewRoot()
    {
        // Retain only ignored diagnostics; do not delete arbitrary roots during tests.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KID.sln"))) directory = directory.Parent;
        var root = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Checkout not found"), "artifacts", "harness", "data-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); return root;
    }
}
