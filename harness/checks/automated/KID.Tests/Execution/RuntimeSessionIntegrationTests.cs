using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Contexts;
using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;
using System.Windows;
using System.Windows.Controls;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class RuntimeSessionIntegrationTests
{
    [Fact]
    public async Task IdeAndStandalone_ShareIdentityTokenAndAdmissionThroughAlcDisposal()
    {
        await StaTest.RunAsync(async () =>
        {
            var originalOut = System.Console.Out;
            var originalIn = System.Console.In;
            var originalError = System.Console.Error;
            long previousId;
            await using (var standalone = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken))
                previousId = ExecutionEnvironmentManager.Current!.ExecutionId;

            var canvas = new Canvas();
            var window = new Window { Content = canvas };
            var textBox = new TextBox();
            var otherCanvas = new Canvas();
            CancellationToken factoryToken = default;
            long ideId = 0;
            var compiler = new FakeCodeCompiler((_, token) =>
            {
                Assert.Equal(factoryToken, token);
                Assert.Equal(token, StopManager.CurrentToken);
                ideId = ExecutionEnvironmentManager.Current!.ExecutionId;
                Assert.True(ideId > previousId);
                return Task.FromResult(CompilationResult.FromArtifact(new CompilationArtifact(new byte[] { 1 })));
            });
            CodeExecutionService? service = null;
            var runner = new FakeCodeRunner(async (_, token) =>
            {
                Assert.Equal(factoryToken, token);
                Assert.Equal(ideId, service!.CurrentExecutionId);
                var mouse = Mouse.CurrentScope;
                Graphics.Circle(1, 1, 1);
                Graphics.SetCanvas(otherCanvas);
                Graphics.Circle(2, 2, 1);
                Assert.Same(mouse, Mouse.CurrentScope);
                System.Console.Write("accepted output");
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken));
            }, disposeAction: () =>
            {
                // This is the ALC/instance disposal boundary: library cleanup is complete,
                // but neither public hosts nor another IDE coordinator may reserve a run yet.
                Assert.Equal(factoryToken, StopManager.CurrentToken);
                Assert.Null(Graphics.Canvas);
                Assert.Null(Mouse.CurrentScope);
                Assert.Null(Keyboard.CurrentScope);
                Assert.Null(Music.CurrentScope);
                Assert.Null(TextBoxConsole.CurrentScope);
                Assert.Equal("accepted output", textBox.Text);
                Assert.Same(originalOut, System.Console.Out);
                Assert.Same(originalIn, System.Console.In);
                Assert.Same(originalError, System.Console.Error);
                Assert.Throws<InvalidOperationException>(() => RuntimeSessionOwner.Reserve(TestContext.Current.CancellationToken));
                var other = new CodeExecutionService(compiler, new FakeCodeRunner());
                var rejected = other.ExecuteAsync("other", _ => throw new InvalidOperationException("Factory must not run."));
                Assert.True(rejected.IsFaulted);
                Assert.IsType<InvalidOperationException>(rejected.Exception!.InnerException);
            });
            service = new CodeExecutionService(compiler, runner);
            var result = await service.ExecuteAsync("script", token =>
            {
                factoryToken = token;
                return CreateContext(canvas, textBox);
            });
            Assert.Equal(ExecutionResultKind.Completed, result.Kind);
            Assert.Equal(ExecutionState.Idle, service.State);
            Assert.Single(canvas.Children);
            Assert.Single(otherCanvas.Children);
            await using var next = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken);
            Assert.True(ExecutionEnvironmentManager.Current!.ExecutionId > ideId);
            GC.KeepAlive(window);
        });
    }

    [Fact]
    public async Task StandaloneOwner_RejectsIdeBeforeContextCreation_AndAllowsRunAfterDisposal()
    {
        var compiler = FakeCodeCompiler.Returning(CompilationResult.FromErrors([]));
        var service = new CodeExecutionService(compiler, new FakeCodeRunner());
        await using (var standalone = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken))
        {
            var token = standalone.CancellationToken;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("script", _ =>
                throw new InvalidOperationException("Factory must not run.")));
            Assert.Equal(0, compiler.CallCount);
            Assert.Equal(token, StopManager.CurrentToken);
            Assert.Equal(ExecutionState.Idle, service.State);
        }
        await service.ExecuteAsync("script", _ => new TrackingCodeExecutionContext());
        Assert.Equal(1, compiler.CallCount);
    }

    [Fact]
    public async Task ContextFactoryFailure_CleansResourcesEvenWithoutReturnedContext()
    {
        await StaTest.RunAsync(async () =>
        {
            var failure = new InvalidOperationException("context factory failed");
            var compiler = FakeCodeCompiler.Returning(CompilationResult.FromErrors([]));
            var service = new CodeExecutionService(compiler, new FakeCodeRunner());
            var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("script", _ =>
            {
                Mouse.Init(new Canvas());
                Music.Init();
                throw failure;
            }));
            Assert.Same(failure, observed);
            Assert.Equal(0, compiler.CallCount);
            Assert.Null(Mouse.CurrentScope);
            Assert.Null(Music.CurrentScope);
            Assert.Null(ExecutionEnvironmentManager.Current);
            Assert.Equal(ExecutionState.Idle, service.State);
            await using var next = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task SessionFactoryFailure_ReleasesReservationBeforeReturningError()
    {
        var failure = new InvalidOperationException("session factory failed");
        var service = new CodeExecutionService(
            FakeCodeCompiler.Returning(CompilationResult.FromErrors([])), new FakeCodeRunner(), _ => throw failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync("script", _ => new TrackingCodeExecutionContext())));
        Assert.Equal(ExecutionState.Idle, service.State);
        Assert.Null(ExecutionEnvironmentManager.Current);
        await using var next = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PartialConsoleRedirection_RollsBackGraphicsAndStreams_AndAllowsNextRun()
    {
        await StaTest.RunAsync(async () =>
        {
            var original = System.Console.Out;
            var failure = new InvalidOperationException("partial console redirect");
            var compiler = FakeCodeCompiler.Returning(CompilationResult.FromErrors([]));
            var service = new CodeExecutionService(compiler, new FakeCodeRunner());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("script", _ =>
            {
                var context = CreateContext(new Canvas(), new TextBox());
                context.ConsoleContext = new TextBoxConsoleContext(new TextBox(), (output, _, _) =>
                {
                    System.Console.SetOut(output);
                    throw failure;
                });
                return context;
            }));
            Assert.Equal(0, compiler.CallCount);
            Assert.Same(original, System.Console.Out);
            Assert.Null(Graphics.Canvas);
            Assert.Null(Mouse.CurrentScope);
            Assert.Null(Music.CurrentScope);
            Assert.Null(TextBoxConsole.CurrentScope);
            Assert.Equal(ExecutionState.Idle, service.State);
            await service.ExecuteAsync("script", _ => CreateContext(new Canvas(), new TextBox()));
            await using var standalone = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task LibraryFailure_BlocksBothHostsAndRestoresStreams()
    {
        if (await IsolatedTest.RunAsync(typeof(RuntimeSessionIntegrationTests))) return;
        await VerifyFailureAsync("Library");
    }

    [Fact]
    public async Task InstanceFailure_BlocksBothHostsAndRestoresStreams()
    {
        if (await IsolatedTest.RunAsync(typeof(RuntimeSessionIntegrationTests))) return;
        await VerifyFailureAsync("Instance");
    }

    [Fact]
    public async Task StopCallbackFailure_BlocksBothHostsAndRestoresStreams()
    {
        if (await IsolatedTest.RunAsync(typeof(RuntimeSessionIntegrationTests))) return;
        await VerifyFailureAsync("StopCallback");
    }

    private static async Task VerifyFailureAsync(string failingStep)
    {
        await StaTest.RunAsync(async () =>
        {
            var original = System.Console.Out;
            var failure = new InvalidOperationException($"{failingStep} cleanup failed");
            var runner = new FakeCodeRunner(async (_, token) =>
            {
                if (failingStep == "Library")
                    await Task.Run(() => DispatcherManager.InvokeOnUI((Action)(() => throw failure)), TestContext.Current.CancellationToken);
                if (failingStep == "StopCallback")
                {
                    using var registration = token.Register(() => throw failure);
                    RuntimeSessionOwner.GetCurrent(ExecutionEnvironmentManager.Current!.ExecutionId).RequestStop();
                }
            }, disposeAction: () =>
            {
                Assert.Same(original, System.Console.Out);
                Assert.Null(Graphics.Canvas);
                if (failingStep == "Instance") throw failure;
            });
            var service = new CodeExecutionService(
                FakeCodeCompiler.Returning(CompilationResult.FromArtifact(new CompilationArtifact(new byte[] { 1 }))), runner);
            var error = await Record.ExceptionAsync(() => service.ExecuteAsync("script", _ => CreateContext(new Canvas(), new TextBox())));
            Assert.NotNull(error);
            var errors = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : [error];
            Assert.Single(errors, exception => ReferenceEquals(exception, failure));
            Assert.Equal(1, runner.DisposeCount);
            Assert.Same(original, System.Console.Out);
            Assert.Equal(ExecutionState.CleaningUp, service.State);
            Assert.Null(ExecutionEnvironmentManager.Current);
            Assert.Equal(ExecutionResultKind.Rejected, (await service.ExecuteAsync("again", _ => new TrackingCodeExecutionContext())).Kind);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken));
            var other = new CodeExecutionService(FakeCodeCompiler.Returning(CompilationResult.FromErrors([])), new FakeCodeRunner());
            await Assert.ThrowsAsync<InvalidOperationException>(() => other.ExecuteAsync("again", _ => new TrackingCodeExecutionContext()));
        });
    }

    private static CodeExecutionContext CreateContext(Canvas canvas, TextBox textBox) => new()
    {
        GraphicsContext = new CanvasGraphicsContext(canvas),
        ConsoleContext = new TextBoxConsoleContext(textBox),
        Dispatcher = canvas.Dispatcher
    };
}
