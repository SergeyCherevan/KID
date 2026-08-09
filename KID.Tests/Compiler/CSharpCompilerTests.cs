using KID.Services.CodeExecution;
using KID.Tests.TestDoubles;

namespace KID.Tests.Compiler;

public sealed class CSharpCompilerTests
{
    [Fact]
    public async Task CompileAsync_ValidProgram_ReturnsAssemblyWithEntryPoint()
    {
        const string code = "public static class Program { public static void Main() { } }";
        var compiler = new CSharpCompiler(new StubLocalizationService());

        var result = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Assembly);
        Assert.NotNull(result.Assembly.EntryPoint);
    }

    [Fact]
    public async Task CompileAsync_InvalidProgram_ReturnsLocalizedCompilationError()
    {
        const string code = "public static class Program { public static void Main( { } }";
        var compiler = new CSharpCompiler(new StubLocalizationService());

        var result = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.StartsWith("Error_Compilation:", error, StringComparison.Ordinal);
        Assert.Null(result.Assembly);
    }

    [Fact]
    public async Task CompileAsync_PreCancelledToken_CancelsCompilation()
    {
        const string code = "public static class Program { public static void Main() { } }";
        var compiler = new CSharpCompiler(new StubLocalizationService());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var exception = await Record.ExceptionAsync(
            () => compiler.CompileAsync(code, cancellationSource.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }
}
