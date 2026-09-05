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
}
