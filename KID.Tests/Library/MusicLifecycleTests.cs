using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
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
    public async Task BlockingMelody_PreservesSequentialToneAndPauseTiming()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(
            1,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime { DrainProviderDuringInit = true };
        Music.Init(environment, runtime);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            Music.Sound(
                new SoundNote(262, 150),
                new SoundNote(0, 30),
                new SoundNote(330, 150),
                new SoundNote(0, 30),
                new SoundNote(392, 150),
                new SoundNote(0, 30),
                new SoundNote(330, 250));
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(80));
            Assert.Equal(
                [6_615, 6_615, 6_615, 11_025],
                runtime.Outputs.Select(static output => output.CapturedSamples.Count));
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
            var compilation = await CompilerFactory.Create(localization).CompileAsync(
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

    [Theory]
    [InlineData("Sound", null)]
    [InlineData("Sound", 3L)]
    [InlineData("SoundPlay", null)]
    [InlineData("SoundPlay", 3L)]
    [InlineData("SoundLoad", null)]
    [InlineData("SoundLoad", 3L)]
    public async Task UrlPlayback_PreservesLimitAndClosesSourceBeforeDeletingFile(string method, long? limit)
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime { CompleteImmediately = true };
        var scope = Music.Init(environment, runtime);
        const string url = "https://example.test/audio.mp3";
        try
        {
            if (method == "Sound")
            {
                if (limit.HasValue)
                    Music.Sound(url, limit);
                else
                    Music.Sound(url);
            }
            else
            {
                using var player = method == "SoundLoad"
                    ? (limit.HasValue ? Music.SoundLoad(url, limit) : Music.SoundLoad(url))
                    : (limit.HasValue ? Music.SoundPlay(url, limit) : Music.SoundPlay(url));
                if (method == "SoundLoad")
                {
                    Assert.Empty(runtime.DownloadLimits);
                    player.SoundPlay();
                }
                await player.PlaySoundAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);
            }

            Assert.Equal(limit, Assert.Single(runtime.DownloadLimits));
            Assert.Equal(["source-disposed", "deleted"], runtime.FileEvents);
            Assert.Equal(1, Assert.Single(runtime.Sources).DisposeCount);
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.Equal(0, scope.TemporaryFileCount);
            await Music.ShutdownAsync(environment);
            Assert.Equal(0, scope.BackgroundTaskCount);
        }
        finally { await IgnoreCleanupFailureAsync(environment); }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void FileOverloads_RejectInvalidLimitSynchronouslyEvenWithoutActiveSession(long limit)
    {
        Assert.Equal("maxDownloadBytes", Assert.Throws<ArgumentOutOfRangeException>(
            () => Music.Sound("", limit)).ParamName);
        Assert.Equal("maxDownloadBytes", Assert.Throws<ArgumentOutOfRangeException>(
            () => Music.SoundPlay("", limit)).ParamName);
        Assert.Equal("maxDownloadBytes", Assert.Throws<ArgumentOutOfRangeException>(
            () => Music.SoundLoad("", limit)).ParamName);
    }

    [Fact]
    public async Task LocalFile_IgnoresDownloadLimitAndDoesNotCreateTemporaryFile()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime { CompleteImmediately = true };
        runtime.AddLocalFile("local.wav");
        Music.Init(environment, runtime);
        try
        {
            Music.Sound("local.wav", maxDownloadBytes: 1);
            Assert.Empty(runtime.DownloadLimits);
            Assert.Empty(runtime.DeletedFiles);
            Assert.Equal(1, Assert.Single(runtime.Sources).DisposeCount);
        }
        finally { await IgnoreCleanupFailureAsync(environment); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UrlFailure_IsObservedAndDeletesFileWithoutStartingOutput(bool failOpening)
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var failure = new InvalidDataException("test failure");
        var runtime = new FakeMusicRuntime
        {
            DownloadFailure = failOpening ? null : failure,
            OpenFileFailure = failOpening ? failure : null
        };
        var scope = Music.Init(environment, runtime);
        try
        {
            using var player = Music.SoundPlay("https://example.test/audio.mp3", 3);
            var observed = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await player.PlaySoundAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken));
            Assert.Same(failure, observed);
            Assert.Empty(runtime.Outputs);
            Assert.Single(runtime.DeletedFiles);
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.Equal(0, scope.TemporaryFileCount);
        }
        finally { await IgnoreCleanupFailureAsync(environment); }
    }

    [Fact]
    public async Task SoundStop_CancelsDownloadAndRepeatedStopLeavesSessionUsable()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var runtime = new FakeMusicRuntime { BlockWrite = true };
        var scope = Music.Init(environment, runtime);
        try
        {
            using var player = Music.SoundPlay("https://example.test/audio.mp3", 3);
            await runtime.WriteStarted.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await Task.Run(() => player.SoundStop(), TestContext.Current.CancellationToken)
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);
            player.SoundStop();
            Assert.Single(runtime.DeletedFiles);
            Assert.Empty(runtime.Outputs);
            Assert.Equal(0, scope.TemporaryFileCount);
            Assert.Equal(0, scope.ActiveSoundCount);
            Assert.True(scope.IsAccepting);
        }
        finally { await IgnoreCleanupFailureAsync(environment); }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("header-limit")]
    [InlineData("body-limit")]
    [InlineData("read-failure")]
    [InlineData("http-failure")]
    public async Task UrlPlayback_DeletesRealTemporaryFileAndClosesExclusiveSource(string scenario)
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        using var runtime = new DiskMusicRuntime(scenario);
        var scope = Music.Init(environment, runtime);
        try
        {
            using var player = Music.SoundPlay("https://example.test/audio.wav",
                scenario is "header-limit" or "body-limit" ? 3 : null);
            var failure = await Record.ExceptionAsync(async () =>
                await player.PlaySoundAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken));
            if (scenario == "success")
            {
                Assert.Null(failure);
                Assert.Equal(["source-disposed", "deleted"], runtime.Events);
            }
            else
            {
                Assert.NotNull(failure);
                Assert.Equal(["deleted"], runtime.Events);
            }
            Assert.False(File.Exists(Assert.Single(runtime.Paths)));
            Assert.Equal(0, scope.TemporaryFileCount);
            Assert.Equal(0, scope.ActiveSoundCount);
        }
        finally { await IgnoreCleanupFailureAsync(environment); }
    }

    [Fact]
    public async Task SoundStop_DeletesRealPartialFileAndClosesWriteHandle()
    {
        using var lease = ExecutionEnvironmentManager.BeginExecution(1, TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        using var runtime = new DiskMusicRuntime("blocked");
        Music.Init(environment, runtime);
        try
        {
            using var player = Music.SoundPlay("https://example.test/audio.wav");
            await runtime.Input.Blocked.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(Assert.Single(runtime.Paths)));
            await Task.Run(() => player.SoundStop(), TestContext.Current.CancellationToken)
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);
            player.SoundStop();
            Assert.False(File.Exists(runtime.Paths[0]));
            Assert.Equal(["deleted"], runtime.Events);
            await Music.ShutdownAsync(environment);
        }
        finally { await IgnoreCleanupFailureAsync(environment); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaughtDownloadFailure_AllowsNextHostRunAndCleansFiles(bool httpFailure)
    {
        await StaTest.RunAsync(async () =>
        {
            const string code = """
                public static class Program
                {
                    public static void Main()
                    {
                        try { KID.Music.Sound("https://example.test/audio.wav", 3); }
                        catch (System.Exception) { }
                    }
                }
                """;
            var localization = new StubLocalizationService();
            var compilation = await CompilerFactory.Create(localization)
                .CompileAsync(code, TestContext.Current.CancellationToken);
            Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Errors));
            var runtime = new FakeMusicRuntime
            {
                DownloadFailure = httpFailure
                    ? new HttpRequestException("HTTP 404")
                    : new InvalidDataException("Audio download exceeds the size limit.")
            };
            var service = new CodeExecutionService(
                FakeCodeCompiler.Returning(compilation),
                new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory));
            var canvas = new Canvas();
            var execution = service.ExecuteAsync(code, _ => new TrackingCodeExecutionContext
            {
                GraphicsContext = new CanvasGraphicsContext(canvas, (_, environment) => Music.Init(environment, runtime))
            });
            var result = await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal(ExecutionResultKind.Completed, result.Kind);
            Assert.Single(runtime.DownloadLimits);
            Assert.Equal(ExecutionState.Idle, service.State);
            Assert.False(service.IsExecutionActive);
            Assert.Null(Music.CurrentScope);
            Assert.Single(runtime.DeletedFiles);

            var nextRuntime = new FakeMusicRuntime { CompleteImmediately = true };
            var next = await service.ExecuteAsync(code, _ => new TrackingCodeExecutionContext
            {
                GraphicsContext = new CanvasGraphicsContext(canvas, (_, environment) => Music.Init(environment, nextRuntime))
            }).WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal(ExecutionResultKind.Completed, next.Kind);
            Assert.Single(nextRuntime.DownloadLimits);
            Assert.Equal(ExecutionState.Idle, service.State);
            Assert.Null(Music.CurrentScope);
            Assert.Single(nextRuntime.DeletedFiles);
        });
    }

    private sealed class DiskMusicRuntime : IMusicRuntime, IDisposable
    {
        private readonly HttpClient client;
        private readonly DefaultMusicRuntime runtime;
        internal DiskReadStream Input { get; }
        internal List<string> Paths { get; } = [];
        internal List<string> Events { get; } = [];

        internal DiskMusicRuntime(string scenario)
        {
            Input = new DiskReadStream(scenario);
            client = new HttpClient(new DiskResponseHandler(Input, scenario));
            runtime = new DefaultMusicRuntime(client);
        }
        public IMusicOutput CreateOutput() => new FakeMusicOutput(false, false, false, true);
        public IMusicFileSource OpenFile(string path) => new LockedDiskSource(path, Events);
        public string CreateTemporaryPath()
        {
            var path = runtime.CreateTemporaryPath();
            Paths.Add(path);
            return path;
        }
        public Task DownloadToFileAsync(string url, string path, long? maxBytes, CancellationToken token) =>
            runtime.DownloadToFileAsync(url, path, maxBytes, token);
        public bool FileExists(string path) => File.Exists(path);
        public void DeleteFile(string path)
        {
            File.Delete(path);
            Events.Add("deleted");
        }
        public void Dispose()
        {
            client.Dispose();
            foreach (var path in Paths)
                File.Delete(path);
        }
    }

    private sealed class LockedDiskSource : IMusicFileSource
    {
        private readonly FileStream file;
        private readonly List<string> events;
        internal LockedDiskSource(string path, List<string> events)
        {
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            this.events = events;
        }
        public ISampleProvider SampleProvider { get; } = new SignalGenerator(44100, 1);
        public TimeSpan CurrentTime { get; set; }
        public TimeSpan TotalTime => TimeSpan.FromSeconds(1);
        public long Position { get; set; }
        public float Volume { get; set; }
        public void Dispose()
        {
            file.Dispose();
            events.Add("source-disposed");
        }
    }

    private sealed class DiskResponseHandler(Stream input, string scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var content = new StreamContent(input);
            if (scenario == "header-limit")
                content.Headers.ContentLength = 4;
            return Task.FromResult(new HttpResponseMessage(
                scenario == "http-failure" ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class DiskReadStream(string scenario) : MemoryStream([1, 2, 3, 4])
    {
        internal TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanSeek => false;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (base.Position >= 2 && scenario == "blocked")
            {
                Blocked.TrySetResult();
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);
            }
            if (base.Position >= 2 && scenario == "read-failure")
                throw new IOException("Read failed midway.");
            return await base.ReadAsync(buffer[..Math.Min(buffer.Length, 2)], token);
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
        internal List<long?> DownloadLimits { get; } = [];
        internal List<string> FileEvents { get; } = [];
        internal List<FakeMusicFileSource> Sources { get; } = [];
        internal Exception? DownloadFailure { get; init; }
        internal Exception? OpenFileFailure { get; init; }
        internal bool CompleteImmediately { get; init; }
        internal TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BlockWrite { get; init; }
        internal bool ThrowOnStop { get; init; }
        internal bool ThrowOnDispose { get; init; }
        internal bool DrainProviderDuringInit { get; init; }

        public IMusicOutput CreateOutput()
        {
            var output = new FakeMusicOutput(
                ThrowOnStop,
                ThrowOnDispose,
                DrainProviderDuringInit,
                CompleteImmediately);
            lock (gate)
            {
                Outputs.Add(output);
            }
            return output;
        }

        public IMusicFileSource OpenFile(string path)
        {
            if (OpenFileFailure != null)
                throw OpenFileFailure;
            var source = new FakeMusicFileSource(() => FileEvents.Add("source-disposed"));
            Sources.Add(source);
            return source;
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

        public async Task DownloadToFileAsync(
            string url,
            string path,
            long? maxBytes,
            CancellationToken cancellationToken)
        {
            DownloadLimits.Add(maxBytes);
            WriteStarted.TrySetResult();
            if (BlockWrite)
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (DownloadFailure != null)
                throw DownloadFailure;
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
                FileEvents.Add("deleted");
            }
        }

        internal void AddLocalFile(string path) => files.Add(path);

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

    private sealed class FakeMusicOutput(
        bool throwOnStop,
        bool throwOnDispose,
        bool drainProviderDuringInit,
        bool completeImmediately) : IMusicOutput
    {
        private readonly object gate = new();
        private PlaybackState state;

        internal int PlayCount { get; private set; }
        internal int StopCount { get; private set; }
        internal int DisposeCount { get; private set; }
        internal List<float> CapturedSamples { get; } = [];

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

        public void Init(ISampleProvider sampleProvider)
        {
            ArgumentNullException.ThrowIfNull(sampleProvider);
            if (!drainProviderDuringInit)
                return;

            var buffer = new float[257];
            int read;
            while ((read = sampleProvider.Read(buffer.AsSpan())) > 0)
                CapturedSamples.AddRange(buffer.Take(read));
        }

        public void Play()
        {
            lock (gate)
            {
                PlayCount++;
                state = drainProviderDuringInit || completeImmediately
                    ? PlaybackState.Stopped
                    : PlaybackState.Playing;
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

    private sealed class FakeMusicFileSource(Action onDispose) : IMusicFileSource
    {
        internal int DisposeCount { get; private set; }
        public ISampleProvider SampleProvider { get; } = new SignalGenerator(44100, 1);
        public TimeSpan CurrentTime { get; set; }
        public TimeSpan TotalTime => TimeSpan.FromSeconds(1);
        public long Position { get; set; }
        public float Volume { get; set; }
        public void Dispose()
        {
            DisposeCount++;
            onDispose();
        }
    }
}
