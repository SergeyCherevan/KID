using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Compilation;
using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Runtime;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfMouse = System.Windows.Input.Mouse;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class KeyboardMouseTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task EventWorker_ShutdownWaitsForRunningHandler_DropsQueueAndIsIdempotent()
    {
        using var environmentLease = ExecutionEnvironmentManager.BeginExecution(
            1,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var worker = new ExecutionEventWorker(environment);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var queuedCalls = 0;

        Assert.True(worker.TryEnqueue(() =>
        {
            entered.TrySetResult();
            Assert.True(release.Wait(Timeout));
        }));
        Assert.True(worker.TryEnqueue(() => Interlocked.Increment(ref queuedCalls)));
        await entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var firstShutdown = worker.ShutdownAsync().AsTask();
        var secondShutdown = worker.ShutdownAsync().AsTask();
        Assert.Same(firstShutdown, secondShutdown);
        Assert.False(firstShutdown.IsCompleted);
        Assert.False(worker.TryEnqueue(() => { }));

        release.Set();
        await firstShutdown.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, queuedCalls);
        Assert.Equal(0, worker.QueuedCount);
        Assert.True(worker.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task EventWorker_SessionStopCancelsWaitAndPulse_HandlerFaultDoesNotKillDelivery()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, stop.Token);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var worker = new ExecutionEventWorker(environment);
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalePulse = 0;

        Assert.True(worker.TryEnqueue(() => throw new InvalidOperationException("user handler")));
        Assert.True(worker.TryEnqueue(() => delivered.TrySetResult()));
        Assert.True(worker.TrySchedule(TimeSpan.FromSeconds(30), () => stalePulse++));
        await delivered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        await stop.CancelAsync();
        await worker.Completion.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await worker.ShutdownAsync().AsTask().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, stalePulse);
        Assert.False(worker.TryEnqueue(() => { }));
    }

    [Fact]
    public async Task Keyboard_ShutdownUnsubscribesClearsDelegatesStateShortcutsAndPolicy()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(
                1,
                TestContext.Current.CancellationToken);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var window = new Window();
            var scope = Keyboard.Init(window, environment);
            var delivered = new TaskCompletionSource<KeyPressInfo>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var survivingHandlerCalls = 0;

            Keyboard.KeyDownEvent += _ => throw new InvalidOperationException("first subscriber");
            Keyboard.KeyDownEvent += info =>
            {
                Interlocked.Increment(ref survivingHandlerCalls);
                delivered.TrySetResult(info);
            };
            Keyboard.TextInputEvent += _ => { };
            Keyboard.ShortcutEvent += _ => { };
            Keyboard.CapturePolicy = KeyboardCapturePolicy.IgnoreWhenTextInputFocused;
            Assert.Equal(1, Keyboard.RegisterShortcut(
                new Shortcut(new KeyChord(KeyModifiers.None, Key.A))));

            SendKey(window, Key.A, WpfKeyboard.PreviewKeyDownEvent);
            SendText(window, "я");
            Assert.Equal(Key.A, (await delivered.Task.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken)).Key);
            Assert.Equal(Key.A, Keyboard.CurrentState.LastKeyDown);
            Assert.Equal("я", Keyboard.ReadText());

            var firstShutdown = Keyboard.ShutdownAsync(environment).AsTask();
            var secondShutdown = Keyboard.ShutdownAsync(environment).AsTask();
            Assert.Same(firstShutdown, secondShutdown);
            await firstShutdown.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Null(Keyboard.CurrentScope);
            Assert.True(scope.EventWorker.Completion.IsCompletedSuccessfully);
            Assert.Equal(Key.None, Keyboard.CurrentState.LastKeyDown);
            Assert.Equal(Key.None, Keyboard.CurrentKeyPress.Key);
            Assert.Equal(string.Empty, Keyboard.CurrentTextInput.Text);
            Assert.Equal(string.Empty, Keyboard.ReadText());
            Assert.Equal(KeyboardCapturePolicy.CaptureAlways, Keyboard.CapturePolicy);
            Assert.False(Keyboard.UnregisterShortcut(1));

            SendKey(window, Key.B, WpfKeyboard.PreviewKeyDownEvent);
            await Task.Delay(30, TestContext.Current.CancellationToken);
            Assert.Equal(1, Volatile.Read(ref survivingHandlerCalls));
            Assert.Equal(Key.None, Keyboard.CurrentState.LastKeyDown);
        });
    }

    [Fact]
    public async Task Mouse_ShutdownUnsubscribesClearsDelegatesAndState()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(
                1,
                TestContext.Current.CancellationToken);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var canvas = new Canvas { Width = 200, Height = 100 };
            var scope = Mouse.Init(canvas, environment);
            var delivered = new TaskCompletionSource<MouseClickInfo>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;

            Mouse.MouseClickEvent += _ => throw new InvalidOperationException("first subscriber");
            Mouse.MouseClickEvent += info =>
            {
                Interlocked.Increment(ref calls);
                delivered.TrySetResult(info);
            };
            Mouse.MouseMoveEvent += _ => { };
            Mouse.MousePressButtonEvent += _ => { };

            SendMouseButton(canvas, MouseButton.Left, UIElement.MouseDownEvent);
            Assert.Equal(ClickStatus.OneLeftClick, (await delivered.Task.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken)).Status);
            Assert.Equal(ClickStatus.OneLeftClick, Mouse.LastClick.Status);

            await Mouse.ShutdownAsync(environment).AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Null(Mouse.CurrentScope);
            Assert.True(scope.EventWorker.Completion.IsCompletedSuccessfully);
            Assert.Equal(ClickStatus.NoClick, Mouse.CurrentClick.Status);
            Assert.Equal(ClickStatus.NoClick, Mouse.LastClick.Status);
            Assert.Null(Mouse.CurrentCursor.Position);
            Assert.Equal(PressButtonStatus.OutOfArea, Mouse.CurrentCursor.PressedButton);

            SendMouseButton(canvas, MouseButton.Left, UIElement.MouseDownEvent);
            await Task.Delay(30, TestContext.Current.CancellationToken);
            Assert.Equal(1, Volatile.Read(ref calls));
            Assert.Equal(ClickStatus.NoClick, Mouse.LastClick.Status);
        });
    }

    [Fact]
    public async Task Cleanup_PreventsPreviousRunInputResourcesFromAffectingNextRun()
    {
        await StaTest.RunAsync(async () =>
        {
            var oldWindows = new List<Window>();
            var totalCalls = 0;

            for (var executionId = 1; executionId <= 20; executionId++)
            {
                using var environmentLease = ExecutionEnvironmentManager.BeginExecution(
                    executionId,
                    TestContext.Current.CancellationToken);
                var environment = ExecutionEnvironmentManager.GetCurrent(executionId);
                var window = new Window();
                var canvas = new Canvas();
                var keyboardScope = Keyboard.Init(window, environment);
                var mouseScope = Mouse.Init(canvas, environment);
                var delivered = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                Keyboard.KeyDownEvent += _ =>
                {
                    Interlocked.Increment(ref totalCalls);
                    delivered.TrySetResult();
                };
                Assert.Equal(1, Keyboard.RegisterShortcut(
                    new Shortcut(new KeyChord(KeyModifiers.None, Key.A))));

                SendKey(window, Key.A, WpfKeyboard.PreviewKeyDownEvent);
                await delivered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                await Task.WhenAll(
                    Keyboard.ShutdownAsync(environment).AsTask(),
                    Mouse.ShutdownAsync(environment).AsTask()).WaitAsync(
                        Timeout,
                        TestContext.Current.CancellationToken);

                Assert.True(keyboardScope.EventWorker.Completion.IsCompletedSuccessfully);
                Assert.True(mouseScope.EventWorker.Completion.IsCompletedSuccessfully);
                Assert.Equal(0, keyboardScope.EventWorker.QueuedCount);
                Assert.Equal(0, mouseScope.EventWorker.QueuedCount);
                oldWindows.Add(window);
            }

            foreach (var oldWindow in oldWindows)
                SendKey(oldWindow, Key.B, WpfKeyboard.PreviewKeyDownEvent);

            await Task.Delay(30, TestContext.Current.CancellationToken);
            Assert.Equal(20, Volatile.Read(ref totalCalls));
            Assert.Null(Keyboard.CurrentScope);
            Assert.Null(Mouse.CurrentScope);
        });
    }

    [Fact]
    public async Task CanvasContext_DisposeWaitsForInputHandlerAndReleasesInputBeforeGraphics()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(
                1,
                TestContext.Current.CancellationToken);
            var canvas = new Canvas();
            var window = new Window { Content = canvas };
            var context = new CanvasGraphicsContext(canvas);
            context.Init(1, canvas.Dispatcher);
            var mouseScope = Assert.IsType<MouseExecutionScope>(Mouse.CurrentScope);
            var keyboardScope = Assert.IsType<KeyboardExecutionScope>(Keyboard.CurrentScope);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();

            Assert.True(mouseScope.EventWorker.TryEnqueue(() =>
            {
                entered.TrySetResult();
                Assert.True(release.Wait(Timeout));
            }));
            await entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            var cleanup = context.DisposeAsync().AsTask();
            Assert.False(cleanup.IsCompleted);
            Assert.False(mouseScope.EventWorker.IsAccepting);
            Assert.False(keyboardScope.EventWorker.IsAccepting);
            Assert.NotNull(Graphics.Canvas);

            release.Set();
            await cleanup.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Null(Mouse.CurrentScope);
            Assert.Null(Keyboard.CurrentScope);
            Assert.Null(Graphics.Canvas);
            Assert.Null(context.GraphicsTarget);
        });
    }

    [Fact]
    public async Task CanvasContext_PartialRuntimeInitStillCleansMouse()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(
                1,
                TestContext.Current.CancellationToken);
            var canvas = new Canvas();
            var failure = new InvalidOperationException("after Mouse.Init");
            var context = new CanvasGraphicsContext(canvas, currentCanvas =>
            {
                Mouse.Init(currentCanvas);
                throw failure;
            });

            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                context.Init(1, canvas.Dispatcher)));
            var partialScope = Assert.IsType<MouseExecutionScope>(Mouse.CurrentScope);

            await context.DisposeAsync().AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Null(Mouse.CurrentScope);
            Assert.True(partialScope.EventWorker.Completion.IsCompletedSuccessfully);
            Assert.Null(Graphics.Canvas);
        });
    }

    [Fact]
    public async Task CompiledInputDelegates_DoNotRootCollectibleAssembly()
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
                        KID.Keyboard.KeyDownEvent += OnKey;
                        KID.Keyboard.KeyUpEvent += OnKey;
                        KID.Keyboard.TextInputEvent += OnText;
                        KID.Keyboard.ShortcutEvent += OnShortcut;
                        KID.Mouse.MouseMoveEvent += OnCursor;
                        KID.Mouse.MousePressButtonEvent += OnCursor;
                        KID.Mouse.MouseClickEvent += OnClick;
                    }

                    private static void OnKey(KID.KeyPressInfo value) { }
                    private static void OnText(KID.TextInputInfo value) { }
                    private static void OnShortcut(KID.ShortcutFiredInfo value) { }
                    private static void OnCursor(KID.CursorInfo value) { }
                    private static void OnClick(KID.MouseClickInfo value) { }
                }
                """,
                TestContext.Current.CancellationToken);
            var artifact = Assert.IsType<CompilationArtifact>(compilation.Artifact);
            var references = new List<WeakReference>();

            for (var executionId = 1; executionId <= 8; executionId++)
                references.Add(await RunInputSubscriptionAsync(
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
                await DispatcherPumpAsync();
            }

            Assert.All(references, reference => Assert.False(reference.IsAlive));
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> RunInputSubscriptionAsync(
        long executionId,
        CompilationArtifact artifact,
        StubLocalizationService localization)
    {
        using var environmentLease = ExecutionEnvironmentManager.BeginExecution(
            executionId,
            TestContext.Current.CancellationToken);
        var environment = ExecutionEnvironmentManager.GetCurrent(executionId);
        var window = new Window();
        var canvas = new Canvas();
        Keyboard.Init(window, environment);
        Mouse.Init(canvas, environment);
        var runner = new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory);
        var running = Assert.IsType<CollectibleCodeRunningInstance>(
            runner.Start(artifact, TestContext.Current.CancellationToken));

        try
        {
            await running.Completion.Task.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken);
            await Task.WhenAll(
                Keyboard.ShutdownAsync(environment).AsTask(),
                Mouse.ShutdownAsync(environment).AsTask()).WaitAsync(
                    Timeout,
                    TestContext.Current.CancellationToken);
            return Assert.IsType<WeakReference>(running.LoadContextReference);
        }
        finally
        {
            await Keyboard.ShutdownAsync(environment);
            await Mouse.ShutdownAsync(environment);
            running.Dispose();
        }
    }

    private static KeyEventArgs SendKey(Window window, Key key, RoutedEvent routedEvent)
    {
        var args = new KeyEventArgs(
            InputManager.Current.PrimaryKeyboardDevice,
            new TestPresentationSource(),
            0,
            key)
        { RoutedEvent = routedEvent };
        window.RaiseEvent(args);
        return args;
    }

    private static TextCompositionEventArgs SendText(Window window, string text)
    {
        var args = new TextCompositionEventArgs(
            InputManager.Current.PrimaryKeyboardDevice,
            new TextComposition(InputManager.Current, window, text))
        { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
        window.RaiseEvent(args);
        return args;
    }

    private static MouseButtonEventArgs SendMouseButton(
        Canvas canvas,
        MouseButton button,
        RoutedEvent routedEvent)
    {
        var args = new MouseButtonEventArgs(WpfMouse.PrimaryDevice, 0, button)
        { RoutedEvent = routedEvent };
        canvas.RaiseEvent(args);
        return args;
    }

    private static async Task DispatcherPumpAsync() =>
        await System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => { });

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
