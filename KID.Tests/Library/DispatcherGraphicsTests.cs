using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Compilation;
using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Runtime;
using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;

namespace KID.Tests.Library;

public sealed class DispatcherGraphicsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Scope_RejectsDoubleInit_AndOldDisposeCannotReleaseNextScope()
    {
        await StaTest.RunAsync(async () =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            Assert.Throws<ArgumentOutOfRangeException>(() => BeginDispatcherExecution(0, dispatcher, TestContext.Current.CancellationToken));
            var old = BeginDispatcherExecution(1, dispatcher, default);
            Assert.Throws<InvalidOperationException>(() => BeginDispatcherExecution(2, dispatcher, TestContext.Current.CancellationToken));
            Assert.Equal(42, DispatcherManager.InvokeOnUI(() => 42));
            await old.DisposeAsync();
            await using var next = BeginDispatcherExecution(2, dispatcher, default);
            await old.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => old.Post(() => 0, false));
            Assert.Equal(43, DispatcherManager.InvokeOnUI(() => 43));
        });
        Assert.Throws<InvalidOperationException>(() => DispatcherManager.InvokeOnUI(() => 1));
    }

    [Fact]
    public async Task StopBeforePosting_AcceptsNoOperation()
    {
        await StaTest.RunAsync(async () =>
        {
            using var stop = new CancellationTokenSource();
            await using var scope = BeginDispatcherExecution(1, Dispatcher.CurrentDispatcher, stop.Token);
            await stop.CancelAsync();
            var error = await Task.Run(() => Record.Exception(() => DispatcherManager.InvokeOnUI(() => 42)), TestContext.Current.CancellationToken);
            Assert.Equal(stop.Token, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
            Assert.Equal(0, scope.PendingCount);
        });
    }

    [Fact]
    public async Task Stop_ReleasesSynchronousWorkerWhileUiIsBlocked_AndAbortsQueuedMutation()
    {
        await StaTest.RunAsync(async () =>
        {
            using var stop = new CancellationTokenSource();
            await using var scope = BeginDispatcherExecution(1, Dispatcher.CurrentDispatcher, stop.Token);
            var canvas = new Canvas();
            var worker = Task.Run(() => Record.Exception(() => DispatcherManager.InvokeOnUI(() =>
            {
                canvas.Children.Add(new Rectangle());
                return 1;
            })), TestContext.Current.CancellationToken);
            // Не качаем UI: ждём подтверждённой регистрации команды через worker-side signal.
            using (Dispatcher.CurrentDispatcher.DisableProcessing())
            {
                Assert.True(SpinWait.SpinUntil(() => scope.PendingCount == 1, Timeout));
                CancelWithBlockedUi(stop);
                Assert.True(SpinWait.SpinUntil(() => worker.IsCompleted, Timeout));
                Assert.Empty(canvas.Children);
            }
            var error = await worker;
            Assert.Equal(stop.Token, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
            await scope.DisposeAsync();
            await PumpAsync();
            Assert.Empty(canvas.Children);
            Assert.Equal(0, scope.PendingCount);
        });
    }

    [Fact]
    public async Task NormalDrain_FinishesAcceptedCommandsAndNestedCalls_RejectsNewWork()
    {
        await StaTest.RunAsync(async () =>
        {
            await using var scope = BeginDispatcherExecution(1, Dispatcher.CurrentDispatcher, default);
            var values = new List<int>();
            Task drain;
            using (Dispatcher.CurrentDispatcher.DisableProcessing())
            {
                var producer = Task.Run(() =>
                {
                    DispatcherManager.InvokeOnUI(() => { values.Add(1); });
                    DispatcherManager.InvokeOnUI(() => { values.Add(DispatcherManager.InvokeOnUI(() => 2)); });
                }, TestContext.Current.CancellationToken);
                Assert.True(SpinWait.SpinUntil(() => producer.IsCompleted, Timeout));
                Assert.True(producer.IsCompletedSuccessfully);
                drain = scope.DisposeAsync().AsTask();
                Assert.False(drain.IsCompleted);
                Assert.Throws<ObjectDisposedException>(() => DispatcherManager.InvokeOnUI(() => 3));
            }
            await drain.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal(new[] { 1, 2 }, values);
            Assert.Equal(0, scope.PendingCount);
        });
    }

    [Fact]
    public async Task QueuedStopAndStaleRelease_CannotClearNextCanvasOrDefaults()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            using var stop = new CancellationTokenSource();
            var old = BeginDispatcherExecution(1, canvas.Dispatcher, stop.Token);
            Graphics.Init(canvas, old);
            var oldSprite = new Sprite();
            using (canvas.Dispatcher.DisableProcessing())
            {
                var producer = Task.Run(() =>
                {
                    Graphics.Clear();
                    Graphics.FillColor = "Red";
                    Graphics.SetFont("Consolas", 80);
                    Graphics.SetCanvasWidth(900);
                }, TestContext.Current.CancellationToken);
                Assert.True(SpinWait.SpinUntil(() => producer.IsCompleted, Timeout));
                Assert.True(producer.IsCompletedSuccessfully);
                CancelWithBlockedUi(stop);
            }
            await old.ShutdownAsync(() => Graphics.Release(old));
            await using var next = BeginDispatcherExecution(2, canvas.Dispatcher, default);
            Graphics.Init(canvas, next);
            try
            {
                var shape = Graphics.Circle(1, 1, 1);
                var text = Graphics.Text(0, 0, "new");
                await old.DisposeAsync();
                Graphics.Release(old);
                await PumpAsync();
                Assert.Same(canvas, Graphics.Canvas);
                Assert.Contains(shape, canvas.Children.Cast<UIElement>());
                Assert.Same(Brushes.Black, shape.Fill);
                Assert.Equal(20, text.FontSize);
                Assert.Equal("Arial", text.FontFamily.Source);
                Assert.True(double.IsNaN(canvas.Width));
                Assert.ThrowsAny<OperationCanceledException>(() => oldSprite.Move(1, 1));
            }
            finally { await next.ShutdownAsync(() => Graphics.Release(next)); }
        });
    }

    [Fact]
    public async Task QueuedFault_IsReportedByCleanup_ButSyncFaultIsReturnedToCaller()
    {
        await StaTest.RunAsync(async () =>
        {
            var scope = BeginDispatcherExecution(1, Dispatcher.CurrentDispatcher, default);
            var failure = new InvalidOperationException("queued");
            await Task.Run(() => DispatcherManager.InvokeOnUI((Action)(() => throw failure)), TestContext.Current.CancellationToken);
            var error = await Record.ExceptionAsync(async () => await scope.DisposeAsync());
            Assert.Same(failure, error);
            Assert.Same(error, await Record.ExceptionAsync(async () => await scope.DisposeAsync()));
            await using var next = BeginDispatcherExecution(2, Dispatcher.CurrentDispatcher, default);
            var syncError = await Task.Run(() => Record.Exception(() => DispatcherManager.InvokeOnUI<int>(() => throw failure)), TestContext.Current.CancellationToken);
            Assert.Same(failure, syncError);
        });
    }

    [Fact]
    public async Task StopDuringCallback_DoesNotCompleteCleanupUntilCallbackExits()
    {
        await StaTest.RunAsync(async () =>
        {
            using var stop = new CancellationTokenSource();
            await using var scope = BeginDispatcherExecution(1, Dispatcher.CurrentDispatcher, stop.Token);
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var waiter = Task.Run(() => Record.Exception(() => DispatcherManager.InvokeOnUI(() =>
            {
                entered.Set();
                Assert.True(release.Wait(Timeout));
                scope.Scope.Environment.ThrowIfCancellationRequested();
                return 1;
            })), TestContext.Current.CancellationToken);
            var controller = Task.Run(async () =>
            {
                try
                {
                    Assert.True(entered.Wait(Timeout));
                    await stop.CancelAsync();
                    var error = await waiter.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                    Assert.IsAssignableFrom<OperationCanceledException>(error);
                    var drain = scope.DisposeAsync().AsTask();
                    Assert.False(drain.IsCompleted);
                    release.Set();
                    await drain.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                }
                finally { release.Set(); }
            }, TestContext.Current.CancellationToken);
            await controller.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Context_PreservesDrawing_ResetsStatics_AndIsIdempotent()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            var context = new CanvasGraphicsContext(canvas);
            using var environment = ExecutionEnvironmentManager.BeginExecution(1, default);
            context.Init(1, canvas.Dispatcher);
            Graphics.Color = "Red";
            Graphics.SetFont("Consolas", 55);
            var shape = Graphics.Circle(3, 3, 2);
            await Task.WhenAll(context.DisposeAsync().AsTask(), context.DisposeAsync().AsTask());
            Assert.Null(Graphics.Canvas);
            Assert.Null(context.GraphicsTarget);
            Assert.Contains(shape, canvas.Children.Cast<UIElement>());
            Assert.Throws<ObjectDisposedException>(() => context.Init(2, canvas.Dispatcher));
            environment.Dispose();
            using var nextEnvironment = ExecutionEnvironmentManager.BeginExecution(2, default);
            await using var next = new CanvasGraphicsContext(canvas);
            next.Init(2, canvas.Dispatcher);
            Assert.Same(Brushes.Black, Graphics.Circle(2, 2, 1).Fill);
            Assert.Equal(20, Graphics.Text(0, 0, "default").FontSize);
        });
    }

    [Fact]
    public async Task Context_DisposeBeforeInit_AndPartialInitReleaseOwnership()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            var empty = new CanvasGraphicsContext(canvas);
            await empty.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => empty.Init(1, canvas.Dispatcher));
            await using var active = BeginDispatcherExecution(1, canvas.Dispatcher, default);
            var partial = new CanvasGraphicsContext(canvas);
            Assert.Throws<InvalidOperationException>(() => partial.Init(2, canvas.Dispatcher));
            await partial.DisposeAsync();
            Assert.Equal(4, DispatcherManager.InvokeOnUI(() => 4));
        });
    }

    [Fact]
    public async Task GraphicsExtensions_MarshalWholeOperationToUi()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            await using var scope = BeginDispatcherExecution(1, canvas.Dispatcher, default);
            Graphics.Init(canvas, scope);
            try
            {
                var text = Graphics.Text(0, 0, "hello");
                await Task.Run(() =>
                {
                    text.SetFont("Consolas", 24);
                    text.SetLeftTopXY(10, 20);
                    Assert.Equal(new Point(10, 20), text.GetCenterXY());
                    text.MoveToRightBottom(5, 6);
                    Assert.Equal(new Point(15, 26), text.GetLeftTopXY());
                }, TestContext.Current.CancellationToken);
                Assert.Equal(24, text.FontSize);
            }
            finally { await scope.ShutdownAsync(() => Graphics.Release(scope)); }
        });
    }

    [Theory]
    [InlineData("show")]
    [InlineData("hide")]
    [InlineData("move")]
    [InlineData("collision")]
    public async Task SpriteTraversal_ObservesStopInsideUiCallback(string operation)
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            using var stop = new CancellationTokenSource();
            await using var scope = BeginDispatcherExecution(1, canvas.Dispatcher, stop.Token);
            Graphics.Init(canvas, scope);
            try
            {
                // Getter отменяет token после входа в операцию, прямо перед foreach.
                // Это детерминированно отличает внутреннюю проверку от одной проверки на входе.
                var sprite = new CancelOnElementsSprite(stop);
                var error = await Task.Run(() => Record.Exception(() =>
                {
                    switch (operation)
                    {
                        case "show": sprite.Show(); break;
                        case "hide": sprite.Hide(); break;
                        case "move": sprite.Move(1, 1); break;
                        default: sprite.DetectCollisions([sprite]); break;
                    }
                }), TestContext.Current.CancellationToken);
                Assert.Equal(stop.Token, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
                await scope.ShutdownAsync(() => Graphics.Release(scope));
                Assert.Equal(0, scope.PendingCount);
            }
            finally { Graphics.Release(scope); }
        });
    }

    private sealed class CancelOnElementsSprite(CancellationTokenSource stop) : Sprite
    {
        public override List<UIElement> GraphicElements
        {
            get { stop.Cancel(); return [new Rectangle()]; }
            set { }
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("compile")]
    [InlineData("fault")]
    [InlineData("stop")]
    public async Task CompiledProgram_RealGraphicsLifecycleCleansEveryOutcome(string outcome)
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            var box = new TextBox { IsReadOnly = true };
            var localization = new StubLocalizationService();
            var service = new CodeExecutionService(new CSharpCompiler(localization),
                new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory));
            var ending = outcome switch
            {
                "compile" => "not valid C#;",
                "fault" => "throw new System.InvalidOperationException(\"user failure\");",
                "stop" => "System.Console.ReadLine();",
                _ => "KID.Graphics.SetFont(\"Consolas\", 25);"
            };
            var execution = service.ExecuteAsync("KID.Graphics.Circle(10,10,5); " + ending, token => new CodeExecutionContext
            {
                GraphicsContext = new CanvasGraphicsContext(canvas),
                ConsoleContext = new TextBoxConsoleContext(box),
                Dispatcher = canvas.Dispatcher,
                CancellationToken = token
            });
            if (outcome == "stop")
            {
                await UntilAsync(() => !box.IsReadOnly);
                Assert.True(service.RequestStop());
            }
            await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal(ExecutionState.Idle, service.State);
            Assert.Null(Graphics.Canvas);
            Assert.Equal(outcome == "compile" ? 0 : 1, canvas.Children.Count);
            Assert.Throws<InvalidOperationException>(() => DispatcherManager.InvokeOnUI(() => 0));
        });
    }

    [Fact]
    public async Task FiftyScopes_ReleaseCapturedDelegatesWhileDispatcherAndTokenRemainAlive()
    {
        await StaTest.RunAsync(async () =>
        {
            using var stop = new CancellationTokenSource();
            var references = new List<WeakReference>();
            for (var id = 1; id <= 50; id++) references.Add(await CreateAndDisposeAsync(id, stop.Token));
            for (var attempt = 0; attempt < 10 && references.Any(reference => reference.IsAlive); attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await PumpAsync();
            }
            Assert.All(references, reference => Assert.False(reference.IsAlive));
            await stop.CancelAsync();
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CreateAndDisposeAsync(long id, CancellationToken token)
    {
        var scope = BeginDispatcherExecution(id, Dispatcher.CurrentDispatcher, token);
        await Task.Run(() => DispatcherManager.InvokeOnUI(() => { }), TestContext.Current.CancellationToken);
        await scope.DisposeAsync();
        return new WeakReference(scope.Scope);
    }

    [Fact]
    public async Task ConcurrentStopPostDispose_TwentyRunsLeaveNoPendingWork()
    {
        await StaTest.RunAsync(async () =>
        {
            for (var id = 1; id <= 20; id++)
            {
                using var stop = new CancellationTokenSource();
                var scope = BeginDispatcherExecution(id, Dispatcher.CurrentDispatcher, stop.Token);
                var writes = 0;
                var producer = Task.Run(() =>
                {
                    for (var i = 0; i < 100; i++)
                    {
                        try { scope.Post(() => ++writes, true); }
                        catch (OperationCanceledException) { break; }
                        catch (ObjectDisposedException) { break; }
                    }
                }, TestContext.Current.CancellationToken);
                var cancel = Task.Run(async () => await stop.CancelAsync(), TestContext.Current.CancellationToken);
                var dispose = Task.Run(async () => await scope.DisposeAsync(), TestContext.Current.CancellationToken);
                await Task.WhenAll(producer, cancel, dispose).WaitAsync(Timeout, TestContext.Current.CancellationToken);
                Assert.Equal(0, scope.PendingCount);
                var before = writes;
                await PumpAsync();
                Assert.Equal(before, writes);
            }
        });
    }

    [Fact]
    public async Task DispatcherShutdown_IsFailure_AndCleanupStillReleasesScope()
    {
        // Dispatcher создан на отдельном потоке и закрыт до принятия команды.
        var dispatcher = await Task.Run(() =>
        {
            var owned = Dispatcher.CurrentDispatcher;
            owned.InvokeShutdown();
            return owned;
        }, TestContext.Current.CancellationToken);
        var scope = BeginDispatcherExecution(1, dispatcher, TestContext.Current.CancellationToken);
        var work = scope.Post(() => 1, false);
        var resultError = await Record.ExceptionAsync(async () => await work.Result.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.IsAssignableFrom<OperationCanceledException>(resultError);
        Assert.NotNull(await Record.ExceptionAsync(async () => await scope.DisposeAsync()));
        Assert.False(ExecutionEnvironmentManager.IsCurrent(scope.Scope.Environment));
    }

    [Fact]
    public async Task HostCleanupFailure_IsStable_AndStillReleasesScope()
    {
        await StaTest.RunAsync(async () =>
        {
            var scope = BeginDispatcherExecution(1, Dispatcher.CurrentDispatcher, default);
            var failure = new InvalidOperationException("host cleanup");
            var error = await Record.ExceptionAsync(async () => await scope.ShutdownAsync(() => throw failure));
            Assert.Same(failure, error);
            Assert.Same(failure, await Record.ExceptionAsync(async () => await scope.DisposeAsync()));
            Assert.False(ExecutionEnvironmentManager.IsCurrent(scope.Scope.Environment));
        });
    }

    [Fact]
    public async Task Service_WaitsForGraphicsCleanup_BeforeUnloadAndNextRun()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeCodeRunner();
        var service = new CodeExecutionService(FakeCodeCompiler.Returning(
            CompilationResult.FromArtifact(new CompilationArtifact(new byte[] { 1 }, new byte[] { 2 }))), runner);
        var graphics = new DelayedGraphicsContext(entered, release);
        var execution = service.ExecuteAsync("code", _ => new CodeExecutionContext
        {
            GraphicsContext = graphics,
            ConsoleContext = new TrackingConsoleContext(),
            Dispatcher = Dispatcher.CurrentDispatcher
        });
        await entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(ExecutionState.CleaningUp, service.State);
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, runner.DisposeCount);
            Assert.True(StopManager.CurrentToken.CanBeCanceled);
            var called = false;
            await service.ExecuteAsync("next", _ => { called = true; return new TrackingCodeExecutionContext(); });
            Assert.False(called);
        }
        finally
        {
            release.TrySetResult();
            await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.Equal(1, runner.DisposeCount);
    }

    private sealed class DelayedGraphicsContext(TaskCompletionSource entered, TaskCompletionSource release)
        : KID.Services.CodeExecution.Contexts.Interfaces.IGraphicsContext
    {
        public object GraphicsTarget { get; set; } = new();
        public void Init(long executionId, Dispatcher dispatcher) { }
        public async ValueTask DisposeAsync()
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CompiledQueuedDelegate_DrainsAndDoesNotRootUserAssembly()
    {
        await StaTest.RunAsync(async () =>
        {
            var reference = await RunQueuedProgramAsync();
            for (var attempt = 0; attempt < 10 && reference.IsAlive; attempt++)
            {
                await PumpAsync();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.False(reference.IsAlive);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> RunQueuedProgramAsync()
    {
        var canvas = new Canvas();
        var localization = new StubLocalizationService();
        var artifact = (await new CSharpCompiler(localization).CompileAsync(
            "KID.DispatcherManager.InvokeOnUI(() => { KID.Graphics.Circle(10,10,5); });",
            TestContext.Current.CancellationToken)).Artifact!;
        using var environment = ExecutionEnvironmentManager.BeginExecution(
            1,
            TestContext.Current.CancellationToken);
        var context = new CanvasGraphicsContext(canvas);
        context.Init(1, canvas.Dispatcher);
        var runner = new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory);
        var running = Assert.IsType<CollectibleCodeRunningInstance>(runner.Start(artifact, TestContext.Current.CancellationToken));
        try
        {
            await running.Completion;
            await context.DisposeAsync();
            Assert.Single(canvas.Children.Cast<UIElement>());
            return running.LoadContextReference!;
        }
        finally
        {
            await context.DisposeAsync();
            running.Dispose();
        }
    }

    [Fact]
    public async Task Context_InitFailureAfterPublishingGraphics_ReleasesBothOwners()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            var failure = new InvalidOperationException("partial runtime init");
            var context = new CanvasGraphicsContext(canvas, _ => throw failure);
            var environment = ExecutionEnvironmentManager.BeginExecution(
                1,
                TestContext.Current.CancellationToken);
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                context.Init(1, canvas.Dispatcher)));
            Assert.Same(canvas, Graphics.Canvas);
            await context.DisposeAsync();
            Assert.Null(Graphics.Canvas);
            environment.Dispose();
            await using var next = BeginDispatcherExecution(2, canvas.Dispatcher, default);
            Assert.Equal(7, DispatcherManager.InvokeOnUI(() => 7));
        });
    }

    [Fact]
    public async Task CompiledGraphicsCall_StopWithBlockedDispatcher_ExitsUserCodeBeforeUiCleanup()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            var box = new TextBox { IsReadOnly = true };
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var proceed = new ManualResetEvent(false);
            using var exited = new ManualResetEvent(false);
            var key = "KID.Graphics.Blocked." + Guid.NewGuid().ToString("N");
            AppContext.SetData(key + ".ready", ready);
            AppContext.SetData(key + ".proceed", proceed);
            AppContext.SetData(key + ".exited", exited);
            var localization = new StubLocalizationService();
            var service = new CodeExecutionService(new CSharpCompiler(localization),
                new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory));
            var code = $$"""
                ((System.Threading.Tasks.TaskCompletionSource)System.AppContext.GetData("{{key}}.ready")).SetResult();
                ((System.Threading.ManualResetEvent)System.AppContext.GetData("{{key}}.proceed")).WaitOne();
                try { KID.Graphics.Circle(1,1,1); }
                finally { ((System.Threading.ManualResetEvent)System.AppContext.GetData("{{key}}.exited")).Set(); }
                """;
            var execution = service.ExecuteAsync(code, token => new CodeExecutionContext
            {
                GraphicsContext = new CanvasGraphicsContext(canvas),
                ConsoleContext = new TextBoxConsoleContext(box),
                Dispatcher = canvas.Dispatcher,
                CancellationToken = token
            });
            try
            {
                await ready.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                var scope = DispatcherManager.GetScope();
                await canvas.Dispatcher.InvokeAsync(() =>
                {
                    using var blocked = canvas.Dispatcher.DisableProcessing();
                    proceed.Set();
                    Assert.True(SpinWait.SpinUntil(() => scope.PendingCount != 0, Timeout));
                    Assert.True(service.RequestStop());
                    Assert.True(exited.WaitOne(Timeout));
                    Assert.False(execution.IsCompleted);
                });
                await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                Assert.Equal(ExecutionState.Idle, service.State);
                Assert.Empty(canvas.Children);
                Assert.Null(Graphics.Canvas);
            }
            finally
            {
                proceed.Set();
                service.RequestStop();
                await execution.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                AppContext.SetData(key + ".ready", null);
                AppContext.SetData(key + ".proceed", null);
                AppContext.SetData(key + ".exited", null);
            }
        });
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private static async Task PumpAsync() =>
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    // Синхронная часть stress-сценария намеренно не качает Dispatcher: отмена и worker
    // обязаны завершаться без UI continuation. За пределами этого теста используем CancelAsync.
    private static void CancelWithBlockedUi(CancellationTokenSource stop) => stop.Cancel();

    private static DispatcherTestExecution BeginDispatcherExecution(
        long executionId,
        Dispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var environmentLease = ExecutionEnvironmentManager.BeginExecution(
            executionId,
            cancellationToken);
        try
        {
            return new DispatcherTestExecution(
                environmentLease,
                DispatcherManager.AttachDispatcher(executionId, dispatcher));
        }
        catch
        {
            environmentLease.Dispose();
            throw;
        }
    }

    private sealed class DispatcherTestExecution(
        IDisposable environmentLease,
        ExecutionDispatcherScope scope) : IAsyncDisposable
    {
        internal ExecutionDispatcherScope Scope { get; } = scope;
        internal int PendingCount => Scope.PendingCount;

        internal ExecutionDispatcherScope.Work<T> Post<T>(Func<T> action, bool reportFailure) =>
            Scope.Post(action, reportFailure);

        internal async ValueTask ShutdownAsync(Action cleanup)
        {
            try { await Scope.ShutdownAsync(cleanup); }
            finally { environmentLease.Dispose(); }
        }

        public async ValueTask DisposeAsync()
        {
            try { await Scope.DisposeAsync(); }
            finally { environmentLease.Dispose(); }
        }

        public static implicit operator ExecutionDispatcherScope(DispatcherTestExecution execution) =>
            execution.Scope;
    }
}
