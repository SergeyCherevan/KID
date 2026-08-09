namespace KID.Tests.Library;

public sealed class StopManagerTests
{
    [Fact]
    public void StopIfButtonPressed_DefaultToken_DoesNotThrow()
    {
        var previousToken = StopManager.CurrentToken;

        try
        {
            StopManager.CurrentToken = default;
            StopManager.StopIfButtonPressed();
        }
        finally
        {
            StopManager.CurrentToken = previousToken;
        }
    }

    [Fact]
    public void StopIfButtonPressed_CancelledToken_ThrowsOperationCanceledException()
    {
        var previousToken = StopManager.CurrentToken;
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        try
        {
            StopManager.CurrentToken = cancellationSource.Token;

            Assert.Throws<OperationCanceledException>(StopManager.StopIfButtonPressed);
        }
        finally
        {
            StopManager.CurrentToken = previousToken;
        }
    }
}
