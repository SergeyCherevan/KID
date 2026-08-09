using KID.Services;
using KID.Services.CodeExecution;
using KID.Tests.TestDoubles;

namespace KID.Tests.Execution;

public sealed class CodeExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CompilationFailure_DisposesContextAndSkipsRunner()
    {
        var compiler = FakeCodeCompiler.Returning(new CompilationResult { Success = false });
        var runner = new FakeCodeRunner();
        var context = new TrackingCodeExecutionContext();
        var service = new CodeExecutionService(compiler, runner);

        await service.ExecuteAsync("invalid code", context);

        Assert.Equal(1, context.InitCount);
        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_RunnerFailure_StillDisposesContext()
    {
        var compilationResult = new CompilationResult
        {
            Success = true,
            Assembly = typeof(CodeExecutionServiceTests).Assembly
        };
        var compiler = FakeCodeCompiler.Returning(compilationResult);
        var runner = new FakeCodeRunner((_, _) =>
            Task.FromException(new InvalidOperationException("runner failed")));
        var context = new TrackingCodeExecutionContext();
        var service = new CodeExecutionService(compiler, runner);

        var exception = await Record.ExceptionAsync(
            () => service.ExecuteAsync("valid code", context));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(1, context.DisposeCount);
        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhileRunning_DoesNotStartSecondCompilation()
    {
        var compilationStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCompilation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var compiler = new FakeCodeCompiler(async (_, cancellationToken) =>
        {
            compilationStarted.TrySetResult(true);
            await releaseCompilation.Task.WaitAsync(cancellationToken);
            return new CompilationResult { Success = false };
        });
        var service = new CodeExecutionService(compiler, new FakeCodeRunner());
        var firstContext = new TrackingCodeExecutionContext();
        var secondContext = new TrackingCodeExecutionContext();

        var firstExecution = service.ExecuteAsync("first", firstContext);
        await compilationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        try
        {
            await service.ExecuteAsync("second", secondContext)
                .WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            Assert.Equal(1, compiler.CallCount);
            Assert.Equal(0, secondContext.InitCount);
            Assert.Equal(0, secondContext.DisposeCount);
        }
        finally
        {
            releaseCompilation.TrySetResult(true);
            await firstExecution.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, firstContext.DisposeCount);
    }
}
