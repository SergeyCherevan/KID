using System.Windows.Threading;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class ExecutionEnvironmentTests
{
    [Fact]
    public void BeginCleanup_KeepsIdentityForOwnerButRejectsNewRuntimeWork()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            17,
            TestContext.Current.CancellationToken);
        var environment = Assert.IsType<ExecutionEnvironment>(ExecutionEnvironmentManager.Current);

        lease.BeginCleanup();
        lease.BeginCleanup();

        Assert.True(ExecutionEnvironmentManager.IsCurrent(environment));
        Assert.False(ExecutionEnvironmentManager.IsCurrentAndAccepting(environment));
        Assert.Throws<ObjectDisposedException>(() => ExecutionEnvironmentManager.GetCurrent(17));
        Assert.Throws<InvalidOperationException>(() =>
            ExecutionEnvironmentManager.BeginExecution(
                18,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void BeginExecution_PublishesOneIdentityUsedByStopFacade()
    {
        using var cancellationSource = new CancellationTokenSource();
        using (ExecutionEnvironmentManager.BeginExecution(41, cancellationSource.Token))
        {
            var environment = Assert.IsType<ExecutionEnvironment>(
                ExecutionEnvironmentManager.Current);

            Assert.Equal(41, environment.ExecutionId);
            Assert.Equal(cancellationSource.Token, environment.CancellationToken);
            Assert.Equal(environment.CancellationToken, StopManager.CurrentToken);
            Assert.Same(environment, ExecutionEnvironmentManager.GetCurrent(41));
            Assert.Throws<InvalidOperationException>(() =>
                ExecutionEnvironmentManager.GetCurrent(42));
        }

        Assert.Null(ExecutionEnvironmentManager.Current);
        Assert.False(StopManager.CurrentToken.CanBeCanceled);
    }

    [Fact]
    public async Task Dispatcher_IsAReplaceableCapabilityOfTheSameEnvironment()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, default);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);

            var first = DispatcherManager.AttachDispatcher(1, Dispatcher.CurrentDispatcher);
            Assert.Same(environment, first.Environment);
            await first.DisposeAsync();

            Assert.True(ExecutionEnvironmentManager.IsCurrent(environment));
            Assert.Throws<InvalidOperationException>(() =>
                DispatcherManager.InvokeOnUI(() => 1));

            var second = DispatcherManager.AttachDispatcher(1, Dispatcher.CurrentDispatcher);
            Assert.Same(environment, second.Environment);
            Assert.Equal(2, DispatcherManager.InvokeOnUI(() => 2));
            await second.DisposeAsync();
        });
    }

    [Fact]
    public async Task AttachDispatcher_RejectsWrongExecutionAndDoubleAttachment()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, default);

            Assert.Throws<InvalidOperationException>(() =>
                DispatcherManager.AttachDispatcher(2, Dispatcher.CurrentDispatcher));
            var scope = DispatcherManager.AttachDispatcher(1, Dispatcher.CurrentDispatcher);
            Assert.Throws<InvalidOperationException>(() =>
                DispatcherManager.AttachDispatcher(1, Dispatcher.CurrentDispatcher));

            await scope.DisposeAsync();
        });
    }

    [Fact]
    public async Task ReleasedEnvironment_InvalidatesCapturedDispatcherScope()
    {
        await StaTest.RunAsync(async () =>
        {
            var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, default);
            var scope = DispatcherManager.AttachDispatcher(1, Dispatcher.CurrentDispatcher);

            environmentLease.Dispose();

            Assert.Throws<ObjectDisposedException>(() => scope.CheckAccess());
            Assert.Throws<InvalidOperationException>(() =>
                DispatcherManager.InvokeOnUI(() => 1));
            await scope.DisposeAsync();
        });
    }
}
