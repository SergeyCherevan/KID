// Isolated process fixture for Verify-Harness.ps1; never used by product checks.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

if (args.FirstOrDefault() == "--cancel-console")
{
    Native.FreeConsole();
    if (!Native.AttachConsole(uint.Parse(args[1]))) return 10;
    Native.SetConsoleCtrlHandler(IntPtr.Zero, true);
    if (!Native.GenerateConsoleCtrlEvent(0, 0)) return 11;
    Thread.Sleep(500);
    Native.FreeConsole();
    return 0;
}
if (args.FirstOrDefault() == "--wait-child") { Thread.Sleep(60000); return 0; }
if (args.SequenceEqual(new[] { "--version" })) { Console.WriteLine("10.0.402-fixture"); return 0; }
Console.WriteLine("FIXTURE " + JsonSerializer.Serialize(args));
var behavior = Environment.GetEnvironmentVariable("KID_HARNESS_FIXTURE_BEHAVIOR");
if (args.FirstOrDefault() == "build")
{
    if (behavior == "fail") { Console.Error.WriteLine("Intentional fixture failure: 37"); return 37; }
    if (behavior == "sleep")
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--wait-child");
        using var child = Process.Start(start)!;
        File.WriteAllText(Environment.GetEnvironmentVariable("KID_HARNESS_FIXTURE_PIDS")!, $"{Environment.ProcessId},{child.Id}");
        Thread.Sleep(60000);
    }
}
if (args.FirstOrDefault() == "test" && behavior != "no-trx")
{
    var folder = args[Array.IndexOf(args, "--results-directory") + 1];
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "KID.Tests.trx"),
        behavior == "empty-trx" ? "<TestRun><ResultSummary><Counters executed=\"0\" /></ResultSummary></TestRun>" :
        "<TestRun><ResultSummary><Counters executed=\"1\" /></ResultSummary></TestRun>");
}
return 0;

static class Native
{
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] public static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GenerateConsoleCtrlEvent(uint controlEvent, uint group);
}
