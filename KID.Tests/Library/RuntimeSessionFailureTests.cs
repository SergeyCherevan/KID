using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using KID.Services;
using KID.Services.CodeExecution;
using KID.Tests.TestDoubles;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class RuntimeSessionFailureTests
{
    private const string FaultCaseVariable = "KID_RUNTIME_FAILURE_CASE";

    [Fact]
    public async Task FailureCases_BlockAdmissionInIsolatedProcesses()
    {
        var faultCase = Environment.GetEnvironmentVariable(FaultCaseVariable);
        if (faultCase != null)
        {
            await VerifyFailureAsync(faultCase);
            return;
        }

        // Admission has no reset by design. Each failure gets its own process, including under CI.
        foreach (var scenario in new[] { "Cleanup", "Rollback", "CanceledRollback", "Host", "StopCallback" })
        {
            var start = new ProcessStartInfo(Path.ChangeExtension(typeof(RuntimeSessionFailureTests).Assembly.Location, ".exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("-method");
            start.ArgumentList.Add($"{typeof(RuntimeSessionFailureTests).FullName}.{nameof(FailureCases_BlockAdmissionInIsolatedProcesses)}");
            start.ArgumentList.Add("-noLogo");
            start.ArgumentList.Add("-noColor");
            start.ArgumentList.Add("-noAutoReporters");
            start.Environment[FaultCaseVariable] = scenario;
            using var process = Process.Start(start)!;
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(
                    TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
                Assert.True(process.ExitCode == 0, $"{scenario}: {await output}\n{await error}");
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
        }
    }

    private static async Task VerifyFailureAsync(string scenario)
    {
        await StaTest.RunAsync(async () =>
        {
            var originalOut = System.Console.Out;
            var originalIn = System.Console.In;
            var originalError = System.Console.Error;
            var canvas = new Canvas();
            var options = new KIDRuntimeOptions
            {
                Canvas = canvas,
                MouseCanvas = canvas,
                KeyboardWindow = new Window(),
                EnableMusic = true,
                ConsoleTextBox = new TextBox(),
                RedirectSystemConsole = true
            };
            var cleanupFailure = new InvalidOperationException("cleanup failure");
            Exception? observed;
            if (scenario is "Rollback" or "CanceledRollback")
            {
                using var source = new CancellationTokenSource();
                var openingFailure = new ArgumentException("opening failure");
                observed = await Record.ExceptionAsync(async () => await RuntimeSessionOwner.OpenAsync(
                    options, source.Token, (output, _, _) =>
                    {
                        System.Console.SetOut(output);
                        // Simulate an audio resource teardown fault after a partial stream redirect.
                        Music.CurrentScope!.RecordFailure(cleanupFailure);
                        if (scenario == "CanceledRollback") CancelSynchronously(source);
                        else throw openingFailure;
                    }));
                var errors = Assert.IsType<AggregateException>(observed).Flatten().InnerExceptions;
                Assert.Contains(cleanupFailure, errors);
                if (scenario == "Rollback") Assert.Contains(openingFailure, errors);
                else Assert.Contains(errors, exception => exception is OperationCanceledException);
            }
            else if (scenario == "Host")
            {
                var owner = await RuntimeSessionOwner.OpenAsync(options, TestContext.Current.CancellationToken);
                await owner.CleanupAsync();
                observed = await Record.ExceptionAsync(async () => await owner.CompleteAsync(cleanupFailure));
                Assert.Same(cleanupFailure, observed);
            }
            else
            {
                var session = await KIDRuntimeSession.OpenAsync(options, TestContext.Current.CancellationToken);
                using var registration = scenario == "StopCallback"
                    ? session.CancellationToken.Register(() => throw cleanupFailure)
                    : default;
                if (scenario == "StopCallback") session.RequestStop();
                else
                {
                    var producer = Task.Run(() => DispatcherManager.InvokeOnUI((Action)(() => throw cleanupFailure)),
                        TestContext.Current.CancellationToken);
                    Assert.True(SpinWait.SpinUntil(() => producer.IsCompleted, TimeSpan.FromSeconds(5)));
                    await producer; // Already completed; the queued UI action has not run yet.
                }
                var first = session.DisposeAsync().AsTask();
                Assert.Same(first, session.DisposeAsync().AsTask());
                observed = await Record.ExceptionAsync(async () => await session.DisposeAsync());
                Assert.NotNull(observed);
                if (scenario == "StopCallback")
                    Assert.Contains(cleanupFailure, Assert.IsType<AggregateException>(observed).Flatten().InnerExceptions);
                else Assert.Same(cleanupFailure, observed);
                session.RequestStop();
                Assert.Same(observed, await Record.ExceptionAsync(async () => await session.DisposeAsync()));
            }

            Assert.Same(originalOut, System.Console.Out);
            Assert.Same(originalIn, System.Console.In);
            Assert.Same(originalError, System.Console.Error);
            Assert.Null(ExecutionEnvironmentManager.Current);
            Assert.Null(Graphics.Canvas);
            Assert.Null(Mouse.CurrentScope);
            Assert.Null(Keyboard.CurrentScope);
            Assert.Null(Music.CurrentScope);
            Assert.Null(TextBoxConsole.CurrentScope);

            var admissionError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions(), TestContext.Current.CancellationToken));
            Assert.NotNull(admissionError.InnerException);
            var service = new CodeExecutionService(
                FakeCodeCompiler.Returning(CompilationResult.FromErrors([])), new FakeCodeRunner());
            var ideAdmissionError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ExecuteAsync("script", _ => throw new InvalidOperationException("Factory must not run.")));
            Assert.Same(admissionError.InnerException, ideAdmissionError.InnerException);
        });
    }

    private static void CancelSynchronously(CancellationTokenSource source) => source.Cancel();

}
