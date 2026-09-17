using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Compilation;
using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Runtime;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace KID.Tests.Console;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class TextBoxConsoleCompletionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly FieldInfo UiReadField = typeof(TextBoxConsole).GetField(
        "uiRead",
        BindingFlags.NonPublic | BindingFlags.Static) ??
        throw new InvalidOperationException("TextBoxConsole.uiRead was not found.");
    private static readonly MethodInfo RestoreReadUiMethod = typeof(TextBoxConsole).GetMethod(
        "RestoreReadUi",
        BindingFlags.NonPublic | BindingFlags.Static) ??
        throw new InvalidOperationException("TextBoxConsole.RestoreReadUi was not found.");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAndReadLine_OnUiThread_AreRejectedWithoutChangingReadState(bool line)
    {
        await StaTest.RunAsync(async () =>
        {
            var box = new TextBox { IsReadOnly = true };
            await using var session = new ConsoleRuntimeSession(box, 1, CancellationToken.None);

            var error = Assert.Throws<InvalidOperationException>(() =>
            {
                if (line) _ = session.In.ReadLine();
                else _ = session.In.Read();
            });

            Assert.Equal("Console input must run outside the UI thread.", error.Message);
            Assert.True(box.IsReadOnly);
            Assert.Null(UiReadField.GetValue(null));
            Assert.Same(session.Scope, TextBoxConsole.CurrentScope);
        });
    }

    [Fact]
    public async Task StaleReadRequest_CannotRestoreFocusOrReadOnlyStateOfNextExecution()
    {
        await StaTest.RunAsync(async () =>
        {
            var focusScope = new StackPanel();
            FocusManager.SetIsFocusScope(focusScope, true);
            var previousFocus = new TextBox();
            var oldBox = new TextBox { IsReadOnly = true };
            var nextBox = new TextBox { IsReadOnly = true };
            focusScope.Children.Add(previousFocus);
            focusScope.Children.Add(oldBox);
            focusScope.Children.Add(nextBox);
            FocusManager.SetFocusedElement(focusScope, previousFocus);

            var old = new ConsoleRuntimeSession(oldBox, 1, CancellationToken.None);
            var oldRead = Task.Run(
                () => Record.Exception(() => old.In.Read()),
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !oldBox.IsReadOnly);
            var staleRequest = Assert.IsAssignableFrom<object>(UiReadField.GetValue(null));

            await old.DisposeAsync();
            Assert.IsType<ObjectDisposedException>(
                await oldRead.WaitAsync(Timeout, TestContext.Current.CancellationToken));

            await using var current = new ConsoleRuntimeSession(nextBox, 2, CancellationToken.None);
            var currentRead = Task.Run(
                () => current.In.Read(),
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => !nextBox.IsReadOnly);
            var currentRequest = Assert.IsAssignableFrom<object>(UiReadField.GetValue(null));
            var logicalFocus = FocusManager.GetFocusedElement(focusScope);

            _ = RestoreReadUiMethod.Invoke(null, [staleRequest]);

            Assert.Same(currentRequest, UiReadField.GetValue(null));
            Assert.False(nextBox.IsReadOnly);
            Assert.Same(logicalFocus, FocusManager.GetFocusedElement(focusScope));
            Assert.True(SendText(nextBox, "N").Handled);
            Assert.Equal('N', (char)await currentRead.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task ParallelWrites_AreSerializedWithoutLossOrCorruption()
    {
        await StaTest.RunAsync(async () =>
        {
            const int writeCount = 200;
            var box = new TextBox();
            await using var session = new ConsoleRuntimeSession(box, 1, CancellationToken.None);
            var writer = session.Out;
            using var start = new ManualResetEventSlim();
            var tasks = Enumerable.Range(0, writeCount)
                .Select(index => Task.Run(() =>
                {
                    start.Wait(TestContext.Current.CancellationToken);
                    writer.Write($"{index:D4};");
                }, TestContext.Current.CancellationToken))
                .ToArray();

            start.Set();
            await Task.WhenAll(tasks).WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await box.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            var tokens = box.Text.Split(';', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(writeCount, tokens.Length);
            Assert.Equal(
                Enumerable.Range(0, writeCount).Select(index => index.ToString("D4")).Order(),
                tokens.Order());
        });
    }

    [Fact]
    public async Task OutputActionFailure_IsReportedWhileContextRestoresStreamsAndSharesDisposeFailure()
    {
        await StaTest.RunAsync(async () =>
        {
            var originalOut = global::System.Console.Out;
            var originalIn = global::System.Console.In;
            var originalError = global::System.Console.Error;
            var box = new TextBox();
            var failure = new InvalidOperationException("WPF output action failed.");
            TextChangedEventHandler throwingHandler = (_, _) => throw failure;
            box.TextChanged += throwingHandler;
            using var stop = new CancellationTokenSource();
            var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, stop.Token);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var context = new TextBoxConsoleContext(box);

            try
            {
                context.Init(1, stop.Token);
                var scope = Assert.IsType<ConsoleExecutionScope>(TextBoxConsole.CurrentScope);
                global::System.Console.Write("trigger");
                environmentLease.BeginCleanup();
                context.BeginCleanup();

                var firstDispose = context.DisposeAsync().AsTask();
                var repeatedDispose = context.DisposeAsync().AsTask();
                Assert.Same(firstDispose, repeatedDispose);

                var firstError = await Record.ExceptionAsync(() => firstDispose.WaitAsync(
                    Timeout,
                    TestContext.Current.CancellationToken));
                var repeatedError = await Record.ExceptionAsync(() => repeatedDispose.WaitAsync(
                    Timeout,
                    TestContext.Current.CancellationToken));

                Assert.Same(failure, firstError);
                Assert.Same(firstError, repeatedError);
                Assert.Same(originalOut, global::System.Console.Out);
                Assert.Same(originalIn, global::System.Console.In);
                Assert.Same(originalError, global::System.Console.Error);
                Assert.Null(TextBoxConsole.CurrentScope);
                Assert.True(scope.EventWorker.Completion.IsCompletedSuccessfully);
                Assert.Equal(0, scope.EventWorker.QueuedCount);
                Assert.False(SendText(box, "late").Handled);
                await stop.CancelAsync();
            }
            finally
            {
                box.TextChanged -= throwingHandler;
                if (TextBoxConsole.CurrentScope != null)
                {
                    environmentLease.BeginCleanup();
                    TextBoxConsole.BeginCleanup(environment);
                    _ = await Record.ExceptionAsync(
                        () => TextBoxConsole.ShutdownAsync(environment).AsTask());
                }
                environmentLease.Dispose();
                global::System.Console.SetOut(originalOut);
                global::System.Console.SetIn(originalIn);
                global::System.Console.SetError(originalError);
            }
        });
    }

    [Fact]
    public async Task DispatcherTeardownFailure_StillReleasesWorkerHandlesAndNextExecution()
    {
        using var stop = new CancellationTokenSource();
        var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, stop.Token);
        var environment = ExecutionEnvironmentManager.GetCurrent(1);
        var initialized = new TaskCompletionSource<(Dispatcher Dispatcher, ConsoleExecutionScope Scope)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => RunConsoleDispatcher(environment, initialized))
        {
            IsBackground = true,
            Name = "KID.Tests.Console.DispatcherTeardown"
        };
        thread.SetApartmentState(ApartmentState.STA);
        Dispatcher? dispatcher = null;
        ConsoleExecutionScope? scope = null;

        try
        {
            thread.Start();
            (dispatcher, scope) = await initialized.Task.WaitAsync(
                Timeout,
                TestContext.Current.CancellationToken);
            environmentLease.BeginCleanup();
            TextBoxConsole.BeginCleanup(environment);
            await dispatcher.InvokeAsync(
                () => dispatcher.BeginInvokeShutdown(DispatcherPriority.Send),
                DispatcherPriority.Send,
                TestContext.Current.CancellationToken).Task.WaitAsync(
                    Timeout,
                    TestContext.Current.CancellationToken);
            Assert.True(thread.Join(Timeout), "Console Dispatcher did not stop.");

            var error = await Record.ExceptionAsync(() =>
                TextBoxConsole.ShutdownAsync(environment).AsTask());

            Assert.NotNull(error);
            Assert.Null(TextBoxConsole.CurrentScope);
            Assert.True(scope.EventWorker.Completion.IsCompletedSuccessfully);
            Assert.Equal(0, scope.EventWorker.QueuedCount);
            await stop.CancelAsync();
        }
        finally
        {
            if (dispatcher is { HasShutdownStarted: false })
            {
                _ = dispatcher.BeginInvoke(
                    () => dispatcher.BeginInvokeShutdown(DispatcherPriority.Send),
                    DispatcherPriority.Send);
            }
            if (thread.IsAlive) _ = thread.Join(Timeout);
            if (TextBoxConsole.CurrentScope != null)
            {
                environmentLease.BeginCleanup();
                TextBoxConsole.BeginCleanup(environment);
                _ = await Record.ExceptionAsync(
                    () => TextBoxConsole.ShutdownAsync(environment).AsTask());
            }
            environmentLease.Dispose();
        }

        await StaTest.RunAsync(async () =>
        {
            await using var next = new ConsoleRuntimeSession(
                new TextBox(),
                2,
                CancellationToken.None);
            Assert.Same(next.Scope, TextBoxConsole.CurrentScope);
        });
    }

    [Fact]
    public async Task CompiledOutputSubscriber_DoesNotRetainCollectibleAssemblyAfterCleanup()
    {
        var referenceKey = $"KID.Tests.Console.OutputSubscriber.{Guid.NewGuid():N}";
        try
        {
            var reference = await StaTestWithResult.RunAsync(() =>
                RunCompiledOutputSubscriberAsync(referenceKey));

            for (var attempt = 0; attempt < 10 && reference.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(reference.IsAlive);
        }
        finally
        {
            AppContext.SetData(referenceKey, null);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> RunCompiledOutputSubscriberAsync(string referenceKey)
    {
        var box = new TextBox();
        var localization = new StubLocalizationService();
        var service = new CodeExecutionService(
            new CSharpCompiler(localization),
            new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory));
        var code = $$"""
            public static class Program
            {
                private static readonly System.Threading.ManualResetEventSlim Delivered = new(false);

                public static void Main()
                {
                    var loadContext = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(
                        typeof(Program).Assembly)!;
                    System.AppContext.SetData(
                        "{{referenceKey}}",
                        new System.WeakReference(loadContext));
                    KID.TextBoxConsole.OutputReceived += OnOutput;
                    System.Console.Write("compiled-event");
                    if (!Delivered.Wait(System.TimeSpan.FromSeconds(5)))
                        throw new System.TimeoutException("OutputReceived was not delivered.");
                }

                private static void OnOutput(string value) => Delivered.Set();
            }
            """;

        await service.ExecuteAsync(code, token => new CodeExecutionContext
        {
            ConsoleContext = new TextBoxConsoleContext(box),
            GraphicsContext = new TrackingGraphicsContext(),
            Dispatcher = box.Dispatcher,
            CancellationToken = token
        }).WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.Contains("compiled-event", box.Text);
        Assert.Null(TextBoxConsole.CurrentScope);
        return Assert.IsType<WeakReference>(AppContext.GetData(referenceKey));
    }

    private static void RunConsoleDispatcher(
        ExecutionEnvironment environment,
        TaskCompletionSource<(Dispatcher Dispatcher, ConsoleExecutionScope Scope)> initialized)
    {
        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(dispatcher));
            _ = dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var scope = TextBoxConsole.Init(new TextBox(), environment);
                    initialized.TrySetResult((dispatcher, scope));
                }
                catch (Exception exception)
                {
                    initialized.TrySetException(exception);
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                }
            }, DispatcherPriority.Send);
            Dispatcher.Run();
        }
        catch (Exception exception)
        {
            initialized.TrySetException(exception);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for console state.");
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
    }

    private static TextCompositionEventArgs SendText(TextBox box, string text)
    {
        var args = new TextCompositionEventArgs(
            InputManager.Current.PrimaryKeyboardDevice,
            new TextComposition(InputManager.Current, box, text))
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent
        };
        box.RaiseEvent(args);
        return args;
    }

    private sealed class ConsoleRuntimeSession : IAsyncDisposable
    {
        private readonly IExecutionEnvironmentLease environmentLease;
        private readonly ExecutionEnvironment environment;
        private Task? disposalTask;

        internal ConsoleRuntimeSession(
            TextBox box,
            long executionId,
            CancellationToken cancellationToken)
        {
            environmentLease = ExecutionEnvironmentManager.BeginExecution(
                executionId,
                cancellationToken);
            try
            {
                environment = ExecutionEnvironmentManager.GetCurrent(executionId);
                Scope = TextBoxConsole.Init(box, environment);
            }
            catch
            {
                environmentLease.Dispose();
                throw;
            }
        }

        internal ConsoleExecutionScope Scope { get; }
        internal TextWriter Out => TextBoxConsole.GetOut(Scope);
        internal TextReader In => TextBoxConsole.GetIn(Scope);

        public ValueTask DisposeAsync()
        {
            disposalTask ??= DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }

        private async Task DisposeCoreAsync()
        {
            environmentLease.BeginCleanup();
            TextBoxConsole.BeginCleanup(environment);
            try
            {
                await TextBoxConsole.ShutdownAsync(environment);
            }
            finally
            {
                environmentLease.Dispose();
            }
        }
    }

    private static class StaTestWithResult
    {
        internal static async Task<T> RunAsync<T>(Func<Task<T>> action)
        {
            T? result = default;
            await StaTest.RunAsync(async () => result = await action());
            return result!;
        }
    }
}
