using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KID.Services.CompilationProfile;

/// <summary>Resolves framework references by runtime provenance, never by app-local scanning.</summary>
internal static class FrameworkReferenceResolver
{
    internal const string ManifestName = "kid-framework-assemblies.v1.json";
    private static readonly string[] Owners = ["Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"];
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static IReadOnlyCollection<string> Resolve(
        string baseDirectory, string coreAnchor, string desktopAnchor, string? tpa)
    {
        try
        {
            return ResolveCore(baseDirectory, coreAnchor, desktopAnchor, tpa);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
                                   ArgumentException or BadImageFormatException)
        {
            throw new InvalidOperationException($"Cannot resolve framework profile at '{baseDirectory}' ({ManifestName}): {ex.Message}", ex);
        }
    }

    private static IReadOnlyCollection<string> ResolveCore(
        string baseDirectory, string coreAnchor, string desktopAnchor, string? tpa)
    {
        if (string.IsNullOrWhiteSpace(coreAnchor) || string.IsNullOrWhiteSpace(desktopAnchor))
            throw new InvalidOperationException("Framework anchor has no file location; single-file is unsupported.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        var core = Path.GetDirectoryName(Path.GetFullPath(coreAnchor))!;
        var desktop = Path.GetDirectoryName(Path.GetFullPath(desktopAnchor))!;
        if (!Directory.Exists(core) || !Directory.Exists(desktop))
            throw new InvalidOperationException("Framework anchor directory does not exist.");
        if (string.IsNullOrWhiteSpace(tpa))
            throw new InvalidOperationException("AppContext data 'TRUSTED_PLATFORM_ASSEMBLIES' is missing or empty.");
        var paths = tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var coreLocal = string.Equals(root, core, StringComparison.OrdinalIgnoreCase);
        var desktopLocal = string.Equals(root, desktop, StringComparison.OrdinalIgnoreCase);
        if (coreLocal != desktopLocal)
            throw new InvalidOperationException("Mixed framework-dependent/self-contained layout is unsupported.");
        if (coreLocal)
            return ReadManifest(root, coreAnchor, desktopAnchor, paths);

        // Keep the FDD contract: TPA filtered by the two framework directories.
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coreCount = 0;
        var desktopCount = 0;
        foreach (var path in paths)
        {
            var isCore = path.StartsWith(core + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            var isDesktop = path.StartsWith(desktop + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!isCore && !isDesktop) continue;
            if (!File.Exists(path)) throw new InvalidOperationException($"Runtime reference file does not exist: '{path}'.");
            if (!HasManagedMetadata(path) || !result.Add(path)) continue;
            if (isCore) coreCount++;
            if (isDesktop) desktopCount++;
        }
        if (coreCount == 0 || desktopCount == 0)
            throw new InvalidOperationException("No managed references found in one of the two framework directories.");
        return result;
    }

    private static IReadOnlyCollection<string> ReadManifest(
        string root, string coreAnchor, string desktopAnchor, HashSet<string> tpa)
    {
        RejectReparse(root);
        var manifestPath = Path.Combine(root, ManifestName);
        RejectReparse(manifestPath);
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        RejectDuplicateProperties(document.RootElement);
        var manifest = document.Deserialize<FrameworkManifest>(ManifestOptions)
            ?? throw new InvalidOperationException($"Empty manifest '{manifestPath}'.");
        if (manifest.SchemaVersion != 1 || manifest.TargetFramework != "net10.0-windows" ||
            manifest.RuntimeIdentifier != "win-x64" || !OperatingSystem.IsWindows() ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new InvalidOperationException($"Unsupported schema/TFM/RID/architecture in '{manifestPath}'.");
        if (manifest.Frameworks is null || manifest.Frameworks.Length != 2 || manifest.Assemblies is null)
            throw new InvalidOperationException($"Invalid owners/assemblies in '{manifestPath}'.");
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var framework in manifest.Frameworks)
        {
            if (framework is null || !Owners.Contains(framework.Name, StringComparer.Ordinal) ||
                !owners.Add(framework.Name) || framework.RuntimePackId != framework.Name + ".Runtime.win-x64" ||
                !Version.TryParse(framework.RuntimePackVersion, out var version) || version.Major != 10 ||
                version.Minor != 0 || version.Build < 0)
                throw new InvalidOperationException($"Invalid framework owner/runtime pack in '{manifestPath}'.");
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ownerCounts = Owners.ToDictionary(owner => owner, _ => 0, StringComparer.Ordinal);
        foreach (var asset in manifest.Assemblies)
        {
            if (asset is null || !owners.Contains(asset.Framework) || string.IsNullOrWhiteSpace(asset.Path) ||
                asset.Path.Any(ch => ch is '/' or '\\' or ':' || char.IsControl(ch)) ||
                asset.Path != Path.GetFileName(asset.Path) || asset.Path.Contains("..", StringComparison.Ordinal) ||
                !asset.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Invalid framework path/owner in '{manifestPath}'.");
            var path = Path.GetFullPath(Path.Combine(root, asset.Path));
            RejectReparse(path);
            if (!paths.Add(path) || !tpa.Contains(path))
                throw new InvalidOperationException($"Duplicate path or TPA mismatch: '{path}' in '{manifestPath}'.");
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) throw new InvalidOperationException($"Not a managed PE: '{path}'.");
            var identity = AssemblyName.GetAssemblyName(path);
            var token = Convert.ToHexString(identity.GetPublicKeyToken() ?? []).ToLowerInvariant();
            if (asset.Name != identity.Name || asset.Version != identity.Version?.ToString() ||
                asset.Culture != (identity.CultureName ?? "") || asset.PublicKeyToken != token ||
                !string.Equals(asset.Path, identity.Name + ".dll", StringComparison.OrdinalIgnoreCase) ||
                !names.TryAdd(asset.Name, asset.Framework))
                throw new InvalidOperationException($"Assembly identity mismatch/conflict: '{path}' in '{manifestPath}'.");
            ownerCounts[asset.Framework]++;
        }
        if (ownerCounts.Values.Any(count => count == 0) ||
            !paths.Contains(Path.GetFullPath(coreAnchor)) || !paths.Contains(Path.GetFullPath(desktopAnchor)) ||
            !names.TryGetValue("System.Private.CoreLib", out var coreOwner) || coreOwner != Owners[0] ||
            !names.TryGetValue("PresentationFramework", out var desktopOwner) || desktopOwner != Owners[1] ||
            !names.ContainsKey("System.Runtime") || !names.ContainsKey("System.Console"))
            throw new InvalidOperationException($"Missing framework owner/anchor/required assembly in '{manifestPath}'.");
        return paths;
    }

    private static void RejectReparse(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Reparse point is unsupported for framework manifest: '{current}'.");
    }

    private static bool HasManagedMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            return pe.HasMetadata;
        }
        catch (BadImageFormatException) { return false; }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}'.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
}

internal sealed record FrameworkManifest(
    int SchemaVersion, string TargetFramework, string RuntimeIdentifier,
    FrameworkOwner[] Frameworks, FrameworkAsset[] Assemblies);
internal sealed record FrameworkOwner(string Name, string RuntimePackId, string RuntimePackVersion);
internal sealed record FrameworkAsset(
    string Framework, string Path, string Name, string Version, string Culture, string PublicKeyToken);
