using System.Globalization;
using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Runtime;
using KID.Services.CodeExecution.Runtime.Interfaces;
using KID.Tests.TestDoubles;

namespace KID.Tests.Execution;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class CodeRunnerCultureTests
{
    [Theory]
    [InlineData("en", "en-US", false)]
    [InlineData("ru", "ru-RU", false)]
    [InlineData("uk", "uk-UA", false)]
    [InlineData("en", "en-US", true)]
    [InlineData("ru", "ru-RU", true)]
    [InlineData("uk", "uk-UA", true)]
    public async Task Start_UsesSelectedLanguageForEntryPointAndInheritedWork(
        string selected, string formatting, bool isAsync)
    {
        var resultKey = $"KID.Tests.RunCulture.{Guid.NewGuid():N}";
        var observed = new string[9];
        var declaration = isAsync ? "async System.Threading.Tasks.Task" : "void";
        var yield = isAsync ? "await System.Threading.Tasks.Task.Yield();" : string.Empty;
        var code = $$"""
            public static class Program
            {
                public static {{declaration}} Main()
                {
                    var result = (string[])System.AppContext.GetData("{{resultKey}}")!;
                    result[0] = System.Globalization.CultureInfo.CurrentUICulture.Name;
                    result[1] = System.Globalization.CultureInfo.CurrentCulture.Name;
                    result[2] = (1234.5m).ToString();
                    {{yield}}
                    result[3] = System.Globalization.CultureInfo.CurrentUICulture.Name;
                    result[4] = System.Globalization.CultureInfo.CurrentCulture.Name;
                    var inherited = System.Threading.Tasks.Task.Run(() =>
                        System.Globalization.CultureInfo.CurrentUICulture.Name + "|" +
                        System.Globalization.CultureInfo.CurrentCulture.Name).GetAwaiter().GetResult();
                    result[5] = inherited;
                    var thread = new System.Threading.Thread(() =>
                        result[6] = System.Globalization.CultureInfo.CurrentUICulture.Name + "|" +
                            System.Globalization.CultureInfo.CurrentCulture.Name);
                    thread.Start();
                    thread.Join();
                    System.Globalization.CultureInfo.CurrentCulture =
                        System.Globalization.CultureInfo.GetCultureInfo("ja-JP");
                    System.Globalization.CultureInfo.CurrentUICulture =
                        System.Globalization.CultureInfo.GetCultureInfo("ja-JP");
                    result[7] = System.Globalization.CultureInfo.CurrentCulture.Name;
                    result[8] = System.Globalization.CultureInfo.CurrentUICulture.Name;
                }
            }
            """;
        var localization = new StubLocalizationService { CurrentCulture = selected };
        var compiler = CompilerFactory.Create(localization);
        var compiled = await compiler.CompileAsync(code, TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(compiled.Artifact);
        var runner = new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory);
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var globalCulture = CultureInfo.DefaultThreadCurrentCulture;
        var globalUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        AppContext.SetData(resultKey, observed);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            using var execution = runner.Start(artifact, TestContext.Current.CancellationToken);
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
            Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name);
            var outcome = await execution.Completion.Task.WaitAsync(
                TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.Equal(ExecutionResultKind.Completed, outcome.Kind);
            Assert.Equal(selected, observed[0]);
            Assert.Equal(formatting, observed[1]);
            Assert.Equal(1234.5m.ToString(CultureInfo.GetCultureInfo(formatting)), observed[2]);
            Assert.Equal(selected, observed[3]);
            Assert.Equal(formatting, observed[4]);
            Assert.Equal($"{selected}|{formatting}", observed[5]);
            Assert.Equal($"{selected}|{formatting}", observed[6]);
            Assert.Equal("ja-JP", observed[7]);
            Assert.Equal("ja-JP", observed[8]);
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
            Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name);
            Assert.Same(globalCulture, CultureInfo.DefaultThreadCurrentCulture);
            Assert.Same(globalUiCulture, CultureInfo.DefaultThreadCurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            AppContext.SetData(resultKey, null);
        }
    }

    [Fact]
    public async Task Start_LanguageChangeDuringRunAppliesOnlyToNextRun()
    {
        var resultKey = $"KID.Tests.RunCulture.Snapshot.{Guid.NewGuid():N}";
        var startedKey = resultKey + ".Started";
        var releaseKey = resultKey + ".Release";
        var observed = new string[2];
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var code = $$"""
            public static class Program
            {
                public static async System.Threading.Tasks.Task Main()
                {
                    var result = (string[])System.AppContext.GetData("{{resultKey}}")!;
                    result[0] = System.Globalization.CultureInfo.CurrentUICulture.Name;
                    ((System.Threading.Tasks.TaskCompletionSource)System.AppContext.GetData("{{startedKey}}")!).SetResult();
                    await ((System.Threading.Tasks.TaskCompletionSource)System.AppContext.GetData("{{releaseKey}}")!).Task;
                    result[1] = System.Globalization.CultureInfo.CurrentUICulture.Name;
                }
            }
            """;
        var localization = new StubLocalizationService { CurrentCulture = "ru" };
        var compiler = CompilerFactory.Create(localization);
        var compiled = await compiler.CompileAsync(code, TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(compiled.Artifact);
        var runner = new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory);
        ICodeRunningInstance? execution = null;
        AppContext.SetData(resultKey, observed);
        AppContext.SetData(startedKey, started);
        AppContext.SetData(releaseKey, release);
        try
        {
            execution = runner.Start(artifact, TestContext.Current.CancellationToken);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            localization.CurrentCulture = "uk";
            release.SetResult();
            var first = await execution.Completion.Task.WaitAsync(
                TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(ExecutionResultKind.Completed, first.Kind);
            Assert.Equal(["ru", "ru"], observed);
            execution.Dispose();
            execution = null;

            started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            AppContext.SetData(startedKey, started);
            execution = runner.Start(artifact, TestContext.Current.CancellationToken);
            var second = await execution.Completion.Task.WaitAsync(
                TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(ExecutionResultKind.Completed, second.Kind);
            Assert.Equal(["uk", "uk"], observed);
        }
        finally
        {
            release.TrySetResult();
            if (execution != null)
            {
                await execution.Completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                execution.Dispose();
            }
            AppContext.SetData(resultKey, null);
            AppContext.SetData(startedKey, null);
            AppContext.SetData(releaseKey, null);
        }
    }

    [Fact]
    public async Task Start_UserFaultAfterChangingCultureLeavesCallerUnchanged()
    {
        const string code = """
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ja-JP");
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ja-JP");
            throw new System.InvalidOperationException("user failed");
            """;
        var localization = new StubLocalizationService { CurrentCulture = "ru" };
        var compiled = await CompilerFactory.Create(localization).CompileAsync(code, TestContext.Current.CancellationToken);
        var artifact = Assert.IsType<CompilationArtifact>(compiled.Artifact);
        var runner = new DefaultCodeRunner(localization, TestThreading.JoinableTaskFactory);
        var beforeCulture = CultureInfo.CurrentCulture;
        var beforeUiCulture = CultureInfo.CurrentUICulture;
        using var execution = runner.Start(artifact, TestContext.Current.CancellationToken);

        var outcome = await execution.Completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionResultKind.RuntimeFaulted, outcome.Kind);
        Assert.Equal("user failed", outcome.ErrorMessage);
        Assert.Same(beforeCulture, CultureInfo.CurrentCulture);
        Assert.Same(beforeUiCulture, CultureInfo.CurrentUICulture);
    }
}
