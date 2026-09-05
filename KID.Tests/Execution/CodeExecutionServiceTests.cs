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
        Assert.Equal(0, runner.StartCount);
        Assert.Equal(0, runner.CallCount);
        Assert.Equal(0, runner.DisposeCount);
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
        Assert.Equal(1, runner.StartCount);
        Assert.Equal(1, runner.CallCount);
        Assert.Equal(1, runner.DisposeCount);
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
    public async Task ExecuteAsync_CompletionFailure_DisposesContextThenRunningInstance()
    {
        var cleanupOrder = new List<string>();
        var compilationResult = CompilationResult.FromArtifact(CreateArtifact());
        var compiler = FakeCodeCompiler.Returning(compilationResult);
        var runner = new FakeCodeRunner(
            (_, _) => Task.FromException(new InvalidOperationException("runner failed")),
            disposeAction: () => cleanupOrder.Add("running instance"));
        var context = new TrackingCodeExecutionContext(() => cleanupOrder.Add("execution context"));
        var service = new CodeExecutionService(compiler, runner);

        var exception = await Record.ExceptionAsync(
            () => service.ExecuteAsync("valid code", _ => context));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(new[] { "execution context", "running instance" }, cleanupOrder);
        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(1, runner.StartCount);
        Assert.Equal(1, runner.CallCount);
        Assert.Equal(1, runner.DisposeCount);
        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_ContextDisposeFails_StillDisposesRunningInstanceAfterContext()
    {
        var cleanupOrder = new List<string>();
        var contextException = new InvalidOperationException("context dispose failed");
        var runner = new FakeCodeRunner(
            disposeAction: () => cleanupOrder.Add("running instance"));
        var context = new TrackingCodeExecutionContext(() =>
        {
            cleanupOrder.Add("execution context");
            throw contextException;
        });
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(
                CompilationResult.FromArtifact(CreateArtifact())),
            runner);

        var exception = await Record.ExceptionAsync(
            () => service.ExecuteAsync("valid code", _ => context));

        Assert.Same(contextException, exception);
        Assert.Equal(
            new[] { "execution context", "running instance" },
            cleanupOrder);
        Assert.Equal(1, runner.DisposeCount);
        Assert.Equal(ExecutionState.CleaningUp, service.State);
        Assert.True(service.IsExecutionActive);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_AllCleanupStepsFail_AttemptsEveryStepAndKeepsRunBlocked()
    {
        var cleanupOrder = new List<string>();
        var contextException = new InvalidOperationException("context dispose failed");
        var runningInstanceException = new InvalidOperationException("running instance dispose failed");
        var leaseException = new InvalidOperationException("lease dispose failed");
        var sessionException = new InvalidOperationException("session dispose failed");
        var compiler = FakeCodeCompiler.Returning(
            CompilationResult.FromArtifact(CreateArtifact()));
        var runner = new FakeCodeRunner(disposeAction: () =>
        {
            cleanupOrder.Add("running instance");
            throw runningInstanceException;
        });
        var context = new TrackingCodeExecutionContext(() =>
        {
            cleanupOrder.Add("execution context");
            throw contextException;
        });
        var service = new CodeExecutionService(
            compiler,
            runner,
            executionId => new ExecutionSession(
                executionId,
                new ThrowingCancellationTokenSource(() =>
                {
                    cleanupOrder.Add("session");
                    throw sessionException;
                })),
            (executionId, cancellationToken) => new DelegatingDisposable(
                StopManager.BeginExecution(executionId, cancellationToken),
                () =>
                {
                    cleanupOrder.Add("stop manager lease");
                    throw leaseException;
                }));

        var exception = await Record.ExceptionAsync(() =>
            service.ExecuteAsync("valid code", _ => context).WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));

        var aggregateException = Assert.IsType<AggregateException>(exception);
        Assert.Equal(
            new Exception[]
            {
                contextException,
                runningInstanceException,
                leaseException,
                sessionException
            },
            aggregateException.InnerExceptions);
        Assert.Equal(
            new[]
            {
                "execution context",
                "running instance",
                "stop manager lease",
                "session"
            },
            cleanupOrder);
        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(1, runner.DisposeCount);
        Assert.Equal(ExecutionState.CleaningUp, service.State);
        Assert.True(service.IsExecutionActive);
        Assert.NotNull(service.CurrentExecutionId);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);

        var secondContextCount = 0;
        await service.ExecuteAsync(
            "second run",
            _ =>
            {
                secondContextCount++;
                return new TrackingCodeExecutionContext();
            });

        Assert.Equal(0, secondContextCount);
        Assert.Equal(1, compiler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutionAndCleanupFail_PreservesPrimaryAndSecondaryErrors()
    {
        var executionException = new InvalidOperationException("execution failed");
        var cleanupException = new InvalidOperationException("cleanup failed");
        var runner = new FakeCodeRunner(
            (_, _) => Task.FromException(executionException));
        var context = new TrackingCodeExecutionContext(() => throw cleanupException);
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(
                CompilationResult.FromArtifact(CreateArtifact())),
            runner);

        var exception = await Record.ExceptionAsync(() =>
            service.ExecuteAsync("valid code", _ => context).WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));

        var aggregateException = Assert.IsType<AggregateException>(exception);
        Assert.Equal(
            new Exception[] { executionException, cleanupException },
            aggregateException.InnerExceptions);
        Assert.Equal(1, runner.DisposeCount);
        Assert.Equal(ExecutionState.CleaningUp, service.State);
        Assert.True(service.IsExecutionActive);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_StopAndCleanupFail_ReportsCleanupFailureInsteadOfCancellation()
    {
        var compilationStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compiler = new FakeCodeCompiler(async (_, cancellationToken) =>
        {
            compilationStarted.TrySetResult(null);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CompilationResult.FromErrors(Array.Empty<string>());
        });
        var cleanupException = new InvalidOperationException("cleanup after stop failed");
        var context = new TrackingCodeExecutionContext(() => throw cleanupException);
        var service = new CodeExecutionService(compiler, new FakeCodeRunner());
        var execution = service.ExecuteAsync("code", _ => context);

        await compilationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        Assert.True(service.RequestStop());

        var exception = await Record.ExceptionAsync(() => execution.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));

        Assert.Same(cleanupException, exception);
        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(ExecutionState.CleaningUp, service.State);
        Assert.True(service.IsExecutionActive);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
    }

    [Theory]
    [InlineData(ExecutionState.CleaningUp)]
    [InlineData(ExecutionState.Idle)]
    public async Task ExecuteAsync_StateChangedSubscriberFails_IsolatesObserverAndCompletesLifecycle(
        ExecutionState failingState)
    {
        var observerException = new InvalidOperationException(
            $"observer failed at {failingState}");
        var statesSeenBySecondObserver = new List<ExecutionState>();
        var compiler = FakeCodeCompiler.Returning(
            CompilationResult.FromArtifact(CreateArtifact()));
        var service = new CodeExecutionService(compiler, new FakeCodeRunner());
        EventHandler<ExecutionStateChangedEventArgs> failingObserver = (_, eventArgs) =>
        {
            if (eventArgs.CurrentState == failingState)
                throw observerException;
        };
        service.StateChanged += failingObserver;
        service.StateChanged += (_, eventArgs) =>
            statesSeenBySecondObserver.Add(eventArgs.CurrentState);

        var exception = await Record.ExceptionAsync(() =>
            service.ExecuteAsync(
                    "valid code",
                    _ => new TrackingCodeExecutionContext())
                .WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken));

        Assert.Same(observerException, exception);
        Assert.Contains(failingState, statesSeenBySecondObserver);
        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.False(service.IsExecutionActive);
        Assert.Null(service.CurrentExecutionId);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);

        service.StateChanged -= failingObserver;
        await service.ExecuteAsync(
                "second run",
                _ => new TrackingCodeExecutionContext())
            .WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        Assert.Equal(2, compiler.CallCount);
        Assert.Equal(ExecutionState.Idle, service.State);
    }

    [Fact]
    public async Task ExecuteAsync_CompleteSessionTransitionFails_CompletesTaskAndKeepsRunBlocked()
    {
        ExecutionSession? createdSession = null;
        var compiler = FakeCodeCompiler.Returning(
            CompilationResult.FromArtifact(CreateArtifact()));
        var service = new CodeExecutionService(
            compiler,
            new FakeCodeRunner(),
            executionId => createdSession = new ExecutionSession(executionId),
            static (executionId, cancellationToken) =>
                StopManager.BeginExecution(executionId, cancellationToken));
        service.StateChanged += (_, eventArgs) =>
        {
            if (eventArgs.CurrentState == ExecutionState.CleaningUp)
            {
                /* Fault injection: нарушаем private FSM после публикации CleaningUp, чтобы
                 * следующий внутренний CompleteSession получил недопустимый Idle -> Idle.
                 */
                createdSession!.TransitionTo(ExecutionState.Idle);
            }
        };

        var exception = await Record.ExceptionAsync(() =>
            service.ExecuteAsync(
                    "valid code",
                    _ => new TrackingCodeExecutionContext())
                .WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken));

        var transitionException = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Idle -> Idle", transitionException.Message);
        Assert.True(service.IsExecutionActive);
        Assert.NotNull(service.CurrentExecutionId);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);

        var secondContextCount = 0;
        await service.ExecuteAsync(
            "second run",
            _ =>
            {
                secondContextCount++;
                return new TrackingCodeExecutionContext();
            });

        Assert.Equal(0, secondContextCount);
        Assert.Equal(1, compiler.CallCount);
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

    [Fact]
    public async Task ExecuteAsync_PendingCompletion_DelaysCleanupAndRejectsSecondRun()
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeCodeRunner(
            async (_, cancellationToken) =>
                await completion.Task.WaitAsync(cancellationToken));
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(CompilationResult.FromArtifact(CreateArtifact())),
            runner);
        var context = new TrackingCodeExecutionContext();
        var execution = service.ExecuteAsync("first", _ => context);

        try
        {
            Assert.Equal(ExecutionState.Running, service.State);
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, context.DisposeCount);
            Assert.Equal(0, runner.DisposeCount);
            var secondContextCount = 0;

            await service.ExecuteAsync("second", _ =>
            {
                secondContextCount++;
                return new TrackingCodeExecutionContext();
            }).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(0, secondContextCount);
            Assert.Equal(1, runner.CallCount);
        }
        finally
        {
            completion.TrySetResult(null);
            await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(1, runner.DisposeCount);
        Assert.Equal(ExecutionState.Idle, service.State);
    }

    private static CompilationArtifact CreateArtifact() => new(new byte[] { 1 });

    private sealed class DelegatingDisposable : IDisposable
    {
        private readonly IDisposable inner;
        private readonly Action afterDispose;

        public DelegatingDisposable(IDisposable inner, Action afterDispose)
        {
            this.inner = inner;
            this.afterDispose = afterDispose;
        }

        public void Dispose()
        {
            inner.Dispose();
            afterDispose();
        }
    }

    private sealed class ThrowingCancellationTokenSource : CancellationTokenSource
    {
        private readonly Action disposeAction;

        public ThrowingCancellationTokenSource(Action disposeAction)
        {
            this.disposeAction = disposeAction;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                disposeAction();
        }
    }
}
