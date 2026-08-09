using KID.Tests.Execution;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class StopManagerTests
{
    [Fact]
    public void CurrentToken_IsReadOnlyForUserCode()
    {
        var property = typeof(StopManager).GetProperty(nameof(StopManager.CurrentToken));

        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    [Fact]
    public void StopIfButtonPressed_WithoutActiveExecution_DoesNotThrow()
    {
        Assert.False(StopManager.CurrentToken.CanBeCanceled);

        StopManager.StopIfButtonPressed();
    }

    [Fact]
    public void ExecutionLease_PublishesCancellationAndResetsTokenOnDispose()
    {
        using var cancellationSource = new CancellationTokenSource();
        var lease = StopManager.BeginExecution(1, cancellationSource.Token);

        try
        {
            Assert.Equal(cancellationSource.Token, StopManager.CurrentToken);

            cancellationSource.Cancel();

            Assert.Throws<OperationCanceledException>(StopManager.StopIfButtonPressed);
        }
        finally
        {
            lease.Dispose();
        }

        Assert.False(StopManager.CurrentToken.CanBeCanceled);
        StopManager.StopIfButtonPressed();
    }

    [Fact]
    public void BeginExecution_WhileTokenIsActive_IsRejected()
    {
        using var firstCancellationSource = new CancellationTokenSource();
        using var secondCancellationSource = new CancellationTokenSource();
        using var firstLease = StopManager.BeginExecution(1, firstCancellationSource.Token);

        Assert.Throws<InvalidOperationException>(() =>
            StopManager.BeginExecution(2, secondCancellationSource.Token));
        Assert.Equal(firstCancellationSource.Token, StopManager.CurrentToken);
    }

    [Fact]
    public void DisposingOldLeaseAgain_DoesNotResetNewExecutionToken()
    {
        using var firstCancellationSource = new CancellationTokenSource();
        using var secondCancellationSource = new CancellationTokenSource();
        var firstLease = StopManager.BeginExecution(1, firstCancellationSource.Token);
        firstLease.Dispose();

        using var secondLease = StopManager.BeginExecution(2, secondCancellationSource.Token);

        firstLease.Dispose();

        Assert.Equal(secondCancellationSource.Token, StopManager.CurrentToken);
    }
}
