using System.Runtime.CompilerServices;
using System.Windows.Controls;
using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Compilation;
using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Runtime;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class MusicLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Shutdown_ClosesRegistrationStopsAwaitsDisposesAndIsIdempotent()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            1,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime();
        var scope = Music.Init(environment, runtime);

        try
        {
            Music.SoundPlay(440, 60_000);
            await runtime.WaitForOutputsAsync(1);

            var first = Music.ShutdownAsync(environment).AsTask();
            var second = Music.ShutdownAsync(environment).AsTask();

            Assert.Same(first, second);
            Assert.False(scope.IsAccepting);
            await first.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            var output = Assert.Single(runtime.Outputs);
            Assert.True(output.StopCount >= 1);
            Assert.Equal(1, output.DisposeCount);
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.Equal(0, scope.BackgroundTaskCount);
            Assert.Equal(0, scope.TemporaryFileCount);
            Assert.Null(Music.CurrentScope);
        }
        finally
        {
            await IgnoreCleanupFailureAsync(environment);
        }
    }

    [Fact]
    public async Task SoundPlayerOff_StopsCurrentSoundsButKeepsSessionOpen()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            1,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime();
        var scope = Music.Init(environment, runtime);

        try
        {
            Music.SoundPlay(440, 60_000);
            await runtime.WaitForOutputsAsync(1);
            Music.SoundPlayerOFF();

            Assert.True(scope.IsAccepting);
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.Equal(0, scope.BackgroundTaskCount);

            Music.SoundPlay(660, 60_000);
            await runtime.WaitForOutputsAsync(2);
            Assert.Equal(1, scope.ActiveSoundCount);
        }
        finally
        {
            await IgnoreCleanupFailureAsync(environment);
        }
    }

    [Fact]
    public async Task PreviousRunHandle_CannotControlNextRunWhenNumericIdIsReused()
    {
        SoundPlayer oldPlayer;
        using (var firstLease = ExecutionEnvironmentManager.BeginExecution(
                   1,
                   TestContext.Current.CancellationToken))
        {
            var firstEnvironment = ExecutionEnvironmentManager.GetCurrent(1);
            var firstRuntime = new FakeMusicRuntime();
            Music.Init(firstEnvironment, firstRuntime);
            oldPlayer = Music.SoundPlay(440, 60_000);
            await firstRuntime.WaitForOutputsAsync(1);
            await Music.ShutdownAsync(firstEnvironment).AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }

        using var secondLease = ExecutionEnvironmentManager.BeginExecution(
            2,
            TestContext.Current.CancellationToken);
        var secondEnvironment = ExecutionEnvironmentManager.GetCurrent(2);
        var secondRuntime = new FakeMusicRuntime();
        var secondScope = Music.Init(secondEnvironment, secondRuntime);

        try
        {
            var currentPlayer = Music.SoundPlay(660, 60_000);
            await secondRuntime.WaitForOutputsAsync(1);
            Assert.Equal(oldPlayer.Id, currentPlayer.Id);

            oldPlayer.SoundVolume(0);
            oldPlayer.SoundStop();
            oldPlayer.Dispose();

            Assert.Equal(1, secondScope.ActiveSoundCount);
            Assert.Equal(PlaybackState.Playing, currentPlayer.SoundState());
            Assert.Equal(0, secondRuntime.Outputs[0].StopCount);
        }
        finally
        {
            await IgnoreCleanupFailureAsync(secondEnvironment);
        }
    }

    [Fact]
    public async Task Shutdown_CancelsFileIoAndDeletesTrackedTemporaryFile()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            1,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime { BlockWrite = true };
        var scope = Music.Init(environment, runtime);

        try
        {
            Music.SoundPlay("https://example.test/audio.mp3");
            await runtime.WriteStarted.Task.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken);

            await Music.ShutdownAsync(environment).AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Single(runtime.DeletedFiles);
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.Equal(0, scope.BackgroundTaskCount);
            Assert.Equal(0, scope.TemporaryFileCount);
        }
        finally
        {
            await IgnoreCleanupFailureAsync(environment);
        }
    }

    [Fact]
    public async Task Shutdown_AttemptsEveryResourceAndReleasesStaticOwnerAfterFailures()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            1,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime
        {
            ThrowOnStop = true,
            ThrowOnDispose = true
        };
        var scope = Music.Init(environment, runtime);

        Music.SoundPlay(440, 60_000);
        Music.SoundPlay(660, 60_000);
        await runtime.WaitForOutputsAsync(2);

        var error = await Record.ExceptionAsync(async () =>
            await Music.ShutdownAsync(environment).AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken));

        Assert.NotNull(error);
        Assert.All(runtime.Outputs, output =>
        {
            Assert.True(output.StopCount >= 1);
            Assert.Equal(1, output.DisposeCount);
        });
        Assert.Equal(0, scope.ActiveSoundCount);
        Assert.Equal(0, scope.BackgroundTaskCount);
        Assert.Null(Music.CurrentScope);
    }

    [Fact]
    public async Task SessionCancellation_InterruptsBlockingSound()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, stop.Token);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime();
        Music.Init(environment, runtime);

        try
        {
            var userCall = Task.Run(
                () => Music.Sound(440, 60_000),
                TestContext.Current.CancellationToken);
            await runtime.WaitForOutputsAsync(1);
            await stop.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await userCall.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        }
        finally
        {
            await IgnoreCleanupFailureAsync(environment);
        }
    }

    [Fact]
    public async Task NoteEnumeration_ChecksCapturedSessionTokenBetweenIterations()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, stop.Token);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime();
        var scope = Music.Init(environment, runtime);

        IEnumerable<SoundNote> CancelDuringEnumeration()
        {
            yield return new SoundNote(440, 10);
            stop.Cancel();
            yield return new SoundNote(660, 10);
        }

        try
        {
            Assert.ThrowsAny<OperationCanceledException>(() =>
                Music.SoundPlay(CancelDuringEnumeration()));
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.Empty(runtime.Outputs);
        }
        finally
        {
            await IgnoreCleanupFailureAsync(environment);
        }
    }

    [Fact]
    public async Task CanvasContext_PartialRuntimeInitializationStillShutsMusicDown()
    {
        await StaTest.RunAsync(async () =>
        {
            using var lease = ExecutionEnvironmentManager.BeginExecution(
                1,
                TestContext.Current.CancellationToken);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var runtime = new FakeMusicRuntime();
            var failure = new InvalidOperationException("after Music.Init");
            var canvas = new Canvas();
            var context = new CanvasGraphicsContext(canvas, (_, currentEnvironment) =>
            {
                Music.Init(currentEnvironment, runtime);
                throw failure;
            });

            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                context.Init(1, canvas.Dispatcher)));
            Assert.NotNull(Music.CurrentScope);

            await context.DisposeAsync().AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Null(Music.CurrentScope);
            Assert.Null(Graphics.Canvas);
            Assert.Null(context.GraphicsTarget);
            await IgnoreCleanupFailureAsync(environment);
        });
    }

    [Fact]
    public async Task RepeatedRuns_LeaveNoAudioTasksOrRegistrationsAndReleaseLoadContexts()
    {
        await StaTest.RunAsync(async () =>
        {
            var localization = new StubLocalizationService();
            var compilation = await new CSharpCompiler(localization).CompileAsync(
                """
                public static class Program
                {
                    public static void Main()
                    {
                        KID.Music.SoundPlay(440, 60000);
                    }
                }
                """,
                TestContext.Current.CancellationToken);
            var artifact = Assert.IsType<CompilationArtifact>(compilation.Artifact);
            var references = new List<WeakReference>();

            for (var executionId = 1; executionId <= 8; executionId++)
                references.Add(await RunCompiledMusicAsync(
                    executionId,
                    artifact,
                    localization));

            for (var attempt = 0;
                 attempt < 10 && references.Any(reference => reference.IsAlive);
                 attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => { });
            }

            Assert.All(references, reference => Assert.False(reference.IsAlive));
            Assert.Null(Music.CurrentScope);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> RunCompiledMusicAsync(
        long executionId,
        CompilationArtifact artifact,
        StubLocalizationService localization)
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            executionId,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(executionId);
        var runtime = new FakeMusicRuntime();
        var scope = Music.Init(environment, runtime);
        var runner = new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory);
        var running = Assert.IsType<CollectibleCodeRunningInstance>(
            runner.Start(artifact, TestContext.Current.CancellationToken));

        try
        {
            await running.Completion.Task.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken);
            await runtime.WaitForOutputsAsync(1);
            await Music.ShutdownAsync(environment).AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.Equal(0, scope.BackgroundTaskCount);
            return Assert.IsType<WeakReference>(running.LoadContextReference);
        }
        finally
        {
            await IgnoreCleanupFailureAsync(environment);
            running.Dispose();
        }
    }

    private static async Task IgnoreCleanupFailureAsync(ExecutionEnvironment environment) =>
        _ = await Record.ExceptionAsync(async () => await Music.ShutdownAsync(environment));

    private sealed class FakeMusicRuntime : IMusicRuntime
    {
        private readonly object gate = new();
        private readonly HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        internal List<FakeMusicOutput> Outputs { get; } = [];
        internal List<string> DeletedFiles { get; } = [];
        internal TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BlockWrite { get; init; }
        internal bool ThrowOnStop { get; init; }
        internal bool ThrowOnDispose { get; init; }

        public IMusicOutput CreateOutput()
        {
            var output = new FakeMusicOutput(ThrowOnStop, ThrowOnDispose);
            lock (gate)
            {
                Outputs.Add(output);
            }
            return output;
        }

        public IMusicFileSource OpenFile(string path) => new FakeMusicFileSource();

        public Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<byte[]>([1, 2, 3]);
        }

        public string CreateTemporaryPath()
        {
            var path = $"fake-{Guid.NewGuid():N}.tmp";
            lock (gate)
            {
                files.Add(path);
            }
            return path;
        }

        public async Task WriteAllBytesAsync(
            string path,
            byte[] content,
            CancellationToken cancellationToken)
        {
            WriteStarted.TrySetResult();
            if (BlockWrite)
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                files.Add(path);
            }
        }

        public bool FileExists(string path)
        {
            lock (gate)
            {
                return files.Contains(path);
            }
        }

        public void DeleteFile(string path)
        {
            lock (gate)
            {
                files.Remove(path);
                DeletedFiles.Add(path);
            }
        }

        internal async Task WaitForOutputsAsync(int count)
        {
            var timeoutAt = DateTime.UtcNow + Timeout;
            while (true)
            {
                lock (gate)
                {
                    if (Outputs.Count >= count && Outputs.Take(count).All(output => output.PlayCount > 0))
                        return;
                }

                if (DateTime.UtcNow >= timeoutAt)
                    throw new TimeoutException($"Expected {count} started music outputs.");
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class FakeMusicOutput(bool throwOnStop, bool throwOnDispose) : IMusicOutput
    {
        private readonly object gate = new();
        private PlaybackState state;

        internal int PlayCount { get; private set; }
        internal int StopCount { get; private set; }
        internal int DisposeCount { get; private set; }

        public PlaybackState PlaybackState
        {
            get
            {
                lock (gate)
                {
                    return state;
                }
            }
        }

        public void Init(ISampleProvider sampleProvider) =>
            ArgumentNullException.ThrowIfNull(sampleProvider);

        public void Play()
        {
            lock (gate)
            {
                PlayCount++;
                state = PlaybackState.Playing;
            }
        }

        public void Pause()
        {
            lock (gate)
            {
                state = PlaybackState.Paused;
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                StopCount++;
                state = PlaybackState.Stopped;
            }
            if (throwOnStop)
                throw new InvalidOperationException("stop failed");
        }

        public void Dispose()
        {
            lock (gate)
            {
                DisposeCount++;
                state = PlaybackState.Stopped;
            }
            if (throwOnDispose)
                throw new InvalidOperationException("dispose failed");
        }
    }

    private sealed class FakeMusicFileSource : IMusicFileSource
    {
        public ISampleProvider SampleProvider { get; } = new SignalGenerator(44100, 1);
        public TimeSpan CurrentTime { get; set; }
        public TimeSpan TotalTime => TimeSpan.FromSeconds(1);
        public long Position { get; set; }
        public float Volume { get; set; }
        public void Dispose() { }
    }
}
