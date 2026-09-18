using System.Reflection;
using System.Reflection.PortableExecutable;
using System.IO;
using System.Collections.Immutable;
using System.Windows;
using KID.Services.CompilationProfile;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NAudio.Wave;

namespace KID.Tests.CompilationProfile;

public sealed class KIDCompilationProfileTests
{
    private static readonly KIDCompilationProfileProvider Provider = new();

    [Fact]
    public void GetProfile_ReturnsSameInstance()
    {
        var first = Provider.GetProfile();
        var second = Provider.GetProfile();

        Assert.Same(first, second);
    }

    [Fact]
    public void Profile_ContainsSystemConsoleReference()
    {
        Assert.Contains("System.Console", GetAssemblyNames());
    }

    [Fact]
    public void Profile_ContainsKidLibraryReference()
    {
        Assert.Contains(typeof(global::KID.Graphics).Assembly.GetName().Name!, GetAssemblyNames());
    }

    [Fact]
    public void Profile_ContainsPlaybackStateAssemblyReference()
    {
        Assert.Contains(typeof(PlaybackState).Assembly.GetName().Name!, GetAssemblyNames());
    }

    [Fact]
    public void Profile_ContainsCurrentNetCoreAndWindowsDesktopFrameworks()
    {
        var paths = GetReferencePaths();
        var netCoreDirectory = GetAssemblyDirectory(typeof(object).Assembly);
        var windowsDesktopDirectory = GetAssemblyDirectory(typeof(Application).Assembly);

        Assert.Contains(paths, path => IsPathWithinDirectory(path, netCoreDirectory));
        Assert.Contains(paths, path => IsPathWithinDirectory(path, windowsDesktopDirectory));
    }

