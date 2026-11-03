namespace KID.Tests.Library;

public sealed class ExecutionDiagnosticsTests
{
    [Fact]
    public async Task EventWorker_ReportsHandlerFailureToExecutionDiagnosticSink()
    {
        var diagnostics = new List<ExecutionDiagnostic>();
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            901,
            CancellationToken.None,
            diagnostics.Add);
        var environment = ExecutionEnvironmentManager.Current;
        Assert.NotNull(environment);
        var delivered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var worker = new ExecutionEventWorker(environment, "Keyboard");

        Assert.True(worker.TryEnqueue(static () => throw new InvalidOperationException("handler")));
        Assert.True(worker.TryEnqueue(() => delivered.TrySetResult()));

        await delivered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await worker.ShutdownAsync();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(901, diagnostic.ExecutionId);
        Assert.Equal("Keyboard", diagnostic.Component);
        Assert.Equal("EventHandler", diagnostic.Operation);
        Assert.IsType<InvalidOperationException>(diagnostic.Exception);
    }
}
