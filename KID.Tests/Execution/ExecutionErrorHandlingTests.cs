using KID.Services.CodeExecution;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class ExecutionErrorHandlingTests
{
    [Fact]
    public void IsExpectedStop_RequiresCancellationExceptionAndCanceledSessionToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationException = new OperationCanceledException();

        Assert.False(
            ExecutionExceptionClassifier.IsExpectedStop(
                cancellationException,
                cancellationSource.Token));

        cancellationSource.Cancel();

        Assert.True(
            ExecutionExceptionClassifier.IsExpectedStop(
                cancellationException,
                cancellationSource.Token));
        Assert.False(
            ExecutionExceptionClassifier.IsExpectedStop(
                new InvalidOperationException("not a cancellation"),
                cancellationSource.Token));
    }

    [Fact]
    public void CreateException_ReturnsNullOrOriginalExceptionWithoutExtraWrapper()
    {
        var failures = new ExecutionFailureCollector();
        var originalException = new InvalidOperationException("primary");

        Assert.Null(failures.CreateException("Multiple lifecycle errors."));

        failures.Add(originalException);

        Assert.Same(
            originalException,
            failures.CreateException("Multiple lifecycle errors."));
    }

    [Fact]
    public void CaptureAndDrainTo_PreserveFailurePriorityAndContinueAfterFailure()
    {
        var primaryException = new InvalidOperationException("primary");
        var cleanupException = new ApplicationException("cleanup");
        var observerException = new NotSupportedException("observer");
        var lifecycleFailures = new ExecutionFailureCollector();
        var observerFailures = new ExecutionFailureCollector();
        bool finalStepExecuted = false;

        lifecycleFailures.Add(primaryException);
        Assert.False(lifecycleFailures.Capture(() => throw cleanupException));
        observerFailures.Add(observerException);
        Assert.True(lifecycleFailures.Capture(() => finalStepExecuted = true));

        observerFailures.DrainTo(lifecycleFailures);

        Assert.True(finalStepExecuted);
        Assert.Null(observerFailures.CreateException("Multiple observer errors."));
        var aggregateException = Assert.IsType<AggregateException>(
            lifecycleFailures.CreateException("Multiple lifecycle errors."));
        Assert.Equal(
            new Exception[] { primaryException, cleanupException, observerException },
            aggregateException.InnerExceptions);
    }

    [Fact]
    public void Capture_RunsFinallyAfterSuccessAndReportsItsFailure()
    {
        var finallyException = new InvalidOperationException("finally");
        var failures = new ExecutionFailureCollector();
        bool catchActionExecuted = false;

        bool succeeded = failures.Capture(
            () => { },
            catchAction: () => catchActionExecuted = true,
            finallyAction: () => throw finallyException);

        Assert.False(succeeded);
        Assert.False(catchActionExecuted);
        Assert.Same(
            finallyException,
            failures.CreateException("Multiple lifecycle errors."));
    }

    [Fact]
    public async Task CaptureAsync_RunsCatchAndFinallyActionsAndPreservesFailureOrder()
    {
        var actionException = new InvalidOperationException("action");
        var catchException = new ApplicationException("catch");
        var finallyException = new NotSupportedException("finally");
        var failures = new ExecutionFailureCollector();
        var executionOrder = new List<string>();

        bool succeeded = await failures.CaptureAsync(
            async () =>
            {
                await Task.Yield();
                executionOrder.Add("action");
                throw actionException;
            },
            catchAction: () =>
            {
                executionOrder.Add("catch");
                throw catchException;
            },
            finallyAction: () =>
            {
                executionOrder.Add("finally");
                throw finallyException;
            });

        Assert.False(succeeded);
        Assert.Equal(new[] { "action", "catch", "finally" }, executionOrder);
        var aggregateException = Assert.IsType<AggregateException>(
            failures.CreateException("Multiple lifecycle errors."));
        Assert.Equal(
            new Exception[] { actionException, catchException, finallyException },
            aggregateException.InnerExceptions);
    }
}
