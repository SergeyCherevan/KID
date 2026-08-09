using KID.Services;
using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Services.CodeExecution.Interfaces;
using System.Reflection;
using System.Windows.Threading;

namespace KID.Tests.TestDoubles;

internal sealed class FakeCodeCompiler : ICodeCompiler
{
    private readonly Func<string, CancellationToken, Task<CompilationResult>> implementation;
    private int callCount;

    public FakeCodeCompiler(
        Func<string, CancellationToken, Task<CompilationResult>> implementation)
    {
        this.implementation = implementation ??
            throw new ArgumentNullException(nameof(implementation));
    }

    public int CallCount => Volatile.Read(ref callCount);

    public Task<CompilationResult> CompileAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref callCount);
        return implementation(code, cancellationToken);
    }

    public static FakeCodeCompiler Returning(CompilationResult result) =>
        new((_, _) => Task.FromResult(result));
}

internal sealed class FakeCodeRunner : ICodeRunner
{
    private readonly Func<Assembly, CancellationToken, Task> implementation;
    private int callCount;

    public FakeCodeRunner(Func<Assembly, CancellationToken, Task>? implementation = null)
    {
        this.implementation = implementation ?? ((_, _) => Task.CompletedTask);
    }

    public int CallCount => Volatile.Read(ref callCount);

    public Task RunAsync(Assembly assembly, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref callCount);
        return implementation(assembly, cancellationToken);
    }
}

internal sealed class TrackingCodeExecutionContext : ICodeExecutionContext
{
    public IGraphicsContext GraphicsContext { get; set; } = new TrackingGraphicsContext();

    public IConsoleContext ConsoleContext { get; set; } = new TrackingConsoleContext();

    public CancellationToken CancellationToken { get; set; }

    public Dispatcher Dispatcher { get; set; } = Dispatcher.CurrentDispatcher;

    public int InitCount { get; private set; }

    public int DisposeCount { get; private set; }

    public void Init()
    {
        InitCount++;
        GraphicsContext.Init();
        ConsoleContext.Init();
    }

    public void Dispose()
    {
        DisposeCount++;
        GraphicsContext.Dispose();
        ConsoleContext.Dispose();
    }
}

internal sealed class TrackingGraphicsContext : IGraphicsContext
{
    public object GraphicsTarget { get; set; } = new();

    public int InitCount { get; private set; }

    public int DisposeCount { get; private set; }

    public void Init() => InitCount++;

    public void Dispose() => DisposeCount++;
}

internal sealed class TrackingConsoleContext : IConsoleContext
{
    public object ConsoleTarget { get; set; } = new();

    public int InitCount { get; private set; }

    public int DisposeCount { get; private set; }

    public void Init() => InitCount++;

    public void Dispose() => DisposeCount++;
}
