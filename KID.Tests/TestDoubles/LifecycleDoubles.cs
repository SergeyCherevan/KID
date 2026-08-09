using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KID.Tests.TestDoubles;

internal enum ExecutionCheckpoint
{
    StopRequested,
    ExecutionCompleted,
    CleanupCompleted
}

internal sealed class RecordingExecutionStateObserver
{
    private readonly List<ExecutionCheckpoint> checkpoints = [];

    public ReadOnlyCollection<ExecutionCheckpoint> Checkpoints => checkpoints.AsReadOnly();

    public void Record(ExecutionCheckpoint checkpoint) => checkpoints.Add(checkpoint);
}

internal sealed class FakeAudioResource : IAsyncDisposable
{
    public bool StopRequested { get; private set; }

    public bool IsDisposed { get; private set; }

    public Task StopAsync()
    {
        StopRequested = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class WpfTestHost
{
    public WpfTestHost()
    {
        Dispatcher = Dispatcher.CurrentDispatcher;
        Canvas = new Canvas();
        TextBox = new TextBox();
    }

    public Dispatcher Dispatcher { get; }

    public Canvas Canvas { get; }

    public TextBox TextBox { get; }
}
