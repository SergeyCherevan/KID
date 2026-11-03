using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class RuntimeSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task InvalidOptionsAndCanceledToken_DoNotPublishEnvironment()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await OpenAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await OpenAsync(
            new KIDRuntimeOptions { RedirectSystemConsole = true }));
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), canceled.Token));
        Assert.Null(ExecutionEnvironmentManager.Current);
        await using var next = await OpenAsync(new KIDRuntimeOptions());
    }

    [Theory]
    [InlineData("Token")]
    [InlineData("Graphics")]
    [InlineData("Mouse")]
    [InlineData("Keyboard")]
    [InlineData("Music")]
    [InlineData("Console")]
    public async Task OptionalComponents_AttachOnlyExplicitResources(string component)
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            var textBox = new TextBox();
            var output = System.Console.Out;
            var input = System.Console.In;
            var error = System.Console.Error;
            await using (var session = await OpenAsync(new KIDRuntimeOptions
            {
                Canvas = component == "Graphics" ? canvas : null,
                MouseCanvas = component == "Mouse" ? canvas : null,
                KeyboardWindow = component == "Keyboard" ? new Window() : null,
                EnableMusic = component == "Music",
                ConsoleTextBox = component == "Console" ? textBox : null
            }))
            {
                Assert.Equal(session.CancellationToken, StopManager.CurrentToken);
                Assert.Equal(component == "Graphics", Graphics.Canvas != null);
                Assert.Equal(component == "Mouse", Mouse.CurrentScope != null);
                Assert.Equal(component == "Keyboard", Keyboard.CurrentScope != null);
                Assert.Equal(component == "Music", Music.CurrentScope != null);
                Assert.Equal(component == "Console", TextBoxConsole.CurrentScope != null);
                Assert.Same(output, System.Console.Out);
                Assert.Same(input, System.Console.In);
                Assert.Same(error, System.Console.Error);
                if (component == "Graphics")
                    Assert.True(canvas.Children.Contains(await Task.Run(() => Graphics.Plot(1, 2))));
                if (component == "Console") TextBoxConsole.Write("drain me");
            }
            if (component == "Console") Assert.Equal("drain me", textBox.Text);
            AssertResourcesReleased();
        });
    }

    [Fact]
    public async Task BackgroundTokenAndMusicSessions_DoNotCreateDispatcher()
    {
        // A fresh thread makes absence of an implicitly created Dispatcher observable.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Assert.Null(System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread));
                var session = OpenAsync(new KIDRuntimeOptions { EnableMusic = true })
                    .AsTask().GetAwaiter().GetResult();
                Assert.Null(System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread));
                session.DisposeAsync().AsTask().GetAwaiter().GetResult();
                Assert.Null(System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread));
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.Start();
        await completion.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WrongThreadAndDifferentDispatchers_FailBeforePublication()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            await Task.Run(async () => await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await OpenAsync(new KIDRuntimeOptions { Canvas = canvas })));
            await StaTest.RunAsync(async () =>
            {
                await Assert.ThrowsAsync<ArgumentException>(async () => await OpenAsync(
                    new KIDRuntimeOptions { Canvas = canvas, ConsoleTextBox = new TextBox() }));
            });
            AssertResourcesReleased();
            await using var next = await OpenAsync(new KIDRuntimeOptions { Canvas = canvas });
        });
    }

    [Fact]
    public async Task ConcurrentOpen_HasOneOwnerAndPreservesItsToken()
    {
        async Task<(KIDRuntimeSession? Session, Exception? Failure)> TryOpenAsync()
        {
            try { return (await OpenAsync(new KIDRuntimeOptions()), null); }
            catch (Exception exception) { return (null, exception); }
        }
        var attempts = await Task.WhenAll(Task.Run(TryOpenAsync), Task.Run(TryOpenAsync));
        var owner = Assert.Single(attempts, attempt => attempt.Session != null).Session!;
        try
        {
            Assert.IsType<InvalidOperationException>(Assert.Single(attempts, attempt => attempt.Failure != null).Failure);
            Assert.Equal(owner.CancellationToken, StopManager.CurrentToken);
            Assert.Throws<InvalidOperationException>(() => ExecutionEnvironmentManager.BeginExecution(999, TestContext.Current.CancellationToken));
        }
        finally { await owner.DisposeAsync(); }
    }

    [Fact]
    public async Task NormalDispose_DrainsAcceptedNestedCommandsAndRejectsNewWorkSynchronously()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            await using var session = await OpenAsync(new KIDRuntimeOptions { Canvas = canvas });
            // Keep the UI busy until the void command has returned after posting to the real queue.
            PostWithoutPumping(() => { _ = Graphics.Plot(3, 4); });
            Assert.Empty(canvas.Children);

            var disposal = session.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.False(session.CancellationToken.IsCancellationRequested);
            Assert.Throws<ObjectDisposedException>(() => Graphics.Plot(5, 6));
            await disposal;
            Assert.Single(canvas.Children);
            AssertResourcesReleased();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_AbortsPendingWorkRetainsAdmissionAndDoesNotAffectNextSession(bool externalCancellation)
    {
        await StaTest.RunAsync(async () =>
        {
            using var source = new CancellationTokenSource();
            var canvas = new Canvas();
            var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions { Canvas = canvas }, source.Token);
            PostWithoutPumping(() => { _ = Graphics.Plot(3, 4); });
            if (externalCancellation) CancelSynchronously(source);
            else session.RequestStop();
            session.RequestStop();
            Assert.True(session.CancellationToken.IsCancellationRequested);
            Assert.Equal(session.CancellationToken, StopManager.CurrentToken);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await OpenAsync(new KIDRuntimeOptions()));
            await Task.WhenAll(Task.Run(async () => await session.DisposeAsync()),
                Task.Run(async () => await session.DisposeAsync()));
            Assert.Empty(canvas.Children);

            await using var next = await OpenAsync(new KIDRuntimeOptions { Canvas = canvas });
            await session.DisposeAsync();
            session.RequestStop();
            Assert.False(next.CancellationToken.IsCancellationRequested);
            Assert.Equal(next.CancellationToken, StopManager.CurrentToken);
            _ = Graphics.Plot(7, 8);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsoleReader_StopIsCancellationAndNormalDisposeIsClosure(bool stop)
    {
        await StaTest.RunAsync(async () =>
        {
            var textBox = new TextBox { IsReadOnly = true };
            await using var session = await OpenAsync(new KIDRuntimeOptions { ConsoleTextBox = textBox });
            var readReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var descriptor = DependencyPropertyDescriptor.FromProperty(TextBox.IsReadOnlyProperty, typeof(TextBox))!;
            EventHandler readUiChanged = (_, _) =>
            {
                if (!textBox.IsReadOnly) readReady.TrySetResult();
            };
            descriptor.AddValueChanged(textBox, readUiChanged);
            try
            {
                var read = Task.Run(() => Record.Exception(() => TextBoxConsole.ReadLine()), TestContext.Current.CancellationToken);
                await readReady.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                if (stop) session.RequestStop();
                await session.DisposeAsync();
                var failure = await read.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                if (stop)
                    Assert.Equal(session.CancellationToken, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
                else Assert.IsType<ObjectDisposedException>(failure);
                Assert.True(textBox.IsReadOnly);
            }
            finally { descriptor.RemoveValueChanged(textBox, readUiChanged); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialConsoleRedirection_RollsBackEveryAcquiredComponentAndAllStreams(bool cancel)
    {
        await StaTest.RunAsync(async () =>
        {
            using var source = new CancellationTokenSource();
            var output = System.Console.Out;
            var input = System.Console.In;
            var error = System.Console.Error;
            var openingFailure = new InvalidOperationException("partial redirection");
            var diagnostics = new List<KIDRuntimeDiagnostic>();
            var canvas = new Canvas();
            var options = new KIDRuntimeOptions
            {
                Canvas = canvas,
                MouseCanvas = canvas,
                KeyboardWindow = new Window(),
                EnableMusic = true,
                ConsoleTextBox = new TextBox(),
                RedirectSystemConsole = true,
                DiagnosticSink = diagnostics.Add
            };
            var failure = await Record.ExceptionAsync(async () => await RuntimeSessionOwner.OpenAsync(
                options, source.Token, (newOut, _, _) =>
                {
                    System.Console.SetOut(newOut);
                    if (cancel) CancelSynchronously(source);
                    else throw openingFailure;
                }));
            if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(failure);
            else Assert.Same(openingFailure, failure);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Operation == "Open");
            Assert.Same(output, System.Console.Out);
            Assert.Same(input, System.Console.In);
            Assert.Same(error, System.Console.Error);
            AssertResourcesReleased();
            await using var next = await OpenAsync(new KIDRuntimeOptions { Canvas = canvas });
        });
    }

    [Fact]
    public async Task DiagnosticSinkFailure_DoesNotInterruptRuntimeOrCleanup()
    {
        await StaTest.RunAsync(async () =>
        {
            var diagnosticReceived = new TaskCompletionSource<KIDRuntimeDiagnostic>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using (var session = await OpenAsync(new KIDRuntimeOptions
            {
                MouseCanvas = new Canvas(),
                DiagnosticSink = diagnostic =>
                {
                    diagnosticReceived.TrySetResult(diagnostic);
                    throw new InvalidOperationException("sink failure");
                }
            }))
            {
                var handlerFailure = new InvalidOperationException("handler failure");
                Assert.True(Mouse.CurrentScope!.EventWorker.TryEnqueue(() => throw handlerFailure));
                var diagnostic = await diagnosticReceived.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                Assert.Same(handlerFailure, diagnostic.Exception);
                Assert.Equal("Mouse", diagnostic.Component);
                Assert.Equal("EventHandler", diagnostic.Operation);
                Assert.Equal(ExecutionEnvironmentManager.Current!.ExecutionId, diagnostic.ExecutionId);
            }
            AssertResourcesReleased();
        });
    }

    [Fact]
    public async Task TwoPhaseCleanup_RetainsReservationUntilHostFinalization()
    {
        await StaTest.RunAsync(async () =>
        {
            var owner = await RuntimeSessionOwner.OpenAsync(new KIDRuntimeOptions { Canvas = new Canvas() }, TestContext.Current.CancellationToken);
            await owner.CleanupAsync();
            Assert.Equal(owner.CancellationToken, StopManager.CurrentToken);
            Assert.Null(Graphics.Canvas);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await OpenAsync(new KIDRuntimeOptions()));
            Assert.Throws<InvalidOperationException>(() => ExecutionEnvironmentManager.BeginExecution(1000, TestContext.Current.CancellationToken));
            await owner.CompleteAsync();
            await using var next = await OpenAsync(new KIDRuntimeOptions());
        });
    }

    [Fact]
    public async Task LegacyInit_UsesSessionOwnershipAndRejectsAnotherDispatcher()
    {
        await StaTest.RunAsync(async () =>
        {
            await using (var session = await OpenAsync(new KIDRuntimeOptions()))
            {
                KID.Mouse.Init(new Canvas());
                await StaTest.RunAsync(() => Assert.Throws<ArgumentException>(() => KID.Keyboard.Init(new Window())));
                KID.Keyboard.Init(new Window());
                Music.Init();
                Assert.Throws<InvalidOperationException>(() => Music.Init());
            }
            AssertResourcesReleased();
        });
    }

    private static ValueTask<KIDRuntimeSession> OpenAsync(KIDRuntimeOptions options) =>
        KIDRuntimeSession.OpenAsync(options, TestContext.Current.CancellationToken);

    private static void CancelSynchronously(CancellationTokenSource source) => source.Cancel();

    private static void PostWithoutPumping(Action action)
    {
        var producer = Task.Run(() => DispatcherManager.InvokeOnUI(action), TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(() => producer.IsCompleted, Timeout));
        Assert.True(producer.IsCompletedSuccessfully, producer.Exception?.ToString());
    }

    private static void AssertResourcesReleased()
    {
        Assert.Null(ExecutionEnvironmentManager.Current);
        Assert.Null(Graphics.Canvas);
        Assert.Null(Mouse.CurrentScope);
        Assert.Null(Keyboard.CurrentScope);
        Assert.Null(Music.CurrentScope);
        Assert.Null(TextBoxConsole.CurrentScope);
        Assert.Equal(default, StopManager.CurrentToken);
    }
}
