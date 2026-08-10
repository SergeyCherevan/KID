using KID.Services.CodeExecution;
using KID.Tests.Execution;
using KID.Tests.TestDoubles;
using System.Reflection;

namespace KID.Tests.Compiler;

[Collection(ExecutionLifecycleCollection.Name)]
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

    [Fact]
    public async Task CompileAsync_InstrumentationPreservesDiagnosticLine()
    {
        const string code = """
            class Program
            {
                static void Main()
                {
                    while (false) { }
                    MissingSymbol();
                }
            }
            """;
        var expectedLine = code.Split('\n')
            .Select((line, index) => (line, index))
            .Single(item => item.line.Contains("MissingSymbol", StringComparison.Ordinal))
            .index + 1;
        var compiler = new CSharpCompiler(new StubLocalizationService());

        var result = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.StartsWith(
                $"Error_Compilation:{expectedLine}|",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompileAsync_InstrumentedLoopObservesStopRequest()
    {
        const string code = """
            public static class Program
            {
                public static void Main()
                {
                    var deadline = System.Environment.TickCount64 + 5000;
                    while (System.Environment.TickCount64 < deadline) { }
                }
            }
            """;
        var compiler = new CSharpCompiler(new StubLocalizationService());
        var result = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.Assembly?.EntryPoint);

        using var cancellationSource = new CancellationTokenSource();
        using var lease = StopManager.BeginExecution(1001, cancellationSource.Token);
        var execution = Task.Run(
            () => result.Assembly.EntryPoint.Invoke(null, null));

        await Task.Delay(50, TestContext.Current.CancellationToken);
        await cancellationSource.CancelAsync();

        var completed = await Task.WhenAny(
            execution,
            Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken));
        Assert.Same(execution, completed);

        var exception = Assert.Throws<TargetInvocationException>(
            () => execution.GetAwaiter().GetResult());
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }
}
