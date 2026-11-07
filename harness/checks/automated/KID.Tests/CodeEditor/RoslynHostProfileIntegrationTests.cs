using System.Reflection;
using System.Text;
using KID.Services.CodeEditor;
using KID.Services.CompilationProfile;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RoslynPad.Roslyn;
using KID.Services.CodeExecution.Compilation;
using KID.Tests.TestDoubles;

namespace KID.Tests.CodeEditor;

public sealed class RoslynHostProfileIntegrationTests
{
    private readonly KIDCompilationProfileProvider profileProvider = new();

    [Theory]
    [InlineData("System.Windows.Forms.Form", true)]
    [InlineData("System.Windows.Window", true)]
    [InlineData("KID.Graphics", true)]
    [InlineData("NAudio.Wave.PlaybackState", true)]
    [InlineData("KID.Services.CodeExecution.CodeExecutionService", false)]
    [InlineData("RoslynPad.Roslyn.RoslynHost", false)]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree", false)]
    [InlineData("ICSharpCode.AvalonEdit.TextEditor", false)]
    [InlineData("Microsoft.Extensions.DependencyInjection.ServiceCollection", false)]
    [InlineData("NAudio.Mixer.Mixer", false)]
    public async Task EditorAndCompiler_ResolveTheSameApprovedTypes(string type, bool allowed)
    {
        // typeof also exercises static classes without requiring constructors or runtime execution.
        var code = $"public static class Program {{ public static void Main() {{ _ = typeof({type}); }} }}";
        var editorErrors = await GetDocumentErrorsAsync(code);
        var compiler = new CSharpCompiler(new StubLocalizationService(), profileProvider);
        var compilation = await compiler.CompileAsync(code, TestContext.Current.CancellationToken);
        Assert.Equal(allowed, editorErrors.Length == 0);
        Assert.Equal(allowed, compilation.Success);
    }

    [Fact]
    public void Host_UsesSharedProfileReferencesAndExplicitImports()
    {
        var profile = profileProvider.GetProfile();
        var host = new RoslynHostService(profileProvider).GetHost();

        Assert.Equal(profile.MetadataReferences.Length, host.DefaultReferences.Length);
        for (var index = 0; index < profile.MetadataReferences.Length; index++)
            Assert.Same(profile.MetadataReferences[index], host.DefaultReferences[index]);
        Assert.Equal(profile.GlobalImports, host.DefaultImports);
    }

    [Fact]
    public async Task HostDocument_ConsoleAndGraphicsHaveNoErrorDiagnostics()
    {
        const string code = """
            using System;
            using KID;

            public static class Program
            {
                public static void Main()
                {
                    Console.WriteLine("ok");
                    Graphics.Circle(10, 10, 5);
                }
            }
            """;

        var errors = await GetDocumentErrorsAsync(code);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task HostDocument_UnknownSymbolProducesErrorDiagnostic()
    {
        const string code = """
            public static class Program
            {
                public static void Main() => MissingSymbol();
            }
            """;

        var errors = await GetDocumentErrorsAsync(code);

        Assert.Contains(errors, diagnostic =>
            diagnostic.Id == "CS0103" &&
            diagnostic.GetMessage().Contains("MissingSymbol", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostDocument_DoesNotReceiveIdeRoslynPadOrCodeAnalysisReferences()
    {
        var host = new RoslynHostService(profileProvider).GetHost();
        var documentId = AddDocument(host, "public static class Program { public static void Main() { } }");

        try
        {
            var document = host.GetDocument(documentId) ??
                throw new InvalidOperationException("RoslynHost did not return the created document.");
            var assemblyNames = document.Project.MetadataReferences
                .Cast<PortableExecutableReference>()
                .Select(reference => AssemblyName.GetAssemblyName(reference.FilePath!).Name!)
                .ToArray();

            Assert.DoesNotContain("KID.WPF.IDE", assemblyNames);
            Assert.DoesNotContain(assemblyNames, name => name.StartsWith("RoslynPad", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(assemblyNames, name => name.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase));

            var compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(compilation);
        }
        finally
        {
            host.CloseDocument(documentId);
        }
    }

    private async Task<Diagnostic[]> GetDocumentErrorsAsync(string code)
    {
        var host = new RoslynHostService(profileProvider).GetHost();
        var documentId = AddDocument(host, code);

        try
        {
            var document = host.GetDocument(documentId) ??
                throw new InvalidOperationException("RoslynHost did not return the created document.");
            var compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(compilation);
            return compilation
                .GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
        }
        finally
        {
            host.CloseDocument(documentId);
        }
    }

    private static DocumentId AddDocument(RoslynHost host, string code)
    {
        var text = SourceText.From(code, Encoding.UTF8);
        return host.AddDocument(new DocumentCreationArgs(
            text.Container,
            AppContext.BaseDirectory,
            SourceCodeKind.Regular,
            _ => { },
            "UserProgram.cs"));
    }
}
