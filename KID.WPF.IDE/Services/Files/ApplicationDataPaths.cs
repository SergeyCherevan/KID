using System.IO;

namespace KID.Services.Files;

/// <summary>One process-wide explicit smoke root, validated before any persistence/logging.</summary>
internal static class ApplicationDataPaths
{
    internal const string OverrideVariable = "KID_TEST_DATA_ROOT";
    internal const string OwnerFile = ".kid-harness-owner";
    private static readonly Lazy<string?> SelectedRoot = new(
        () => ValidateOverride(Environment.GetEnvironmentVariable(OverrideVariable)));

    internal static string SettingsDirectory => SelectedRoot.Value ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KID");

    internal static string LogsDirectory
    {
        get
        {
            if (SelectedRoot.Value is { } root) return Path.Combine(root, "Logs");
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(local) ? Path.Combine(AppContext.BaseDirectory, "Logs") : Path.Combine(local, "KID", "Logs");
        }
    }

    internal static string? ValidateOverride(string? value)
    {
        if (value is null) return null; // Normal launch retains the existing paths and priorities.
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new ArgumentException($"{OverrideVariable} must be an absolute, nonempty owned directory.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (string.Equals(root, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{OverrideVariable} cannot be a filesystem root.");
        foreach (var folder in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
        {
            var profile = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(folder), "KID"));
            if (root.Equals(profile, StringComparison.OrdinalIgnoreCase) || root.StartsWith(profile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{OverrideVariable} cannot select real KID user data.");
        }
        RejectReparseAncestors(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Prepare the owned {OverrideVariable} directory before launch: {root}");
        var owner = Path.Combine(root, OwnerFile);
        RejectReparseAncestors(owner);
        if (!File.Exists(owner) || !Guid.TryParse(File.ReadAllText(owner).Trim(), out _))
            throw new InvalidDataException($"{OverrideVariable} requires a GUID marker {OwnerFile}.");
        foreach (var name in new[] { "settings.json", "editor-session.json", "Logs" }) RejectReparseAncestors(Path.Combine(root, name));
        // Fail closed before bootstrap logging's best-effort catch and before settings/session.
        CheckWritable(root);
        var logs = Path.Combine(root, "Logs");
        Directory.CreateDirectory(logs);
        CheckWritable(logs);
        return root;
    }

    private static void RejectReparseAncestors(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"{OverrideVariable} cannot traverse a reparse point: {current}");
        }
    }

    private static void CheckWritable(string directory)
    {
        using var stream = new FileStream(Path.Combine(directory, $".kid-write-check-{Guid.NewGuid():N}"),
            FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        stream.WriteByte(0);
    }
}
