using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;

namespace KID.Tests.Lifecycle;

public sealed class WpfTestHostTests
{
    [Fact]
    public async Task StaTest_CreatesWpfControlsOnOwningDispatcherWithoutVisibleWindow()
    {
        await StaTest.RunAsync(() =>
        {
            var host = new WpfTestHost();

            Assert.True(host.Dispatcher.CheckAccess());
            Assert.Same(host.Dispatcher, host.Canvas.Dispatcher);
            Assert.Same(host.Dispatcher, host.TextBox.Dispatcher);
        });
    }

    [Fact]
    public async Task LifecycleDoubles_RecordStopExecutionAndCleanupAsSeparateSteps()
    {
        var observer = new RecordingExecutionStateObserver();
        var audio = new FakeAudioResource();

        observer.Record(ExecutionCheckpoint.StopRequested);
        await audio.StopAsync();
        observer.Record(ExecutionCheckpoint.ExecutionCompleted);
        await audio.DisposeAsync();
        observer.Record(ExecutionCheckpoint.CleanupCompleted);

        Assert.True(audio.StopRequested);
        Assert.True(audio.IsDisposed);
        Assert.Equal(
            [
                ExecutionCheckpoint.StopRequested,
                ExecutionCheckpoint.ExecutionCompleted,
                ExecutionCheckpoint.CleanupCompleted
            ],
            observer.Checkpoints);
    }
}