    [Fact]
    public void Profile_ContainsOnlyCurrentFrameworksAndExplicitDependencies()
    {
        var actualPaths = GetReferencePaths();
        var netCoreDirectory = GetAssemblyDirectory(typeof(object).Assembly);
        var windowsDesktopDirectory = GetAssemblyDirectory(typeof(Application).Assembly);
        var tpa = Assert.IsType<string>(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));
        var expectedPaths = tpa
            .Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .Where(path =>
                IsPathWithinDirectory(path, netCoreDirectory) ||
                IsPathWithinDirectory(path, windowsDesktopDirectory))
            .Where(HasManagedMetadata)
            .Append(Path.GetFullPath(typeof(global::KID.Graphics).Assembly.Location))
            .Append(Path.GetFullPath(typeof(PlaybackState).Assembly.Location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AssertPathsEqual(expectedPaths, actualPaths);
    }

    [Fact]
    public void Profile_DoesNotContainKidIdeAssembly()
    {
        Assert.DoesNotContain("KID.WPF.IDE", GetAssemblyNames());
    }

    [Fact]
    public void Profile_DoesNotContainRoslynPadOrCodeAnalysisAssemblies()
    {
        var names = GetAssemblyNames();

        Assert.DoesNotContain(names, name => name.StartsWith("RoslynPad", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Profile_DoesNotContainAvalonEditDependencyInjectionOrVisualStudioThreading()
    {
        var names = GetAssemblyNames();

        Assert.DoesNotContain(names, name => name.StartsWith("ICSharpCode.AvalonEdit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Microsoft.VisualStudio.Threading", names);
    }

    [Fact]
    public void Profile_DoesNotContainOtherNAudioAssemblies()
    {
        var naudioAssemblies = GetAssemblyNames()
            .Where(name => name.StartsWith("NAudio", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(["NAudio.Core"], naudioAssemblies);
    }

    [Fact]
    public void Profile_ReferencePathsAreAbsoluteExistingAndUnique()
    {
        var paths = GetReferencePaths();

        Assert.All(paths, path =>
        {
            Assert.True(Path.IsPathFullyQualified(path));
            Assert.True(File.Exists(path), $"Missing profile reference: {path}");
        });
        Assert.Equal(
            paths.Length,
            paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Profile_ReferencesHaveStableOrdinalIgnoreCaseOrdering()
    {
        var paths = GetReferencePaths();
        var orderedPaths = paths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AssertPathsEqual(orderedPaths, paths);
    }

    [Fact]
    public void Profile_GlobalImportsAreExplicitAndInitiallyEmpty()
    {
        Assert.Empty(Provider.GetProfile().GlobalImports);
    }

    [Fact]
    public void Profile_EmptyReferencesFailFast()
    {
        Assert.Throws<ArgumentException>(() => new KIDCompilationProfile(
            ImmutableArray<MetadataReference>.Empty,
            ImmutableArray<string>.Empty));
    }

    [Fact]
    public void Profile_DuplicateReferencePathsFailFast()
    {
        MetadataReference reference = MetadataReference.CreateFromFile(
            Path.GetFullPath(typeof(object).Assembly.Location));

        Assert.Throws<ArgumentException>(() => new KIDCompilationProfile(
            [reference, reference],
            ImmutableArray<string>.Empty));
    }

    [Fact]
    public void Profile_InvalidOrDuplicateImportsFailFast()
    {
        MetadataReference reference = MetadataReference.CreateFromFile(
            Path.GetFullPath(typeof(object).Assembly.Location));

        Assert.Throws<ArgumentException>(() => new KIDCompilationProfile(
            [reference],
            ["not a valid import!"]));
        Assert.Throws<ArgumentException>(() => new KIDCompilationProfile(
            [reference],
            ["System", "System"]));
    }

    [Fact]
    public void ProfileReferences_CompileConsoleKidAndPlaybackStateWithoutErrors()
    {
        const string code = """
            using System;
            using KID;
            using NAudio.Wave;

            Console.WriteLine("ok");
            Graphics.Circle(10, 10, 5);
            PlaybackState state = PlaybackState.Stopped;
            """;

        var errors = GetCompilationErrors(code);

        Assert.Empty(errors);
    }

    [Fact]
    public void ProfileImports_DoNotMakeKidNamespaceImplicit()
    {
        const string code = """
            using System;

            Console.WriteLine("ok");
            Graphics.Circle(10, 10, 5);
            """;

        var errors = GetCompilationErrors(code);

        Assert.Contains(errors, diagnostic =>
            diagnostic.Id == "CS0103" &&
            diagnostic.GetMessage().Contains("Graphics", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("KID.Services.CodeExecution.CodeExecutionService")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree")]
    [InlineData("RoslynPad.Roslyn.RoslynHost")]
    [InlineData("ICSharpCode.AvalonEdit.TextEditor")]
    [InlineData("NAudio.Mixer.Mixer")]
    public void ProfileReferences_DoNotResolveDeniedHostOrPackageType(string typeName)
    {
        var code = $$"""
            public static class Program
            {
                public static void Main()
                {
                    {{typeName}} value = null;
                }
            }
            """;

        var errors = GetCompilationErrors(code);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void GetProfile_LateAppLocalAssemblyLoadDoesNotChangeIdentityOrReferences()
    {
        var profile = Provider.GetProfile();
        var beforePaths = GetReferencePaths(profile);

        _ = Assembly.LoadFile(typeof(KIDCompilationProfileTests).Assembly.Location);

        var afterProfile = Provider.GetProfile();
        var afterPaths = GetReferencePaths(afterProfile);

        Assert.Same(profile, afterProfile);
        AssertPathsEqual(beforePaths, afterPaths);
        Assert.DoesNotContain("KID.Tests", GetAssemblyNames(afterProfile));
    }

    private static Diagnostic[] GetCompilationErrors(string code)
    {
        var profile = Provider.GetProfile();
        var tree = CSharpSyntaxTree.ParseText(code, path: "UserProgram.cs");
        var compilation = CSharpCompilation.Create(
            $"ProfileContract_{Guid.NewGuid():N}",
            [tree],
            profile.MetadataReferences,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithUsings(profile.GlobalImports));

        return compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
    }

    private static string[] GetReferencePaths() =>
        GetReferencePaths(Provider.GetProfile());

    private static string[] GetReferencePaths(KIDCompilationProfile profile) =>
        profile.MetadataReferences
            .Cast<PortableExecutableReference>()
            .Select(reference => Assert.IsType<string>(reference.FilePath))
            .ToArray();

    private static string[] GetAssemblyNames() =>
        GetAssemblyNames(Provider.GetProfile());

    private static string[] GetAssemblyNames(KIDCompilationProfile profile) =>
        GetReferencePaths(profile)
            .Select(path => AssemblyName.GetAssemblyName(path).Name!)
            .ToArray();

    private static string GetAssemblyDirectory(Assembly assembly) =>
        Path.TrimEndingDirectorySeparator(
            Path.GetDirectoryName(Path.GetFullPath(assembly.Location))!);

    private static bool IsPathWithinDirectory(string path, string directory) =>
        path.StartsWith(
            directory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool HasManagedMetadata(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        return reader.HasMetadata;
    }

    private static void AssertPathsEqual(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.True(
                string.Equals(expected[index], actual[index], StringComparison.OrdinalIgnoreCase),
                $"Reference path mismatch at index {index}: expected '{expected[index]}', actual '{actual[index]}'.");
        }
    }
}
