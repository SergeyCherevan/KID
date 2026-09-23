using KID.Services.CodeExecution;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace KID.Tests.Console;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class KIDConsoleOutputEventLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Handlers_RunSequentiallyOffDispatcher_AndFaultDoesNotStopDelivery()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var box = new TextBox();
            _ = KIDConsole.Init(box, environment);
            using var releaseFirst = new ManualResetEventSlim();
            var firstEntered = NewCompletionSource();
            var secondCalled = NewCompletionSource();
            var order = new ConcurrentQueue<string>();
            var firstRanOnDispatcher = true;
            var firstWasReleased = false;

            KIDConsole.OutputReceived += _ =>
            {
                firstRanOnDispatcher = box.Dispatcher.CheckAccess();
                order.Enqueue("first");
                firstEntered.TrySetResult();
                firstWasReleased = releaseFirst.Wait(Timeout);
                throw new InvalidOperationException("observer failure");
            };
            KIDConsole.OutputReceived += _ =>
            {
                order.Enqueue("second");
                secondCalled.TrySetResult();
            };

            try
            {
                KIDConsole.Write("value");
                await firstEntered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

                Assert.False(firstRanOnDispatcher);
                Assert.False(secondCalled.Task.IsCompleted);

                releaseFirst.Set();
                await secondCalled.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

                Assert.True(firstWasReleased);
                Assert.Equal(["first", "second"], order);
            }
            finally
            {
                releaseFirst.Set();
                await ShutdownAsync(environmentLease, environment);
            }
        });
    }

    [Fact]
    public async Task Shutdown_WaitsForRunningHandler_DropsQueuedHandler_AndIsIdempotent()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var scope = KIDConsole.Init(new TextBox(), environment);
            using var releaseHandler = new ManualResetEventSlim();
            var handlerEntered = NewCompletionSource();
            var queuedCalls = 0;

            KIDConsole.OutputReceived += value =>
            {
                handlerEntered.TrySetResult();
                _ = releaseHandler.Wait(Timeout);
            };
            KIDConsole.OutputReceived += _ => Interlocked.Increment(ref queuedCalls);

            Task? shutdown = null;
            try
            {
                KIDConsole.Write("value");
                await handlerEntered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

                environmentLease.BeginCleanup();
                KIDConsole.BeginCleanup(environment);
                shutdown = KIDConsole.ShutdownAsync(environment).AsTask();
                var repeatedShutdown = KIDConsole.ShutdownAsync(environment).AsTask();

                Assert.Same(shutdown, repeatedShutdown);
                Assert.False(shutdown.IsCompleted);
                Assert.False(scope.EventWorker.IsAccepting);
                Assert.Equal(0, Volatile.Read(ref queuedCalls));

                releaseHandler.Set();
                await shutdown.WaitAsync(Timeout, TestContext.Current.CancellationToken);

                Assert.Equal(0, Volatile.Read(ref queuedCalls));
                Assert.True(scope.EventWorker.Completion.IsCompletedSuccessfully);
                Assert.Null(KIDConsole.CurrentScope);
            }
            finally
            {
                releaseHandler.Set();
                if (KIDConsole.CurrentScope != null)
                    await ShutdownAsync(environmentLease, environment);
                else if (shutdown != null)
                    await shutdown;
            }
        });
    }

    [Fact]
    public async Task Shutdown_DrainsAcceptedOutputWithoutStartingNewHandler()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var box = new TextBox();
            _ = KIDConsole.Init(box, environment);
            var handlerCalls = 0;
            KIDConsole.OutputReceived += _ => Interlocked.Increment(ref handlerCalls);
            using var outputAccepted = new ManualResetEventSlim();
            Task? writer = null;
            Task? shutdown = null;

            await box.Dispatcher.InvokeAsync(() =>
            {
                using var blocked = box.Dispatcher.DisableProcessing();
                writer = Task.Run(() =>
                {
                    KIDConsole.Write("accepted");
                    outputAccepted.Set();
                }, TestContext.Current.CancellationToken);
                Assert.True(outputAccepted.Wait(Timeout));
                Assert.Equal(string.Empty, box.Text);

                environmentLease.BeginCleanup();
                KIDConsole.BeginCleanup(environment);
                shutdown = KIDConsole.ShutdownAsync(environment).AsTask();

                Assert.Equal("accepted", box.Text);
                Assert.Equal(0, Volatile.Read(ref handlerCalls));
            });

            await writer!.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await shutdown!.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal("accepted", box.Text);
            Assert.Equal(0, Volatile.Read(ref handlerCalls));
        });
    }

    [Fact]
    public async Task InitAndRelease_ClearSubscribersBetweenExecutions()
    {
        await StaTest.RunAsync(async () =>
        {
            var subscribedBeforeInitCalls = 0;
            var oldSessionCalls = 0;
            KIDConsole.OutputReceived += _ =>
                Interlocked.Increment(ref subscribedBeforeInitCalls);

            using (var firstLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None))
            {
                var firstEnvironment = ExecutionEnvironmentManager.GetCurrent(1);
                _ = KIDConsole.Init(new TextBox(), firstEnvironment);
                var firstDelivered = NewCompletionSource();
                KIDConsole.OutputReceived += _ =>
                {
                    Interlocked.Increment(ref oldSessionCalls);
                    firstDelivered.TrySetResult();
                };

                KIDConsole.Write("first");
                await firstDelivered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                Assert.Equal(0, Volatile.Read(ref subscribedBeforeInitCalls));
                Assert.Equal(1, Volatile.Read(ref oldSessionCalls));
                await ShutdownAsync(firstLease, firstEnvironment);
            }

            using var nextLease = ExecutionEnvironmentManager.BeginExecution(2, CancellationToken.None);
            var nextEnvironment = ExecutionEnvironmentManager.GetCurrent(2);
            _ = KIDConsole.Init(new TextBox(), nextEnvironment);
            var nextDelivered = NewCompletionSource();
            KIDConsole.OutputReceived += _ => nextDelivered.TrySetResult();

            KIDConsole.Write("next");
            await nextDelivered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(0, Volatile.Read(ref subscribedBeforeInitCalls));
            Assert.Equal(1, Volatile.Read(ref oldSessionCalls));
            await ShutdownAsync(nextLease, nextEnvironment);
        });
    }

    [Fact]
    public async Task ReleasedSubscriberTarget_IsCollectible()
    {
        var reference = await StaTestWithResult.RunAsync(CreateReleasedObserverAsync);

        for (var attempt = 0; attempt < 10 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(reference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CreateReleasedObserverAsync()
    {
        using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        _ = KIDConsole.Init(new TextBox(), environment);
        var observer = new OutputObserver();
        KIDConsole.OutputReceived += observer.Receive;
        var reference = new WeakReference(observer);

        KIDConsole.Write("value");
        await observer.Called.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await ShutdownAsync(environmentLease, environment);
        observer = null!;
        return reference;
    }

    private static async Task ShutdownAsync(
        IExecutionEnvironmentLease environmentLease,
        ExecutionEnvironment environment)
    {
        environmentLease.BeginCleanup();
        KIDConsole.BeginCleanup(environment);
        await KIDConsole.ShutdownAsync(environment);
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class OutputObserver
    {
        internal TaskCompletionSource Called { get; } = NewCompletionSource();
        internal void Receive(string _) => Called.TrySetResult();
    }

    private static class StaTestWithResult
    {
        internal static async Task<T> RunAsync<T>(Func<Task<T>> action)
        {
            T? result = default;
            await StaTest.RunAsync(async () => result = await action());
            return result!;
        }
    }
}
