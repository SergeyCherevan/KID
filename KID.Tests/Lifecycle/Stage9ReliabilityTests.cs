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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfMouse = System.Windows.Input.Mouse;

namespace KID.Tests.Lifecycle;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class Stage9ReliabilityTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("Task.Delay")]
    [InlineData("Thread.Sleep")]
    public async Task CompiledProgram_StopInterruptsSupportedBclWait(string waitKind)
    {
        var startedKey = $"KID.Tests.Stage9.Wait.{Guid.NewGuid():N}";
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var waitStatement = waitKind switch
        {
            "Task.Delay" =>
                "await System.Threading.Tasks.Task.Delay(System.Threading.Timeout.InfiniteTimeSpan);",
            "Thread.Sleep" =>
                "System.Threading.Thread.Sleep(System.Threading.Timeout.InfiniteTimeSpan);",
            _ => throw new ArgumentOutOfRangeException(nameof(waitKind))
        };
        var code = $$"""
            public static class Program
            {
                public static async System.Threading.Tasks.Task Main()
                {
                    var started = (System.Threading.Tasks.TaskCompletionSource<bool>)
                        System.AppContext.GetData("{{startedKey}}")!;
                    started.TrySetResult(true);
                    {{waitStatement}}
                }
            }
            """;
        var localization = new StubLocalizationService();
        var compilation = await CompilerFactory.Create(localization).CompileAsync(
            code,
            TestContext.Current.CancellationToken);
        Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Errors));
        var artifact = Assert.IsType<CompilationArtifact>(compilation.Artifact);
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(CompilationResult.FromArtifact(artifact)),
            new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory));

        AppContext.SetData(startedKey, started);
        var execution = service.ExecuteAsync(
            code,
            _ => new TrackingCodeExecutionContext());

        try
        {
            await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal(ExecutionState.Running, service.State);
            Assert.True(service.RequestStop());

            var result = await execution.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken);

            Assert.Equal(ExecutionResultKind.Stopped, result.Kind);
            Assert.Equal(ExecutionState.Idle, service.State);
            Assert.False(StopManager.CurrentToken.CanBeCanceled);
        }
        finally
        {
            if (service.IsExecutionActive)
                service.RequestStop();
            if (!execution.IsCompleted)
                await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            AppContext.SetData(startedKey, null);
        }
    }

    [Fact]
    public async Task FiftySequentialRunStop_ClearsHandlersWorkersShortcutsAndSoundPlayers()
    {
        await StaTest.RunAsync(async () =>
        {
            var startedKey = $"KID.Tests.Stage9.Soak.Started.{Guid.NewGuid():N}";
            var callbackKey = $"KID.Tests.Stage9.Soak.Callback.{Guid.NewGuid():N}";
            var code = CreateSoakProgram(startedKey, callbackKey);
            var localization = new StubLocalizationService();
            var compilation = await CompilerFactory.Create(localization).CompileAsync(
                code,
                TestContext.Current.CancellationToken);
            Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Errors));
            var artifact = Assert.IsType<CompilationArtifact>(compilation.Artifact);
            var runner = new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory);
            var service = new CodeExecutionService(
                FakeCodeCompiler.Returning(CompilationResult.FromArtifact(artifact)),
                runner);
            var originalOut = global::System.Console.Out;
            var originalIn = global::System.Console.In;
            var originalError = global::System.Console.Error;
            var callbackCount = 0;
            var oldTargets = new List<(Window Window, Canvas Canvas, TextBox TextBox)>();

            AppContext.SetData(
                callbackKey,
                new Action(() => Interlocked.Increment(ref callbackCount)));
            try
            {
                for (var executionIndex = 1; executionIndex <= 50; executionIndex++)
                {
                    var started = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    AppContext.SetData(startedKey, started);
                    var window = new Window();
                    var canvas = new Canvas();
                    var textBox = new TextBox { IsReadOnly = true };
                    window.Content = canvas;
                    var musicRuntime = new SoakMusicRuntime();

                    var execution = service.ExecuteAsync("compiled-stage-9-soak", token =>
                        new CodeExecutionContext
                        {
                            GraphicsContext = new CanvasGraphicsContext(
                                canvas,
                                (currentCanvas, environment) =>
                                {
                                    Mouse.Init(currentCanvas, environment);
                                    Music.Init(environment, musicRuntime);
                                    var owner = Window.GetWindow(currentCanvas);
                                    Assert.NotNull(owner);
                                    Keyboard.Init(owner, environment);
                                }),
                            ConsoleContext = new TextBoxConsoleContext(textBox),
                            Dispatcher = canvas.Dispatcher,
                            CancellationToken = token
                        });

                    await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                    await musicRuntime.OutputCreated.Task.WaitAsync(
                        Timeout,
                        TestContext.Current.CancellationToken);
                    var keyboardScope = Assert.IsType<KeyboardExecutionScope>(Keyboard.CurrentScope);
                    var mouseScope = Assert.IsType<MouseExecutionScope>(Mouse.CurrentScope);
                    var musicScope = Assert.IsType<MusicExecutionScope>(Music.CurrentScope);
                    Assert.Equal(1, musicScope.ActiveSoundCount);

                    Assert.True(service.RequestStop());
                    Assert.False(service.RequestStop());
                    var result = await execution.WaitAsync(
                        Timeout,
                        TestContext.Current.CancellationToken);

                    Assert.Equal(ExecutionResultKind.Stopped, result.Kind);
                    Assert.Equal(ExecutionState.Idle, service.State);
                    Assert.True(keyboardScope.EventWorker.Completion.IsCompletedSuccessfully);
                    Assert.True(mouseScope.EventWorker.Completion.IsCompletedSuccessfully);
                    Assert.Equal(0, keyboardScope.EventWorker.QueuedCount);
                    Assert.Equal(0, mouseScope.EventWorker.QueuedCount);
                    Assert.Equal(0, musicScope.ActiveSoundCount);
                    Assert.Equal(0, musicScope.BackgroundTaskCount);
                    Assert.Equal(0, musicScope.TemporaryFileCount);
                    Assert.All(musicRuntime.Outputs, output =>
                    {
                        Assert.Equal(1, output.DisposeCount);
                        Assert.Equal(PlaybackState.Stopped, output.PlaybackState);
                    });
                    Assert.Null(Keyboard.CurrentScope);
                    Assert.Null(Mouse.CurrentScope);
                    Assert.Null(Music.CurrentScope);
                    Assert.False(Keyboard.UnregisterShortcut(1));
                    Assert.Same(originalOut, global::System.Console.Out);
                    Assert.Same(originalIn, global::System.Console.In);
                    Assert.Same(originalError, global::System.Console.Error);
                    oldTargets.Add((window, canvas, textBox));
                }

                foreach (var target in oldTargets)
                {
                    SendKey(target.Window, Key.A);
                    SendMouseClick(target.Canvas);
                    Assert.False(SendText(target.TextBox, "late").Handled);
                }

                await Task.Delay(30, TestContext.Current.CancellationToken);
                Assert.Equal(0, Volatile.Read(ref callbackCount));
            }
            finally
            {
                AppContext.SetData(startedKey, null);
                AppContext.SetData(callbackKey, null);
            }
        });
    }

    private static string CreateSoakProgram(string startedKey, string callbackKey) => $$"""
        public static class Program
        {
            public static async System.Threading.Tasks.Task Main()
            {
                var callback = (System.Action)System.AppContext.GetData("{{callbackKey}}")!;
                KID.Keyboard.KeyDownEvent += _ => callback();
                KID.Mouse.MouseClickEvent += _ => callback();
                KID.Keyboard.RegisterShortcut(new KID.Shortcut(
                    new KID.KeyChord(KID.KeyModifiers.None, System.Windows.Input.Key.A)));
                KID.Music.SoundPlay(440, 60000);
                var started = (System.Threading.Tasks.TaskCompletionSource<bool>)
                    System.AppContext.GetData("{{startedKey}}")!;
                started.TrySetResult(true);
                await System.Threading.Tasks.Task.Delay(System.Threading.Timeout.InfiniteTimeSpan);
            }
        }
        """;

    private static void SendKey(Window window, Key key)
    {
        var args = new KeyEventArgs(
            WpfKeyboard.PrimaryDevice,
            new TestPresentationSource(),
            0,
            key)
        { RoutedEvent = WpfKeyboard.PreviewKeyDownEvent };
        window.RaiseEvent(args);
    }

    private static void SendMouseClick(Canvas canvas)
    {
        var args = new MouseButtonEventArgs(WpfMouse.PrimaryDevice, 0, MouseButton.Left)
        { RoutedEvent = UIElement.MouseDownEvent };
        canvas.RaiseEvent(args);
    }

    private static TextCompositionEventArgs SendText(TextBox textBox, string text)
    {
        var args = new TextCompositionEventArgs(
            WpfKeyboard.PrimaryDevice,
            new TextComposition(InputManager.Current, textBox, text))
        { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
        textBox.RaiseEvent(args);
        return args;
    }

    private sealed class SoakMusicRuntime : IMusicRuntime
    {
        internal List<SoakMusicOutput> Outputs { get; } = [];
        internal TaskCompletionSource OutputCreated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IMusicOutput CreateOutput()
        {
            var output = new SoakMusicOutput();
            Outputs.Add(output);
            OutputCreated.TrySetResult();
            return output;
        }

        public IMusicFileSource OpenFile(string path) =>
            throw new NotSupportedException("The Stage 9 soak uses generated audio only.");

        public Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The Stage 9 soak does not use the network.");

        public string CreateTemporaryPath() =>
            throw new NotSupportedException("The Stage 9 soak creates no files.");

        public Task WriteAllBytesAsync(
            string path,
            byte[] content,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The Stage 9 soak creates no files.");

        public bool FileExists(string path) => false;

        public void DeleteFile(string path) { }
    }

    private sealed class SoakMusicOutput : IMusicOutput
    {
        public PlaybackState PlaybackState { get; private set; }
        internal int DisposeCount { get; private set; }

        public void Init(ISampleProvider sampleProvider) =>
            ArgumentNullException.ThrowIfNull(sampleProvider);

        public void Play() => PlaybackState = PlaybackState.Playing;

        public void Pause() => PlaybackState = PlaybackState.Paused;

        public void Stop()
        {
            PlaybackState = PlaybackState.Stopped;
        }

        public void Dispose()
        {
            DisposeCount++;
            PlaybackState = PlaybackState.Stopped;
        }
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
