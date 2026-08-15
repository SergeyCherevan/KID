using KID.Services;
using KID.Services.CodeExecution;
using KID.Tests.TestDoubles;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class CodeExecutionServiceTests
{
    [Fact]
    public void NewService_IsIdleAndCannotBeStopped()
    {
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(
                CompilationResult.FromErrors(Array.Empty<string>())),
            new FakeCodeRunner());

        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.Null(service.CurrentExecutionId);
        Assert.False(service.IsExecutionActive);
        Assert.False(service.RequestStop());
    }

    [Fact]
    public async Task ExecuteAsync_CompilationFailure_DisposesContextAndSkipsRunner()
    {
        var compiler = FakeCodeCompiler.Returning(
            CompilationResult.FromErrors(Array.Empty<string>()));
        var runner = new FakeCodeRunner();
        var context = new TrackingCodeExecutionContext();
        var service = new CodeExecutionService(compiler, runner);

        await service.ExecuteAsync("invalid code", _ => context);

        Assert.Equal(1, context.InitCount);
        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(0, runner.CallCount);
        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.False(service.IsExecutionActive);
    }

    [Fact]
    public async Task ExecuteAsync_Success_PublishesLifecycleAndResetsStopToken()
    {
        var artifact = CreateArtifact();
        var compilationResult = CompilationResult.FromArtifact(artifact);
        var observedChanges = new List<ExecutionStateChangedEventArgs>();
        CancellationToken runnerToken = default;
        CompilationArtifact? runnerArtifact = null;
        var runner = new FakeCodeRunner((receivedArtifact, cancellationToken) =>
        {
            runnerArtifact = receivedArtifact;
            runnerToken = cancellationToken;
            Assert.Equal(cancellationToken, StopManager.CurrentToken);
            return Task.CompletedTask;
        });
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(compilationResult),
            runner);
        service.StateChanged += (_, eventArgs) =>
            observedChanges.Add(eventArgs);

        await service.ExecuteAsync("valid code", _ => new TrackingCodeExecutionContext());

        Assert.True(runnerToken.CanBeCanceled);
        Assert.Same(artifact, runnerArtifact);
        Assert.Equal(
            new[]
            {
                ExecutionState.Compiling,
                ExecutionState.Running,
                ExecutionState.CleaningUp,
                ExecutionState.Idle
            },
            observedChanges.Select(change => change.CurrentState));
        var executionId = Assert.Single(
            observedChanges.Select(change => change.ExecutionId).Distinct());
        Assert.True(executionId > 0);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
        Assert.Null(service.CurrentExecutionId);
    }

    [Fact]
    public async Task ExecuteAsync_RunnerFailure_StillDisposesContext()
    {
        var compilationResult = CompilationResult.FromArtifact(CreateArtifact());
        var compiler = FakeCodeCompiler.Returning(compilationResult);
        var runner = new FakeCodeRunner((_, _) =>
            Task.FromException(new InvalidOperationException("runner failed")));
        var context = new TrackingCodeExecutionContext();
        var service = new CodeExecutionService(compiler, runner);

        var exception = await Record.ExceptionAsync(
            () => service.ExecuteAsync("valid code", _ => context));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(1, runner.CallCount);
        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
    }

    [Fact]
    public async Task RequestStop_DuringCompilation_IsIdempotentAndWaitsForCleanup()
    {
        var compilationStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compiler = new FakeCodeCompiler(async (_, cancellationToken) =>
        {
            compilationStarted.TrySetResult(null);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CompilationResult.FromErrors(Array.Empty<string>());
        });
        var context = new TrackingCodeExecutionContext();
        var observedStates = new List<ExecutionState>();
        var service = new CodeExecutionService(compiler, new FakeCodeRunner());
        service.StateChanged += (_, eventArgs) =>
            observedStates.Add(eventArgs.CurrentState);

        var execution = service.ExecuteAsync("code", _ => context);
        await compilationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(service.RequestStop());
        Assert.Equal(ExecutionState.StopRequested, service.State);
        Assert.False(service.RequestStop());

        await execution.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(
            new[]
            {
                ExecutionState.Compiling,
                ExecutionState.StopRequested,
                ExecutionState.CleaningUp,
                ExecutionState.Idle
            },
            observedStates);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_WhileRunning_DoesNotCreateSecondContextOrCompilation()
    {
        var compilationStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCompilation = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var compiler = new FakeCodeCompiler(async (_, cancellationToken) =>
        {
            compilationStarted.TrySetResult(null);
            await releaseCompilation.Task.WaitAsync(cancellationToken);
            return CompilationResult.FromErrors(Array.Empty<string>());
        });
        var service = new CodeExecutionService(compiler, new FakeCodeRunner());
        var firstContext = new TrackingCodeExecutionContext();
        var secondFactoryCallCount = 0;

        var firstExecution = service.ExecuteAsync("first", _ => firstContext);
        await compilationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        try
        {
            await service.ExecuteAsync(
                    "second",
                    _ =>
                    {
                        secondFactoryCallCount++;
                        return new TrackingCodeExecutionContext();
                    })
                .WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            Assert.Equal(1, compiler.CallCount);
            Assert.Equal(0, secondFactoryCallCount);
            Assert.True(service.IsExecutionActive);
        }
        finally
        {
            releaseCompilation.TrySetResult(null);
            await firstExecution.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, firstContext.DisposeCount);
        Assert.Equal(ExecutionState.Idle, service.State);
    }

    private static CompilationArtifact CreateArtifact() => new(new byte[] { 1 });
}
