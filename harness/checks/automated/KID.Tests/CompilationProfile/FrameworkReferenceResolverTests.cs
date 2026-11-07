using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using KID.Services.CompilationProfile;

namespace KID.Tests.CompilationProfile;

public sealed class FrameworkReferenceResolverTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void ValidManifest_SelectsOnlyDeclaredAssets_AndIgnoresExtraDll()
    {
        using var fixture = new Fixture();
        File.Copy(typeof(FrameworkReferenceResolverTests).Assembly.Location, Path.Combine(fixture.Root, "Unrelated.dll"));
        var result = fixture.Resolve();
        Assert.Equal(4, result.Count);
        Assert.DoesNotContain(result, path => Path.GetFileName(path) == "Unrelated.dll");
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("tfm")]
    [InlineData("rid")]
    [InlineData("owner")]
    [InlineData("emptyOwner")]
    [InlineData("pack")]
    [InlineData("packVersion")]
    [InlineData("escape")]
    [InlineData("absolute")]
    [InlineData("ads")]
    [InlineData("duplicate")]
    [InlineData("identity")]
    [InlineData("culture")]
    [InlineData("token")]
    [InlineData("nullAsset")]
    [InlineData("nullOwners")]
    [InlineData("unknownProperty")]
    [InlineData("missingAnchor")]
    public void InvalidManifest_FailsExplicitly(string mutation)
    {
        using var fixture = new Fixture();
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath))!.AsObject();
        var assets = manifest["assemblies"]!.AsArray();
        switch (mutation)
        {
            case "schema": manifest["schemaVersion"] = 99; break;
            case "tfm": manifest["targetFramework"] = "net8.0-windows"; break;
            case "rid": manifest["runtimeIdentifier"] = "win-arm64"; break;
            case "owner": assets[0]!["framework"] = "Microsoft.AspNetCore.App"; break;
            case "emptyOwner": assets.RemoveAt(3); break;
            case "pack": manifest["frameworks"]![0]!["runtimePackId"] = "fake"; break;
            case "packVersion": manifest["frameworks"]![0]!["runtimePackVersion"] = "9.0.1"; break;
            case "escape": assets[0]!["path"] = "../System.Runtime.dll"; break;
            case "absolute": assets[0]!["path"] = fixture.Core; break;
            case "ads": assets[0]!["path"] = "System.Runtime.dll:stream"; break;
            case "duplicate": assets.Add(assets[0]!.DeepClone()); break;
            case "identity": assets[0]!["version"] = "0.0.0.0"; break;
            case "culture": assets[0]!["culture"] = "ru"; break;
            case "token": assets[0]!["publicKeyToken"] = "bad"; break;
            case "nullAsset": assets[0] = null; break;
            case "nullOwners": manifest["frameworks"] = null; break;
            case "unknownProperty": manifest["extra"] = true; break;
            case "missingAnchor": assets.RemoveAt(0); break;
        }
        File.WriteAllText(fixture.ManifestPath, manifest.ToJsonString());
        Assert.Throws<InvalidOperationException>(() => fixture.Resolve());
    }

    [Theory]
    [InlineData("missingManifest")]
    [InlineData("brokenJson")]
    [InlineData("duplicateProperty")]
    [InlineData("missingDll")]
    [InlineData("nativeDll")]
    [InlineData("tpaMismatch")]
    [InlineData("missingTpa")]
    [InlineData("mixed")]
    [InlineData("emptyLocation")]
    public void InvalidLayout_FailsWithoutFallback(string mutation)
    {
        using var fixture = new Fixture();
        switch (mutation)
        {
            case "missingManifest": File.Delete(fixture.ManifestPath); break;
            case "brokenJson": File.WriteAllText(fixture.ManifestPath, "{"); break;
            case "duplicateProperty": File.WriteAllText(fixture.ManifestPath, "{\"schemaVersion\":1,\"schemaVersion\":1}"); break;
            case "missingDll": File.Delete(fixture.Core); break;
            case "nativeDll": File.WriteAllText(fixture.Core, "not a managed PE"); break;
            case "tpaMismatch": fixture.Tpa = fixture.Desktop; break;
            case "missingTpa": fixture.Tpa = ""; break;
            case "mixed": fixture.Desktop = typeof(System.Windows.Application).Assembly.Location; break;
            case "emptyLocation": fixture.Core = ""; break;
        }
        Assert.Throws<InvalidOperationException>(() => fixture.Resolve());
    }

    [Fact]
    public void ReparseDirectory_FailsWithoutFollowingJunction()
    {
        using var fixture = new Fixture();
        var junction = Path.Combine(fixture.Root, "junction");
        // Directory symlinks require privileges; a junction does not on Windows.
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "mklink", "/J", junction, fixture.Root },
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        try
        {
            Assert.Throws<InvalidOperationException>(() => FrameworkReferenceResolver.Resolve(
                junction, Path.Combine(junction, Path.GetFileName(fixture.Core)),
                Path.Combine(junction, Path.GetFileName(fixture.Desktop)), fixture.Tpa));
        }
        finally { Directory.Delete(junction); }
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(AppContext.BaseDirectory, "profile-fixtures", Guid.NewGuid().ToString("N"));
        internal string ManifestPath => Path.Combine(Root, FrameworkReferenceResolver.ManifestName);
        internal string Core { get; set; }
        internal string Desktop { get; set; }
        internal string Tpa { get; set; }
        internal Fixture()
        {
            Directory.CreateDirectory(Root);
            // A fixed independent minimum fixture, not the generator's asset-selection algorithm.
            Assembly[] sources = [typeof(object).Assembly, typeof(System.Runtime.GCSettings).Assembly,
                typeof(System.Console).Assembly, typeof(System.Windows.Application).Assembly];
            // GCSettings lives in CoreLib: use the runtime's explicit System.Runtime file instead.
            string[] sourcePaths = [sources[0].Location,
                Path.Combine(Path.GetDirectoryName(sources[0].Location)!, "System.Runtime.dll"),
                sources[2].Location, sources[3].Location];
            var assets = sourcePaths.Select((source, index) =>
            {
                var target = Path.Combine(Root, Path.GetFileName(source));
                File.Copy(source, target);
                var identity = AssemblyName.GetAssemblyName(source);
                return new FrameworkAsset(index == 3 ? "Microsoft.WindowsDesktop.App" : "Microsoft.NETCore.App",
                    Path.GetFileName(target), identity.Name!, identity.Version!.ToString(), identity.CultureName ?? "",
                    Convert.ToHexString(identity.GetPublicKeyToken() ?? []).ToLowerInvariant());
            }).ToArray();
            var manifest = new FrameworkManifest(1, "net10.0-windows", "win-x64",
                [new("Microsoft.NETCore.App", "Microsoft.NETCore.App.Runtime.win-x64", "10.0.13"),
                 new("Microsoft.WindowsDesktop.App", "Microsoft.WindowsDesktop.App.Runtime.win-x64", "10.0.13")], assets);
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            Core = Path.Combine(Root, assets[0].Path);
            Desktop = Path.Combine(Root, assets[3].Path);
            Tpa = string.Join(Path.PathSeparator, assets.Select(a => Path.Combine(Root, a.Path)));
        }
        internal IReadOnlyCollection<string> Resolve() => FrameworkReferenceResolver.Resolve(Root, Core, Desktop, Tpa);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
