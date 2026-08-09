using KID.Tests.Infrastructure;

namespace KID.Tests.Lifecycle;

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

    [Fact(Skip = KnownIssueReasons.CollectibleAssembly)]
    public void RepeatedRuns_ReleaseCollectibleAssemblyLoadContexts()
    {
        Assert.Fail("CSharpCompiler currently loads user assemblies into the default context.");
    }
}
