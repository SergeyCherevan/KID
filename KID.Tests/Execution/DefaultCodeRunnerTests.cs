using KID.Services;
using KID.Services.CodeExecution;
using KID.Tests.TestDoubles;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class DefaultCodeRunnerTests
{
    [Fact]
    public async Task Start_ReturnsBeforeCompletion_AndRepeatedAwaitDoesNotRunAgain()
    {
        var signalKey = $"KID.Tests.DefaultCodeRunner.Start.{Guid.NewGuid():N}";
        var releaseKey = $"{signalKey}.Release";
        var countKey = $"{signalKey}.Count";
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var runCount = new int[1];
        var code = $$"""
            public static class Program
            {
                public static void Main()
                {
                    var count = (int[])System.AppContext.GetData("{{countKey}}")!;
                    System.Threading.Interlocked.Increment(ref count[0]);
                    var started = (System.Threading.Tasks.TaskCompletionSource<bool>)
                        System.AppContext.GetData("{{signalKey}}")!;
                    started.SetResult(true);
                    var release = (System.Threading.ManualResetEventSlim)
                        System.AppContext.GetData("{{releaseKey}}")!;
                    if (!release.Wait(System.TimeSpan.FromSeconds(10)))
                        throw new System.TimeoutException("The test did not release the program.");
                }
            }
            """;
        var compiler = new CSharpCompiler(new StubLocalizationService());
        var result = await compiler.CompileAsync(code, TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(result.Artifact);
        var runner = new DefaultCodeRunner(
            new StubLocalizationService(),
            TestThreading.JoinableTaskFactory);
        AppContext.SetData(signalKey, started);
        AppContext.SetData(releaseKey, release);
        AppContext.SetData(countKey, runCount);
        var execution = runner.Start(artifact, TestContext.Current.CancellationToken);

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var completion = execution.Completion;
            Assert.False(completion.IsCompleted);
            Assert.Same(completion, execution.Completion);
            Assert.Throws<InvalidOperationException>(() => execution.Dispose());

            release.Set();
            await completion.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            await execution.Completion;
            Assert.Equal(1, runCount[0]);
        }
        finally
        {
            release.Set();
            try
            {
                await execution.Completion.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            }
            finally
            {
                execution.Dispose();
                AppContext.SetData(signalKey, null);
                AppContext.SetData(releaseKey, null);
                AppContext.SetData(countKey, null);
            }
        }
    }

    [Fact]
    public async Task Start_InvalidArtifact_ReturnsInstanceWithFaultedCompletion()
    {
        var runner = new DefaultCodeRunner(
            new StubLocalizationService(),
            TestThreading.JoinableTaskFactory);
        using var execution = Assert.IsType<CollectibleCodeRunningInstance>(
            runner.Start(new CompilationArtifact(new byte[] { 1 }), TestContext.Current.CancellationToken));
        var completion = execution.Completion;

        await Assert.ThrowsAsync<BadImageFormatException>(() =>
            completion.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.True(completion.Task.IsFaulted);
        Assert.Same(completion, execution.Completion);
        Assert.NotNull(execution.LoadContextReference);
        Assert.True(execution.LoadContextReference.IsAlive);
    }

    [Fact]
    public async Task Start_AlreadyCanceledToken_ReturnsInstanceWithCanceledCompletion()
    {
        var runner = new DefaultCodeRunner(
            new StubLocalizationService(),
            TestThreading.JoinableTaskFactory);
        using var execution = Assert.IsType<CollectibleCodeRunningInstance>(
            runner.Start(new CompilationArtifact(new byte[] { 1 }), new CancellationToken(canceled: true)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            execution.Completion.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));

        Assert.True(execution.Completion.Task.IsCanceled);
        Assert.Null(execution.LoadContextReference);
    }

    [Fact]
    public async Task Start_CompiledArtifact_UsesCollectibleContextAndSharedKidDependency()
    {
        var collectibleSignalKey = $"KID.Tests.DefaultCodeRunner.Collectible.{Guid.NewGuid():N}";
        var sharedDependencySignalKey = $"KID.Tests.DefaultCodeRunner.Shared.{Guid.NewGuid():N}";
        var code = $$"""
            public static class Program
            {
                public static void Main()
                {
                    var ownContext = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(
                        typeof(Program).Assembly);
                    var kidContext = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(
                        typeof(KID.StopManager).Assembly);

                    System.AppContext.SetData(
                        "{{collectibleSignalKey}}",
                        ownContext is { IsCollectible: true } &&
                        !object.ReferenceEquals(
                            ownContext,
                            System.Runtime.Loader.AssemblyLoadContext.Default));
                    System.AppContext.SetData(
                        "{{sharedDependencySignalKey}}",
                        object.ReferenceEquals(
                            kidContext,
                            System.Runtime.Loader.AssemblyLoadContext.Default));
                }
            }
            """;
        var compiler = new CSharpCompiler(new StubLocalizationService());
        var compilationResult = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(compilationResult.Artifact);
        var runner = new DefaultCodeRunner(
            new StubLocalizationService(),
            TestThreading.JoinableTaskFactory);
        var execution = Assert.IsType<CollectibleCodeRunningInstance>(
            runner.Start(artifact, TestContext.Current.CancellationToken));

        try
        {
            var completion = execution.Completion;
            await completion;

            Assert.Equal(true, AppContext.GetData(collectibleSignalKey));
            Assert.Equal(true, AppContext.GetData(sharedDependencySignalKey));
            Assert.NotNull(execution.LoadContextReference);
            Assert.True(execution.LoadContextReference.IsAlive);
            Assert.Same(completion, execution.Completion);
            await execution.Completion;
        }
        finally
        {
            execution.Dispose();
            execution.Dispose();
            AppContext.SetData(collectibleSignalKey, null);
            AppContext.SetData(sharedDependencySignalKey, null);
        }
    }

    [Theory]
    [InlineData("void", false)]
    [InlineData("void", true)]
    [InlineData("int", false)]
    [InlineData("int", true)]
    [InlineData("Task", false)]
    [InlineData("Task", true)]
    [InlineData("Task<int>", false)]
    [InlineData("Task<int>", true)]
    public async Task Start_SupportedMainSignature_CompletesAfterEntryPoint(
        string returnType,
        bool acceptsArguments)
    {
        var signalKey = $"KID.Tests.DefaultCodeRunner.Signature.{Guid.NewGuid():N}";
        var startedKey = $"{signalKey}.Started";
        var releaseKey = $"{signalKey}.Release";
        var isAsync = returnType.StartsWith("Task", StringComparison.Ordinal);
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var code = CreateMainSource(
            returnType,
            acceptsArguments,
            signalKey,
            startedKey,
            releaseKey);
        var localizationService = new StubLocalizationService();
        var compiler = new CSharpCompiler(localizationService);
        var compilationResult = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(compilationResult.Artifact);
        var runner = new DefaultCodeRunner(
            localizationService,
            TestThreading.JoinableTaskFactory);

        AppContext.SetData(startedKey, started);
        AppContext.SetData(releaseKey, release);
        var execution = runner.Start(artifact, TestContext.Current.CancellationToken);

        try
        {
            if (isAsync)
            {
                await started.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

                Assert.False(execution.Completion.IsCompleted);
                Assert.Null(AppContext.GetData(signalKey));
                release.TrySetResult(true);
            }

            await execution.Completion.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(true, AppContext.GetData(signalKey));
        }
        finally
        {
            release.TrySetResult(true);
            try
            {
                await execution.Completion.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            }
            finally
            {
                execution.Dispose();
                AppContext.SetData(signalKey, null);
                AppContext.SetData(startedKey, null);
                AppContext.SetData(releaseKey, null);
            }
        }
    }

    private static string CreateMainSource(
        string returnType,
        bool acceptsArguments,
        string signalKey,
        string startedKey,
        string releaseKey)
    {
        var returnDeclaration = returnType switch
        {
            "void" => "void",
            "int" => "int",
            "Task" => "System.Threading.Tasks.Task",
            "Task<int>" => "System.Threading.Tasks.Task<int>",
            _ => throw new ArgumentOutOfRangeException(nameof(returnType))
        };
        var asyncModifier = returnType.StartsWith("Task", StringComparison.Ordinal)
            ? "async "
            : string.Empty;
        var parameters = acceptsArguments ? "string[] args" : string.Empty;
        var argumentCheck = acceptsArguments ? "args.Length == 0" : "true";
        var asynchronousWait = returnType.StartsWith("Task", StringComparison.Ordinal)
            ? $$"""
                var started = (System.Threading.Tasks.TaskCompletionSource<bool>)
                    System.AppContext.GetData("{{startedKey}}")!;
                started.TrySetResult(true);
                var release = (System.Threading.Tasks.TaskCompletionSource<bool>)
                    System.AppContext.GetData("{{releaseKey}}")!;
                await release.Task;
                """
            : string.Empty;
        var returnStatement = returnType is "int" or "Task<int>"
            ? "return 17;"
            : string.Empty;

        return $$"""
            public static class Program
            {
                public static {{asyncModifier}}{{returnDeclaration}} Main({{parameters}})
                {
                    {{asynchronousWait}}
                    System.AppContext.SetData("{{signalKey}}", {{argumentCheck}});
                    {{returnStatement}}
                }
            }
            """;
    }
}
