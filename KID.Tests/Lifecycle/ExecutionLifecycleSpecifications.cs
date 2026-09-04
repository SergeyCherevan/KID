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
    [Theory(Skip = KnownIssueReasons.AsyncEntryPoint)]
    [InlineData("Task")]
    [InlineData("Task<int>")]
    public void Runner_AwaitsAsyncEntryPointBeforeCleanup(string returnType)
    {
        Assert.Fail($"Async entry point is not awaited for return type {returnType}.");
    }

    [Theory(Skip = KnownIssueReasons.RuntimeCleanup)]
    [InlineData("Keyboard and Mouse handlers")]
    [InlineData("active audio resources")]
    [InlineData("queued Dispatcher operations")]
    public void Cleanup_PreventsPreviousRunResourcesFromAffectingNextRun(string resource)
    {
        Assert.Fail($"Deterministic cleanup is not implemented for {resource}.");
    }

    [Fact]
    public async Task RepeatedRuns_ReleaseCollectibleAssemblyLoadContexts()
    {
        const string code = """
            public static class Program
            {
                public static void Main()
                {
                }
            }
            """;
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
