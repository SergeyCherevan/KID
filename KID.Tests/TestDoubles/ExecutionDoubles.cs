using KID.Services;
using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Services.CodeExecution.Interfaces;
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
    private readonly Func<CompilationArtifact, CancellationToken, Task> implementation;
    private readonly Action? disposeAction;
    private int createCount;
    private int callCount;
    private int disposeCount;

    public FakeCodeRunner(
        Func<CompilationArtifact, CancellationToken, Task>? implementation = null,
        Action? disposeAction = null)
    {
        this.implementation = implementation ?? ((_, _) => Task.CompletedTask);
        this.disposeAction = disposeAction;
    }

    public int CreateCount => Volatile.Read(ref createCount);

    public int CallCount => Volatile.Read(ref callCount);

    public int DisposeCount => Volatile.Read(ref disposeCount);

    public ICodeExecutionHandle CreateExecution(CompilationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Interlocked.Increment(ref createCount);
        return new FakeCodeExecutionHandle(this, artifact);
    }

    private sealed class FakeCodeExecutionHandle : ICodeExecutionHandle
    {
        private readonly FakeCodeRunner owner;
        private readonly CompilationArtifact artifact;
        private int isDisposed;

        public FakeCodeExecutionHandle(
            FakeCodeRunner owner,
            CompilationArtifact artifact)
        {
            this.owner = owner;
            this.artifact = artifact;
        }

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref owner.callCount);
            return owner.implementation(artifact, cancellationToken);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref isDisposed, 1) != 0)
                return;

            Interlocked.Increment(ref owner.disposeCount);
            owner.disposeAction?.Invoke();
        }
    }
}

internal sealed class TrackingCodeExecutionContext : ICodeExecutionContext
{
    private readonly Action? disposeAction;

    public TrackingCodeExecutionContext(Action? disposeAction = null)
    {
        this.disposeAction = disposeAction;
    }

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
        try
        {
            GraphicsContext.Dispose();
            ConsoleContext.Dispose();
        }
        finally
        {
            disposeAction?.Invoke();
        }
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
