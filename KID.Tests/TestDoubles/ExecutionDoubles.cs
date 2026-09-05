using KID.Services;
using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Services.CodeExecution.Interfaces;
using Microsoft.VisualStudio.Threading;
using System.Windows.Threading;

namespace KID.Tests.TestDoubles;

internal static class TestThreading
{
    private static readonly JoinableTaskContext JoinableTaskContext = new();

    public static JoinableTaskFactory JoinableTaskFactory => JoinableTaskContext.Factory;
}

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
    private int startCount;
    private int callCount;
    private int disposeCount;

    public FakeCodeRunner(
        Func<CompilationArtifact, CancellationToken, Task>? implementation = null,
        Action? disposeAction = null)
    {
        this.implementation = implementation ?? ((_, _) => Task.CompletedTask);
        this.disposeAction = disposeAction;
    }

    public int StartCount => Volatile.Read(ref startCount);

    public int CallCount => Volatile.Read(ref callCount);

    public int DisposeCount => Volatile.Read(ref disposeCount);

    public ICodeRunningInstance Start(
        CompilationArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Interlocked.Increment(ref startCount);
        return new FakeCodeRunningInstance(this, artifact, cancellationToken);
    }

    private sealed class FakeCodeRunningInstance : ICodeRunningInstance
    {
        private readonly FakeCodeRunner owner;
        private readonly CompilationArtifact artifact;
        private int isDisposed;

        public FakeCodeRunningInstance(
            FakeCodeRunner owner,
            CompilationArtifact artifact,
            CancellationToken cancellationToken)
        {
            this.owner = owner;
            this.artifact = artifact;
            Completion = TestThreading.JoinableTaskFactory.RunAsync(
                () => ExecuteAsync(cancellationToken));
        }

        public JoinableTask Completion { get; }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref owner.callCount);
            await owner.implementation(artifact, cancellationToken);
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
    public long ExecutionId { get; set; }

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
        ConsoleContext.Init(ExecutionId, CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        DisposeCount++;
        try
        {
            GraphicsContext.Dispose();
            await ConsoleContext.DisposeAsync();
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

    public void Init(long executionId, CancellationToken cancellationToken) => InitCount++;

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
