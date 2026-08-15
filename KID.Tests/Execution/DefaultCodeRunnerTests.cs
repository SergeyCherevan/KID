using KID.Services;
using KID.Services.CodeExecution;
using KID.Tests.TestDoubles;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class DefaultCodeRunnerTests
{
    [Fact]
    public async Task RunAsync_CompiledArtifact_UsesCollectibleContextAndSharedKidDependency()
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
        var runner = new DefaultCodeRunner(new StubLocalizationService());
        var execution = Assert.IsType<CollectibleCodeExecutionHandle>(
            runner.CreateExecution(artifact));

        try
        {
            await execution.RunAsync(TestContext.Current.CancellationToken);

            Assert.Equal(true, AppContext.GetData(collectibleSignalKey));
            Assert.Equal(true, AppContext.GetData(sharedDependencySignalKey));
            Assert.NotNull(execution.LoadContextReference);
            Assert.True(execution.LoadContextReference.IsAlive);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => execution.RunAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            execution.Dispose();
            execution.Dispose();
            AppContext.SetData(collectibleSignalKey, null);
            AppContext.SetData(sharedDependencySignalKey, null);
        }
    }
}
