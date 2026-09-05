using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;

namespace KID.Tests.Console;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class TextBoxConsoleSpecifications
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Stop_CompletesReadWithoutInput_AndRestoresUi(bool line, bool beforeRead)
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            var scope = new StackPanel();
            var previousFocus = new TextBox();
            FocusManager.SetIsFocusScope(scope, true);
            scope.Children.Add(previousFocus);
            scope.Children.Add(box);
            FocusManager.SetFocusedElement(scope, previousFocus);
            using var stop = new CancellationTokenSource();
            await using var console = new TextBoxConsole(box, 1, stop.Token);
            if (beforeRead) await stop.CancelAsync();

            var read = Task.Run(() => Record.Exception(() => Read(console, line)), TestContext.Current.CancellationToken);
            if (!beforeRead)
            {
                await WaitUntilAsync(() => !box.IsReadOnly);
                await stop.CancelAsync();
            }
            var error = Assert.IsType<OperationCanceledException>(await read.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            Assert.Equal(stop.Token, error.CancellationToken);
            await PumpAsync(box);
            Assert.True(box.IsReadOnly);
            Assert.Same(previousFocus, FocusManager.GetFocusedElement(scope));
            Assert.False(SendText(box, "late").Handled);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_WakesRead_AndCanBeRepeatedFromUiAndWorker(bool line)
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            using var stop = new CancellationTokenSource();
            await using var console = new TextBoxConsole(box, 1, stop.Token);
            var read = Task.Run(() => Record.Exception(() => Read(console, line)), TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !box.IsReadOnly);
            ((IDisposable)console).Dispose();
            await Task.WhenAll(
                console.DisposeAsync().AsTask(),
                Task.Run(async () => await console.DisposeAsync(), TestContext.Current.CancellationToken)).WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.IsType<ObjectDisposedException>(await read.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            Assert.True(box.IsReadOnly);
            Assert.False(SendText(box, "late").Handled);
            Assert.False(SendKey(box, Key.Enter).Handled);
            Assert.False(SendKey(box, Key.Back).Handled);
            await stop.CancelAsync(); // Регистрация уже снята; callback не обращается к закрытому handle.
            console.Write("late");
            TextBoxConsole.StaticConsole.Clear();
            Assert.Equal("", box.Text);
        });
    }

    [Fact]
    public async Task ReadLine_PartialInput_StopDoesNotRequireEnter()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            using var stop = new CancellationTokenSource();
            await using var console = new TextBoxConsole(box, 1, stop.Token);
            var read = Task.Run(() => Record.Exception(() => console.ReadLine()), TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !box.IsReadOnly);
            Assert.True(SendText(box, "Привет").Handled);
            await WaitUntilAsync(() => box.Text == "Привет");
            await stop.CancelAsync();
            Assert.IsType<OperationCanceledException>(await read.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            await PumpAsync(box);
            Assert.True(box.IsReadOnly);
        });
    }

    [Fact]
    public async Task Input_PreservesUnicodeSpaceBackspaceEnter_AndNextRead()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true, Text = "prompt>" };
            await using var console = new TextBoxConsole(box, 1, CancellationToken.None);
            var line = Task.Run(console.ReadLine, TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !box.IsReadOnly);
            SendKey(box, Key.Back); // Пустой ввод не удаляет prompt.
            SendText(box, "Жя");
            SendKey(box, Key.Back);
            SendKey(box, Key.Space);
            SendText(box, "🙂");
            SendKey(box, Key.Enter);
            Assert.Equal("Ж 🙂", await line.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            await PumpAsync(box);
            Assert.Equal("prompt>Ж 🙂\n", box.Text);
            Assert.True(box.IsReadOnly);

            var character = Task.Run(console.Read, TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !box.IsReadOnly);
            SendText(box, "Ю");
            Assert.Equal('Ю', await character.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            await PumpAsync(box);
            Assert.True(box.IsReadOnly);
        });
    }

    [Fact]
    public async Task RepeatedRead_PreservesAllUtf16CharactersFromOneInputEvent()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            await using var console = new TextBoxConsole(box, 1, CancellationToken.None);
            var read = Task.Run(() => new string(
                [(char)console.Read(), (char)console.Read(), (char)console.Read()]),
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !box.IsReadOnly);
            SendText(box, "A🙂");
            Assert.Equal("A🙂", await read.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            await PumpAsync(box);
            Assert.True(box.IsReadOnly);
        });
    }

    [Fact]
    public async Task Stop_WhileUiQueueIsBlocked_StillReleasesWorker()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            using var stop = new CancellationTokenSource();
            await using var console = new TextBoxConsole(box, 1, stop.Token);
            using var exited = new ManualResetEvent(false);
            var read = Task.Run(() =>
            {
                try { return Record.Exception(() => console.ReadLine()); }
                finally { exited.Set(); }
            }, TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !box.IsReadOnly);
            await box.Dispatcher.InvokeAsync(() =>
            {
                using var blocked = box.Dispatcher.DisableProcessing();
                stop.Cancel();
                // Reader должен выйти, хотя его UI-finally пока невозможно выполнить.
                Assert.True(exited.WaitOne(Timeout));
            });
            Assert.IsType<OperationCanceledException>(await read.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            await PumpAsync(box);
            Assert.True(box.IsReadOnly);
        });
    }

    [Fact]
    public async Task StopInputDispose_Race_ReleasesAllReaders()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            for (var i = 1; i <= 20; i++)
            {
                using var stop = new CancellationTokenSource();
                await using var console = new TextBoxConsole(box, i, stop.Token);
                var first = Task.Run(() => Record.Exception(() => console.ReadLine()), TestContext.Current.CancellationToken);
                await WaitUntilAsync(() => !box.IsReadOnly);
                var second = Task.Run(() => Record.Exception(() => console.ReadLine()), TestContext.Current.CancellationToken);
                SendText(box, "x");
                await stop.CancelAsync();
                ((IDisposable)console).Dispose();
                var results = await Task.WhenAll(first, second).WaitAsync(Timeout, TestContext.Current.CancellationToken);
                Assert.All(results, error => Assert.IsType<OperationCanceledException>(error));
                await console.DisposeAsync();
                Assert.True(box.IsReadOnly);
            }
        });
    }

    [Fact]
    public async Task ConcurrentStopAndDispose_DoNotCloseSignalsUnderReaders()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            for (var id = 1; id <= 20; id++)
            {
                using var stop = new CancellationTokenSource();
                await using var console = new TextBoxConsole(box, id, stop.Token);
                var read = Task.Run(() => Record.Exception(() => console.ReadLine()), TestContext.Current.CancellationToken);
                await WaitUntilAsync(() => !box.IsReadOnly);
                var cancellation = Task.Run(async () => await stop.CancelAsync(), TestContext.Current.CancellationToken);
                var disposal = Task.Run(async () => await console.DisposeAsync(), TestContext.Current.CancellationToken);
                SendText(box, "race");
                await Task.WhenAll(read, cancellation, disposal).WaitAsync(Timeout, TestContext.Current.CancellationToken);
                var error = await read;
                Assert.True(error is OperationCanceledException or ObjectDisposedException);
                if (error is OperationCanceledException canceled) Assert.Equal(stop.Token, canceled.CancellationToken);
                Assert.True(box.IsReadOnly);
            }
        });
    }

    [Fact]
    public async Task QueuedOutputAndLateDispose_CannotChangeNewConsoleOrClearItsBridge()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            await using var old = new TextBoxConsole(box, 1, CancellationToken.None);
            Task? queued = null;
            TextBoxConsole? next = null;
            using var outputQueued = new ManualResetEvent(false);
            await box.Dispatcher.InvokeAsync(() =>
            {
                using var blocked = box.Dispatcher.DisableProcessing();
                queued = Task.Run(() =>
                {
                    old.Write("old");
                    old.Clear();
                    outputQueued.Set();
                }, TestContext.Current.CancellationToken);
                Assert.True(outputQueued.WaitOne(Timeout));
                next = new TextBoxConsole(box, 2, CancellationToken.None);
                box.Text = "new";
                old.Dispose();
            });
            await queued!.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            // Даже уже сохранённый writer старого запуска не должен писать в новый.
            var oldWriter = old.Out;
            await using var current = next!;
            await old.DisposeAsync();
            await oldWriter.WriteAsync("late");
            old.Clear();
            await PumpAsync(box);
            Assert.Equal("new", box.Text);
            TextBoxConsole.StaticConsole.Clear();
            Assert.Equal("", box.Text);
            current.Write("current");
            Assert.Equal("current", box.Text);
        });
    }

    [Fact]
    public async Task Dispose_FlushesAcceptedOutputBeforeCompletion()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            await using var console = new TextBoxConsole(box, 1, CancellationToken.None);
            using var outputQueued = new ManualResetEvent(false);
            Task? writer = null;
            await box.Dispatcher.InvokeAsync(() =>
            {
                using var blocked = box.Dispatcher.DisableProcessing();
                writer = Task.Run(() =>
                {
                    console.Write("first");
                    console.Write("last");
                    outputQueued.Set();
                }, TestContext.Current.CancellationToken);
                Assert.True(outputQueued.WaitOne(Timeout));
                Assert.Equal("", box.Text);
                console.Dispose();
                Assert.Equal("firstlast", box.Text);
            });
            await writer!.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await console.DisposeAsync();
            Assert.Equal("firstlast", box.Text);
            box.Clear();
            await PumpAsync(box);
            Assert.Equal("", box.Text);
        });
    }

    [Fact]
    public async Task FiftyDisposedConsoles_AreCollectibleWhileTheirTextBoxAndTokensRemainAlive()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            using var stop = new CancellationTokenSource();
            var references = Enumerable.Range(1, 50).Select(id => CreateDisposedConsole(box, id, stop.Token)).ToArray();
            for (var i = 0; i < 10 && references.Any(r => r.IsAlive); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.All(references, reference => Assert.False(reference.IsAlive));
            Assert.False(SendText(box, "late").Handled);
            Assert.False(SendKey(box, Key.Enter).Handled);
            await stop.CancelAsync();
            GC.KeepAlive(box);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Context_PartialInit_RestoresAllStreams_AndDisposeIsIdempotent(int redirectedStreams)
    {
        await StaTest.RunAsync(async () =>
        {
            var originalOut = global::System.Console.Out;
            var originalIn = global::System.Console.In;
            var originalError = global::System.Console.Error;
            var box = new TextBox { IsReadOnly = true };
            var context = new TextBoxConsoleContext(box, console =>
            {
                if (redirectedStreams >= 1) global::System.Console.SetOut(console.Out);
                if (redirectedStreams >= 2) global::System.Console.SetIn(console.In);
                if (redirectedStreams >= 3) global::System.Console.SetError(console.Error);
                throw new InvalidOperationException("partial Init");
            });
            try
            {
                Assert.Throws<InvalidOperationException>(() => context.Init(1, CancellationToken.None));
            }
            finally
            {
                await context.DisposeAsync();
            }
            Assert.Same(originalOut, global::System.Console.Out);
            Assert.Same(originalIn, global::System.Console.In);
            Assert.Same(originalError, global::System.Console.Error);
            await using var next = new TextBoxConsoleContext(box);
            next.Init(2, CancellationToken.None);
            var nextOut = global::System.Console.Out;
            await context.DisposeAsync();
            Assert.Same(nextOut, global::System.Console.Out);
            Assert.Throws<ObjectDisposedException>(() => context.Init(3, CancellationToken.None));
        });
    }

    [Fact]
    public async Task Context_DisposeBeforeInit_LeavesStreamsUntouched()
    {
        await StaTest.RunAsync(async () =>
        {
            var original = global::System.Console.Out;
            var context = new TextBoxConsoleContext(new TextBox());
            await context.DisposeAsync();
            await context.DisposeAsync();
            Assert.Same(original, global::System.Console.Out);
            Assert.Throws<ObjectDisposedException>(() => context.Init(1, CancellationToken.None));
        });
    }

    [Theory]
    [InlineData("success")]
    [InlineData("compilation error")]
    [InlineData("runtime error")]
    [InlineData("stop")]
    public async Task Service_RealConsole_RestoresStreamsAndUiForEveryOutcome(string outcome)
    {
        await StaTest.RunAsync(async () =>
        {
            var originalOut = global::System.Console.Out;
            var originalIn = global::System.Console.In;
            var originalError = global::System.Console.Error;
            var box = new TextBox { IsReadOnly = true };
            var compiler = FakeCodeCompiler.Returning(outcome == "compilation error"
                ? CompilationResult.FromErrors(["compile error"])
                : CompilationResult.FromArtifact(new CompilationArtifact(new byte[] { 1 }, new byte[] { 2 })));
            var runner = new FakeCodeRunner((_, _) => Task.Run(() =>
            {
                global::System.Console.Write("user output");
                if (outcome == "runtime error") throw new InvalidOperationException("user error");
                if (outcome == "stop") global::System.Console.ReadLine();
            }, TestContext.Current.CancellationToken));
            var service = new CodeExecutionService(compiler, runner);
            CodeExecutionContext? context = null;
            var execution = service.ExecuteAsync("code", token => context = new CodeExecutionContext
            {
                ConsoleContext = new TextBoxConsoleContext(box),
                GraphicsContext = new TrackingGraphicsContext(),
                Dispatcher = box.Dispatcher,
                CancellationToken = token
            });
            if (outcome == "stop")
            {
                await WaitUntilAsync(() => !box.IsReadOnly);
                Assert.Equal(service.CurrentExecutionId, context!.ExecutionId);
                Assert.True(service.RequestStop());
                Assert.False(service.RequestStop());
            }
            var error = await Record.ExceptionAsync(() => execution.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            if (outcome == "runtime error") Assert.IsType<InvalidOperationException>(error);
            else Assert.Null(error);
            Assert.Equal(ExecutionState.Idle, service.State);
            Assert.Same(originalOut, global::System.Console.Out);
            Assert.Same(originalIn, global::System.Console.In);
            Assert.Same(originalError, global::System.Console.Error);
            Assert.True(box.IsReadOnly);
            Assert.Contains(outcome == "compilation error" ? "compile error" : "user output", box.Text);
            Assert.False(SendText(box, "late").Handled);
        });
    }

    [Fact]
    public async Task Service_AwaitsAsyncConsoleCleanup_BeforeUnloadingAndAllowingRun()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var console = new DelayedConsoleContext(entered, release);
        var runner = new FakeCodeRunner();
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(CompilationResult.FromArtifact(new CompilationArtifact(new byte[] { 1 }, new byte[] { 2 }))), runner);
        var execution = service.ExecuteAsync("code", _ => new TrackingCodeExecutionContext { ConsoleContext = console });
        await entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(ExecutionState.CleaningUp, service.State);
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, runner.DisposeCount);
            Assert.True(StopManager.CurrentToken.CanBeCanceled);
            var secondFactoryCalled = false;
            await service.ExecuteAsync("second", _ =>
            {
                secondFactoryCalled = true;
                return new TrackingCodeExecutionContext();
            });
            Assert.False(secondFactoryCalled);
            Assert.Equal(1, runner.StartCount);
        }
        finally
        {
            release.TrySetResult();
            await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
        Assert.Equal(1, runner.DisposeCount);
        Assert.Equal(ExecutionState.Idle, service.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompiledProgram_StopDuringConsoleRead_CompletesRealLifecycle(bool line)
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            var localization = new StubLocalizationService();
            var service = new CodeExecutionService(new CSharpCompiler(localization),
                new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory));
            var code = "class Program { static void Main() { System.Console.Write(\"ready\"); System.Console." +
                (line ? "ReadLine" : "Read") + "(); } }";
            var execution = service.ExecuteAsync(code, token => new CodeExecutionContext
            {
                ConsoleContext = new TextBoxConsoleContext(box),
                GraphicsContext = new TrackingGraphicsContext(),
                Dispatcher = box.Dispatcher,
                CancellationToken = token
            });
            try
            {
                await WaitUntilAsync(() => !box.IsReadOnly);
                Assert.True(service.RequestStop());
            }
            finally
            {
                service.RequestStop();
                await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            }
            Assert.Equal(ExecutionState.Idle, service.State);
            Assert.True(box.IsReadOnly);
            Assert.Contains("ready", box.Text);
            Assert.Contains("Notification_ProgramStopped", box.Text);
            Assert.DoesNotContain("Error_RuntimeError", box.Text);
        });
    }

    [Fact]
    public async Task GraphicsCleanupFailure_StillRestoresRealConsole_AndRepeatedDisposeKeepsFailure()
    {
        await StaTest.RunAsync(async () =>
        {
            var original = global::System.Console.Out;
            var graphics = new FailingGraphicsContext();
            var context = new CodeExecutionContext
            {
                ExecutionId = 1,
                ConsoleContext = new TextBoxConsoleContext(new TextBox()),
                GraphicsContext = graphics,
                Dispatcher = Dispatcher.CurrentDispatcher
            };
            context.Init();
            var errors = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
                Record.ExceptionAsync(async () => await context.DisposeAsync()).AsTask()));
            Assert.All(errors, error => Assert.Same(graphics.Failure, error));
            Assert.Equal(1, graphics.DisposeCount);
            Assert.Same(original, global::System.Console.Out);
        });
    }

    [Fact]
    public async Task OutputCallbackFailure_IsReportedByCleanup_AndStreamsAreRestored()
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            var original = global::System.Console.Out;
            TextBoxConsole? console = null;
            var failure = new InvalidOperationException("output callback");
            var context = new TextBoxConsoleContext(box, adapter =>
            {
                console = adapter;
                global::System.Console.SetOut(adapter.Out);
                adapter.OutputReceived += (_, _) => throw failure;
            });
            context.Init(1, CancellationToken.None);
            console!.Write("text");
            var error = await Record.ExceptionAsync(async () => await context.DisposeAsync());
            Assert.Same(failure, error);
            Assert.Same(original, global::System.Console.Out);
            Assert.False(SendText(box, "late").Handled);
        });
    }

    private sealed class FailingGraphicsContext : IGraphicsContext
    {
        public object GraphicsTarget { get; set; } = new();
        public Exception Failure { get; } = new InvalidOperationException("graphics cleanup");
        public int DisposeCount { get; private set; }
        public void Init() { }
        public void Dispose()
        {
            DisposeCount++;
            throw Failure;
        }
    }

    private sealed class DelayedConsoleContext(TaskCompletionSource entered, TaskCompletionSource release) : IConsoleContext
    {
        public object ConsoleTarget { get; set; } = new();
        public void Init(long executionId, CancellationToken cancellationToken) { }
        public async ValueTask DisposeAsync()
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedConsole(TextBox box, long id, CancellationToken token)
    {
        var console = new TextBoxConsole(box, id, token);
        console.OutputReceived += (_, _) => { };
        var reference = new WeakReference(console);
        ((IDisposable)console).Dispose();
        return reference;
    }

    private static void Read(TextBoxConsole console, bool line)
    {
        if (line) console.ReadLine();
        else console.Read();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for console input state.");
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
    }

    private static async Task PumpAsync(TextBox box) =>
        await box.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private static TextCompositionEventArgs SendText(TextBox box, string text)
    {
        var args = new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice,
            new TextComposition(InputManager.Current, box, text))
        { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
        box.RaiseEvent(args);
        return args;
    }

    private static KeyEventArgs SendKey(TextBox box, Key key)
    {
        var args = new KeyEventArgs(InputManager.Current.PrimaryKeyboardDevice, new TestPresentationSource(), 0, key)
        { RoutedEvent = global::System.Windows.Input.Keyboard.PreviewKeyDownEvent };
        box.RaiseEvent(args);
        return args;
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
