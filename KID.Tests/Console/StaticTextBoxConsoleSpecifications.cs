using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Contexts;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using System.Windows.Controls;
using System.Windows.Input;

namespace KID.Tests.Console;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class StaticTextBoxConsoleSpecifications
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Scope_IsPassiveIdentityForEnvironmentTextBoxAndEventWorker()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var box = new TextBox();
            var scope = new ConsoleExecutionScope(environment, box);

            Assert.Same(environment, scope.Environment);
            Assert.Same(box, scope.TextBox);
            Assert.Same(environment, scope.EventWorker.Environment);

            await scope.EventWorker.ShutdownAsync();
        });
    }

    [Fact]
    public async Task Init_RequiresCurrentAcceptingEnvironment()
    {
        await StaTest.RunAsync(() =>
        {
            var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            environmentLease.Dispose();

            var error = Assert.Throws<InvalidOperationException>(() =>
                TextBoxConsole.Init(new TextBox(), environment));

            Assert.Equal("Execution does not own the current environment.", error.Message);
        });
    }

    [Fact]
    public async Task Init_RequiresTextBoxDispatcherThread()
    {
        await StaTest.RunAsync(async () =>
        {
            using var environmentLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var environment = ExecutionEnvironmentManager.GetCurrent(1);
            var box = new TextBox();

            var error = await Task.Run(
                () => Record.Exception(() => TextBoxConsole.Init(box, environment)),
                TestContext.Current.CancellationToken);

            Assert.IsType<InvalidOperationException>(error);
            Assert.Null(TextBoxConsole.CurrentScope);
        });
    }

    [Fact]
    public async Task SecondInit_IsRejectedUntilOwningShutdownCompletes()
    {
        await StaTest.RunAsync(async () =>
        {
            var firstLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var firstEnvironment = ExecutionEnvironmentManager.GetCurrent(1);
            try
            {
                _ = TextBoxConsole.Init(new TextBox(), firstEnvironment);
                Assert.Throws<InvalidOperationException>(() =>
                    TextBoxConsole.Init(new TextBox(), firstEnvironment));

                firstLease.BeginCleanup();
                TextBoxConsole.BeginCleanup(firstEnvironment);
                await TextBoxConsole.ShutdownAsync(firstEnvironment);
                Assert.Null(TextBoxConsole.CurrentScope);
            }
            finally
            {
                firstLease.Dispose();
            }

            using var nextLease = ExecutionEnvironmentManager.BeginExecution(2, CancellationToken.None);
            var nextEnvironment = ExecutionEnvironmentManager.GetCurrent(2);
            _ = TextBoxConsole.Init(new TextBox(), nextEnvironment);
            nextLease.BeginCleanup();
            TextBoxConsole.BeginCleanup(nextEnvironment);
            await TextBoxConsole.ShutdownAsync(nextEnvironment);
        });
    }

    [Fact]
    public async Task RetainedScopeStreams_CannotAffectNextExecution()
    {
        await StaTest.RunAsync(async () =>
        {
            var oldBox = new TextBox { IsReadOnly = true };
            var oldLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var oldEnvironment = ExecutionEnvironmentManager.GetCurrent(1);
            var oldScope = TextBoxConsole.Init(oldBox, oldEnvironment);
            var oldWriter = TextBoxConsole.GetOut(oldScope);
            var oldReader = TextBoxConsole.GetIn(oldScope);

            Assert.DoesNotContain(
                oldWriter.GetType().GetMethods(),
                method => method.Name == nameof(TextBoxConsole.Clear));

            await oldWriter.WriteAsync("old");
            Assert.Equal("old", oldBox.Text);
            oldLease.BeginCleanup();
            TextBoxConsole.BeginCleanup(oldEnvironment);
            await TextBoxConsole.ShutdownAsync(oldEnvironment);
            oldLease.Dispose();

            var nextBox = new TextBox { IsReadOnly = true };
            using var nextLease = ExecutionEnvironmentManager.BeginExecution(2, CancellationToken.None);
            var nextEnvironment = ExecutionEnvironmentManager.GetCurrent(2);
            var nextScope = TextBoxConsole.Init(nextBox, nextEnvironment);
            try
            {
                await oldWriter.WriteAsync("stale");
                var staleReadError = await Task.Run(
                    () => Record.Exception(() => oldReader.Read()),
                    TestContext.Current.CancellationToken);

                Assert.IsType<ObjectDisposedException>(staleReadError);
                Assert.Equal("old", oldBox.Text);
                Assert.Equal(string.Empty, nextBox.Text);

                var nextRead = Task.Run(
                    () => TextBoxConsole.GetIn(nextScope).Read(),
                    TestContext.Current.CancellationToken);
                await WaitUntilAsync(() => !nextBox.IsReadOnly);
                Assert.True(SendText(nextBox, "Z").Handled);
                Assert.Equal('Z', (char)await nextRead.WaitAsync(
                    Timeout,
                    TestContext.Current.CancellationToken));
                await nextBox.Dispatcher.InvokeAsync(() => { });
                Assert.Equal("Z", nextBox.Text);
            }
            finally
            {
                nextLease.BeginCleanup();
                TextBoxConsole.BeginCleanup(nextEnvironment);
                await TextBoxConsole.ShutdownAsync(nextEnvironment);
            }
        });
    }

    [Fact]
    public async Task StaleShutdown_CannotReleaseCurrentScope()
    {
        await StaTest.RunAsync(async () =>
        {
            var oldLease = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            var oldEnvironment = ExecutionEnvironmentManager.GetCurrent(1);
            _ = TextBoxConsole.Init(new TextBox(), oldEnvironment);
            oldLease.BeginCleanup();
            TextBoxConsole.BeginCleanup(oldEnvironment);
            await TextBoxConsole.ShutdownAsync(oldEnvironment);
            oldLease.Dispose();

            using var currentLease = ExecutionEnvironmentManager.BeginExecution(2, CancellationToken.None);
            var currentEnvironment = ExecutionEnvironmentManager.GetCurrent(2);
            var currentScope = TextBoxConsole.Init(new TextBox(), currentEnvironment);
            try
            {
                await TextBoxConsole.ShutdownAsync(oldEnvironment);
                Assert.Same(currentScope, TextBoxConsole.CurrentScope);
            }
            finally
            {
                currentLease.BeginCleanup();
                TextBoxConsole.BeginCleanup(currentEnvironment);
                await TextBoxConsole.ShutdownAsync(currentEnvironment);
            }
        });
    }

    [Fact]
    public async Task ConflictingContextInit_CannotShutdownExistingOwner()
    {
        await StaTest.RunAsync(async () =>
        {
            var originalOut = global::System.Console.Out;
            var box = new TextBox();
            using var environment = ExecutionEnvironmentManager.BeginExecution(1, CancellationToken.None);
            await using var owner = new TextBoxConsoleContext(box);
            var conflicting = new TextBoxConsoleContext(new TextBox());
            owner.Init(1, CancellationToken.None);
            var ownerScope = TextBoxConsole.CurrentScope;

            Assert.Throws<InvalidOperationException>(() =>
                conflicting.Init(1, CancellationToken.None));
            await conflicting.DisposeAsync();

            Assert.Same(ownerScope, TextBoxConsole.CurrentScope);
            global::System.Console.Write("owner");
            Assert.Equal("owner", box.Text);

            environment.BeginCleanup();
            owner.BeginCleanup();
            await owner.DisposeAsync();
            Assert.Same(originalOut, global::System.Console.Out);
        });
    }

    [Fact]
    public void PublicApi_WithoutActiveExecution_HasDefinedBehavior()
    {
        TextBoxConsole.Write("ignored");
        TextBoxConsole.Clear();

        Assert.Equal(
            "No console execution is active.",
            Assert.Throws<InvalidOperationException>(() => TextBoxConsole.Read()).Message);
        Assert.Equal(
            "No console execution is active.",
            Assert.Throws<InvalidOperationException>(TextBoxConsole.ReadLine).Message);
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
}
