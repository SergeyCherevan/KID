using KID.Services;
using KID.Services.CodeExecution;
using KID.Tests.TestDoubles;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class DefaultCodeRunnerTests
{
    [Fact]
    public async Task RunAsync_CompiledArtifact_LoadsAndInvokesEntryPoint()
    {
        var signalKey = $"KID.Tests.DefaultCodeRunner.{Guid.NewGuid():N}";
        var code = $$"""
            public static class Program
            {
                public static void Main()
                {
                    System.AppContext.SetData("{{signalKey}}", true);
                }
            }
            """;
        var compiler = new CSharpCompiler(new StubLocalizationService());
        var compilationResult = await compiler.CompileAsync(
            code,
            TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(compilationResult.Artifact);
        var runner = new DefaultCodeRunner(new StubLocalizationService());

        try
        {
            await runner.RunAsync(
                artifact,
                TestContext.Current.CancellationToken);

            Assert.Equal(true, AppContext.GetData(signalKey));
        }
        finally
        {
            AppContext.SetData(signalKey, null);
        }
    }
}
