using System.Runtime.CompilerServices;
using KID.Services;
using KID.Services.CodeExecution;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using KID.Tests.TestDoubles;

namespace KID.Tests.Lifecycle;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class ExecutionLifecycleSpecifications
{
    [Theory]
    [InlineData("Task")]
    [InlineData("Task<int>")]
    public async Task Runner_AwaitsAsyncEntryPointBeforeCleanup(string returnType)
    {
        var signalKey = $"KID.Tests.Lifecycle.AsyncMain.{Guid.NewGuid():N}";
        var startedKey = $"{signalKey}.Started";
        var releaseKey = $"{signalKey}.Release";
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var localizationService = new StubLocalizationService();
        var compiler = new CSharpCompiler(localizationService);
        var runner = new DefaultCodeRunner(
            localizationService,
            TestThreading.JoinableTaskFactory);
        var service = new CodeExecutionService(compiler, runner);
        var context = new TrackingCodeExecutionContext();
        var code = CreateAsyncMainSource(
            returnType,
            signalKey,
            startedKey,
            releaseKey);

        AppContext.SetData(startedKey, started);
        AppContext.SetData(releaseKey, release);
        var execution = service.ExecuteAsync(code, _ => context);

        try
        {
            await started.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(ExecutionState.Running, service.State);
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, context.DisposeCount);
            Assert.Null(AppContext.GetData(signalKey));

            release.TrySetResult(true);
            await execution.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(true, AppContext.GetData(signalKey));
            Assert.Equal(1, context.DisposeCount);
            Assert.Equal(ExecutionState.Idle, service.State);
        }
        finally
        {
            release.TrySetResult(true);
            if (!execution.IsCompleted)
            {
                await execution.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            }

            AppContext.SetData(signalKey, null);
            AppContext.SetData(startedKey, null);
            AppContext.SetData(releaseKey, null);
        }
    }

    [Theory(Skip = KnownIssueReasons.RuntimeCleanup)]
    [InlineData("Keyboard and Mouse handlers")]
    [InlineData("active audio resources")]
    [InlineData("queued Dispatcher operations")]
    public void Cleanup_PreventsPreviousRunResourcesFromAffectingNextRun(string resource)
    {
        Assert.Fail($"Deterministic cleanup is not implemented for {resource}.");
    }

    [Theory]
    [InlineData("void")]
    [InlineData("Task<int>")]
    public async Task RepeatedRuns_ReleaseCollectibleAssemblyLoadContexts(string returnType)
    {
        var code = returnType switch
        {
            "void" => """
                public static class Program
                {
                    public static void Main()
                    {
                    }
                }
                """,
            "Task<int>" => """
                public static class Program
                {
                    public static async System.Threading.Tasks.Task<int> Main()
                    {
                        await System.Threading.Tasks.Task.Yield();
                        return 0;
                    }
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(returnType))
        };
        var localizationService = new StubLocalizationService();
        var compiler = new CSharpCompiler(localizationService);
        var compilationResult = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(compilationResult.Artifact);
        var runner = new DefaultCodeRunner(
            localizationService,
            TestThreading.JoinableTaskFactory);
        var contextReferences = new List<WeakReference>();

        for (var executionIndex = 0; executionIndex < 8; executionIndex++)
        {
            contextReferences.Add(await ExecuteAndDisposeAsync(runner, artifact));
        }

        /* Unload является кооперативным: bounded GC loop доказывает не только вызов Dispose,
         * но и отсутствие сильных host-ссылок, мешающих собрать все восемь ALC.
         */
        for (var attempt = 0;
             attempt < 10 && contextReferences.Any(reference => reference.IsAlive);
             attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.All(contextReferences, reference => Assert.False(reference.IsAlive));
    }

    private static string CreateAsyncMainSource(
        string returnType,
        string signalKey,
        string startedKey,
        string releaseKey)
    {
        var returnDeclaration = returnType switch
        {
            "Task" => "System.Threading.Tasks.Task",
            "Task<int>" => "System.Threading.Tasks.Task<int>",
            _ => throw new ArgumentOutOfRangeException(nameof(returnType))
        };
        var returnStatement = returnType == "Task<int>" ? "return 23;" : string.Empty;

        return $$"""
            public static class Program
            {
                public static async {{returnDeclaration}} Main()
                {
                    var started = (System.Threading.Tasks.TaskCompletionSource<bool>)
                        System.AppContext.GetData("{{startedKey}}")!;
                    started.TrySetResult(true);
                    var release = (System.Threading.Tasks.TaskCompletionSource<bool>)
                        System.AppContext.GetData("{{releaseKey}}")!;
                    await release.Task;
                    System.AppContext.SetData("{{signalKey}}", true);
                    {{returnStatement}}
                }
            }
            """;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ExecuteAndDisposeAsync(
        DefaultCodeRunner runner,
        CompilationArtifact artifact)
    {
        /* Отдельный non-inlined frame следует рекомендуемому .NET шаблону unload-теста:
         * после возврата здесь на стеке не остаются экземпляр выполнения, Assembly или MethodInfo.
         */
        var execution = Assert.IsType<CollectibleCodeRunningInstance>(
            runner.Start(artifact, TestContext.Current.CancellationToken));
        try
        {
            await execution.Completion;
            return Assert.IsType<WeakReference>(execution.LoadContextReference);
        }
        finally
        {
            execution.Dispose();
        }
    }
}
