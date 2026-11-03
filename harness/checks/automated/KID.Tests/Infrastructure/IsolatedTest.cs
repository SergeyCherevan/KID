using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace KID.Tests.Infrastructure;

internal static class IsolatedTest
{
    // Failed runtime cleanup permanently closes admission. Exercise it without resetting production state.
    internal static async Task<bool> RunAsync(Type testClass, [CallerMemberName] string method = "")
    {
        var target = $"{testClass.FullName}.{method}";
        const string variable = "KID_ISOLATED_TEST";
        if (Environment.GetEnvironmentVariable(variable) == target) return false;
        var start = new ProcessStartInfo(Path.ChangeExtension(testClass.Assembly.Location, ".exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-method", target, "-noLogo", "-noColor", "-noAutoReporters" })
            start.ArgumentList.Add(argument);
        start.Environment[variable] = target;
        using var process = Process.Start(start)!;
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(
                TimeSpan.FromSeconds(45), TestContext.Current.CancellationToken);
            Assert.True(process.ExitCode == 0, $"{target}: {await output}\n{await error}");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        return true;
    }
}
