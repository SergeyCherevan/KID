using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Tests.TestDoubles;
using System.Windows.Threading;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class CodeExecutionContextTests
{
    [Fact]
    public async Task Init_SameIdentityIsNoOp_ConflictingIdentityIsRejected()
    {
        using var cancellationSource = new CancellationTokenSource();
        var graphics = new TrackingGraphicsContext();
        var console = new TrackingConsoleContext();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var context = new CodeExecutionContext
        {
            ExecutionId = 17,
            CancellationToken = cancellationSource.Token,
            Dispatcher = dispatcher,
            GraphicsContext = graphics,
            ConsoleContext = console
        };

        context.Init();
        context.Init();

        Assert.Equal(1, graphics.InitCount);
        Assert.Equal(1, console.InitCount);

        context.ExecutionId = 18;
        Assert.Throws<InvalidOperationException>(context.Init);
        context.ExecutionId = 17;

        using var otherCancellationSource = new CancellationTokenSource();
        context.CancellationToken = otherCancellationSource.Token;
        Assert.Throws<InvalidOperationException>(context.Init);
        context.CancellationToken = cancellationSource.Token;

        var otherDispatcher = await Task.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.InvokeShutdown();
            return dispatcher;
        }, TestContext.Current.CancellationToken);
        context.Dispatcher = otherDispatcher;
        Assert.Throws<InvalidOperationException>(context.Init);
        context.Dispatcher = dispatcher;

        graphics.GraphicsTarget = new object();
        Assert.Throws<InvalidOperationException>(context.Init);

        await context.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(context.Init);
    }

    [Fact]
    public async Task Init_ConsoleFailureLeavesContextCleanupOnly_AndDisposesEveryOwner()
    {
        var graphics = new TrackingGraphicsContext();
        var console = new ThrowingInitConsoleContext();
        var context = new CodeExecutionContext
        {
            ExecutionId = 17,
            Dispatcher = Dispatcher.CurrentDispatcher,
            GraphicsContext = graphics,
            ConsoleContext = console
        };

        Assert.Same(console.Failure, Assert.Throws<InvalidOperationException>(context.Init));
        Assert.Throws<InvalidOperationException>(context.Init);

        await context.DisposeAsync();
        await context.DisposeAsync();

        Assert.Equal(1, graphics.InitCount);
        Assert.Equal(1, console.InitCount);
        Assert.Equal(1, graphics.BeginCleanupCount);
        Assert.Equal(1, console.BeginCleanupCount);
        Assert.Equal(1, graphics.DisposeCount);
        Assert.Equal(1, console.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_ClosesAllIngressBeforeAwaitingGraphicsCleanup()
    {
        var releaseGraphics = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var graphicsDisposeEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var graphics = new BlockingGraphicsContext(graphicsDisposeEntered, releaseGraphics);
        var console = new TrackingConsoleContext();
        var context = new CodeExecutionContext
        {
            ExecutionId = 17,
            Dispatcher = Dispatcher.CurrentDispatcher,
            GraphicsContext = graphics,
            ConsoleContext = console
        };
        context.Init();

        var disposal = context.DisposeAsync().AsTask();
        await graphicsDisposeEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(1, graphics.BeginCleanupCount);
            Assert.Equal(1, console.BeginCleanupCount);
            Assert.Equal(0, console.DisposeCount);
            Assert.False(disposal.IsCompleted);
        }
        finally
        {
            releaseGraphics.TrySetResult();
            await disposal.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, console.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_AfterPropertyMutationStillCleansOriginalOwners()
    {
        var originalGraphics = new TrackingGraphicsContext();
        var originalConsole = new TrackingConsoleContext();
        var replacementGraphics = new TrackingGraphicsContext();
        var replacementConsole = new TrackingConsoleContext();
        var context = new CodeExecutionContext
        {
            ExecutionId = 17,
            Dispatcher = Dispatcher.CurrentDispatcher,
            GraphicsContext = originalGraphics,
            ConsoleContext = originalConsole
        };
        context.Init();

        context.GraphicsContext = replacementGraphics;
        context.ConsoleContext = replacementConsole;
        await context.DisposeAsync();

        Assert.Equal(1, originalGraphics.BeginCleanupCount);
        Assert.Equal(1, originalGraphics.DisposeCount);
        Assert.Equal(1, originalConsole.BeginCleanupCount);
        Assert.Equal(1, originalConsole.DisposeCount);
        Assert.Equal(0, replacementGraphics.BeginCleanupCount);
        Assert.Equal(0, replacementGraphics.DisposeCount);
        Assert.Equal(0, replacementConsole.BeginCleanupCount);
        Assert.Equal(0, replacementConsole.DisposeCount);
    }

    [Fact]
    public async Task BeginCleanupFailure_IsStable_AndDoesNotSkipChildDispose()
    {
        var failure = new InvalidOperationException("console ingress failure");
        var graphics = new TrackingGraphicsContext();
        var console = new ThrowingBeginCleanupConsoleContext(failure);
        var context = new CodeExecutionContext
        {
            ExecutionId = 17,
            Dispatcher = Dispatcher.CurrentDispatcher,
            GraphicsContext = graphics,
            ConsoleContext = console
        };
        context.Init();

        context.BeginCleanup();
        context.BeginCleanup();
        var actual = await Record.ExceptionAsync(async () => await context.DisposeAsync());

        Assert.Same(failure, actual);
        Assert.Equal(1, console.BeginCleanupCount);
        Assert.Equal(1, console.DisposeCount);
        Assert.Equal(1, graphics.BeginCleanupCount);
        Assert.Equal(1, graphics.DisposeCount);
    }

    private sealed class ThrowingInitConsoleContext : IConsoleContext
    {
        public object ConsoleTarget { get; set; } = new();
        public InvalidOperationException Failure { get; } = new("partial console Init");
        public int InitCount { get; private set; }
        public int BeginCleanupCount { get; private set; }
        public int DisposeCount { get; private set; }

        public void Init(long executionId, CancellationToken cancellationToken)
        {
            InitCount++;
            throw Failure;
        }

        public void BeginCleanup() => BeginCleanupCount++;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingGraphicsContext(
        TaskCompletionSource disposeEntered,
        TaskCompletionSource releaseDispose) : IGraphicsContext
    {
        public object GraphicsTarget { get; set; } = new();
        public int BeginCleanupCount { get; private set; }

        public void Init(long executionId, Dispatcher dispatcher) { }

        public void BeginCleanup() => BeginCleanupCount++;

        public async ValueTask DisposeAsync()
        {
            disposeEntered.TrySetResult();
            await releaseDispose.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
    }

    private sealed class ThrowingBeginCleanupConsoleContext(Exception failure) : IConsoleContext
    {
        public object ConsoleTarget { get; set; } = new();
        public int BeginCleanupCount { get; private set; }
        public int DisposeCount { get; private set; }

        public void Init(long executionId, CancellationToken cancellationToken) { }

        public void BeginCleanup()
        {
            BeginCleanupCount++;
            throw failure;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
