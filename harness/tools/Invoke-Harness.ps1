# Run with PowerShell 7+. Parameters are parsed here so usage errors consistently exit 2.
# No shell command strings are evaluated; every child receives an ArgumentList.
$ErrorActionPreference = 'Stop'
$options = @{
    Command = 'help'; Configuration = 'Release'; NoRestore = $false
    Mode = 'All'; ConsoleMode = 'Both'; Profile = 'Headless'; Scenario = 'All'
    TimeoutSeconds = 600; Help = $false
}
$specified = @{}
$choices = @{
    Command = @('help', 'doctor', 'build', 'test', 'probe', 'demo', 'check', 'manual')
    Configuration = @('Release', 'Debug')
    Mode = @('All', 'Regression', 'Session', 'SessionFailure', 'CanvasSwitching', 'CanvasObjects')
    ConsoleMode = @('Both', 'Without', 'With'); Profile = @('Headless', 'Full')
    Scenario = @('All', 'standalone-library', 'music-download', 'first-launch-language', 'persistence-recovery', 'editor-session-recovery')
}
try {
    for ($i = 0; $i -lt $args.Count; $i++) {
        $token = [string]$args[$i]
        if ($token -notmatch '^-(\w+)(?::(.*))?$' -or -not $options.ContainsKey($Matches[1])) {
            throw "Unknown parameter: $token"
        }
        $name = $Matches[1]
        $inline = $Matches[2]
        if ($specified.ContainsKey($name)) { throw "Duplicate parameter: -$name" }
        $specified[$name] = $true
        if ($name -in @('Help', 'NoRestore')) {
            if ($inline) {
                if ($inline -notin @('true', 'false', '$true', '$false')) { throw "Invalid switch: $token" }
                $options[$name] = $inline -in @('true', '$true')
            }
            else { $options[$name] = $true }
        }
        else {
            if ($token.Contains(':')) { $options[$name] = $inline }
            elseif (++$i -lt $args.Count) { $options[$name] = [string]$args[$i] }
            else { throw "Missing value for -$name" }
        }
    }
    foreach ($name in $choices.Keys) {
        $canonical = @($choices[$name] | Where-Object { $_ -eq $options[$name] })
        if ($canonical.Count -ne 1) { throw "Invalid -${name}: $($options[$name]). Expected: $($choices[$name] -join ', ')." }
        $options[$name] = $canonical[0]
    }
    $timeout = 0
    if (-not [int]::TryParse([string]$options.TimeoutSeconds, [ref]$timeout) -or $timeout -lt 1 -or $timeout -gt 86400) {
        throw '-TimeoutSeconds must be an integer from 1 to 86400.'
    }
    $options.TimeoutSeconds = $timeout
    $command = $options.Command
    $allowed = @('Command', 'Help')
    if ($command -in @('build', 'test', 'probe', 'demo', 'check')) {
        $allowed += @('Configuration', 'NoRestore', 'TimeoutSeconds')
    }
    $allowed += switch ($command) { probe { 'Mode' }; demo { 'ConsoleMode' }; check { 'Profile' }; manual { 'Scenario' } }
    foreach ($name in $specified.Keys) {
        if ($name -notin $allowed) { throw "-$name is not valid for $command." }
    }
    if ($specified.ContainsKey('Help') -and ($command -ne 'help' -or @($specified.Keys | Where-Object { $_ -notin @('Command', 'Help') }).Count)) {
        throw '-Help cannot be combined with working parameters.'
    }
}
catch { [Console]::Error.WriteLine("USAGE: $($_.Exception.Message)"); exit 2 }

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$solution = Join-Path $repoRoot 'KID.sln'
$probe = Join-Path $repoRoot 'harness/checks/probes/KID.Library.StandaloneProbe/KID.Library.StandaloneProbe.csproj'
$demo = Join-Path $repoRoot 'harness/checks/probes/KID.Library.StandaloneDemo/KID.Library.StandaloneDemo.csproj'
$tests = Join-Path $repoRoot 'harness/checks/automated/KID.Tests/KID.Tests.csproj'
$manualRoot = Join-Path $repoRoot 'harness/checks/manual'

if ($command -eq 'help') {
    Write-Output @'
INFO KID harness (PowerShell 7+, Windows, SDK selected by root global.json)
Usage: ./harness/tools/Invoke-Harness.ps1 [-Command <command>] [parameters]
  help    Show this help (default); -Help is also supported. No dotnet required.
  doctor  Inspect PowerShell/Windows, selected SDK, solution, check projects and manual READMEs.
  build   Restore and build the solution.
  test    Restore/build the solution, then run all tests without rebuilding/restoring.
  probe   Restore the external probe; rebuild/run each selected mode in a separate process.
  demo    Restore/build demo; open real windows and wait for verification to finish.
  check   Doctor, solution build, tests and all five probe modes; Full also runs both demos.
  manual  Show scenario README(s) and preparation/restoration commands; does not execute them.
Parameters (case insensitive):
  -Configuration Release|Debug                         default Release; build/test/probe/demo/check
  -NoRestore                                           same commands; never falls back to restore
  -TimeoutSeconds 1..86400                              same commands; per process, default 600
  -Mode All|Regression|Session|SessionFailure|CanvasSwitching|CanvasObjects
                                                       probe only, default All (listed order)
  -ConsoleMode Both|Without|With                        demo only, default Both
  -Profile Headless|Full                               check only, default Headless
  -Scenario All|standalone-library|music-download|first-launch-language|
            persistence-recovery|editor-session-recovery   manual only, default All
Working runs are sequential, stop on failure, and lock artifacts/harness/runner.lock.
Logs, test TRX and summary.json: artifacts/harness/<UTC timestamp>-<run id>/.
Full requires a desktop and opens two windows. Physical input/audio remain manual.
Exit codes: 0 selected set passed or INFO; 1 check failed; 2 usage; 3 environment/lock;
            4 runner/artifact error; 124 child timeout; 130 cancelled.
Direct dotnet commands through KID.sln remain supported; coordinate them with runner yourself.
'@
    exit 0
}

if ($command -eq 'manual') {
    foreach ($scenario in $choices.Scenario | Where-Object { $_ -ne 'All' }) {
        if ($options.Scenario -ne 'All' -and $options.Scenario -ne $scenario) { continue }
        $readme = Join-Path $manualRoot "$scenario/README.md"
        if (-not (Test-Path -LiteralPath $readme -PathType Leaf)) { [Console]::Error.WriteLine("ENVIRONMENT: Missing $readme"); exit 3 }
        Write-Output "INFO ${scenario}: $readme"
        if ($options.Scenario -ne 'All') { Get-Content -LiteralPath $readme }
    }
    exit 0
}

if ($PSVersionTable.PSVersion.Major -lt 7 -or -not $IsWindows) {
    [Console]::Error.WriteLine('ENVIRONMENT: PowerShell 7+ on Windows is required.'); exit 3
}

# Managed event handlers run without a PowerShell runspace. Both pipes are drained
# concurrently; timeout/cancellation kill only the process tree started by this runner.
try {
    if (-not ('KIDHarness.ProcessHost' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace KIDHarness {
    public sealed class ProcessResult {
        public int? ExitCode { get; set; }
        public string Status { get; set; } = "failed";
        public string StdOut { get; set; } = "";
        public string StdErr { get; set; } = "";
    }
    public static class ProcessHost {
        static volatile bool cancelled;
        static Process active;
        public static bool Cancelled => cancelled;
        // PowerShell's own handler can consume Console.CancelKeyPress while a
        // synchronous managed method runs. Register last in the Windows handler
        // chain so Ctrl+C/Ctrl+Break request cancellation immediately.
        delegate bool ConsoleHandler(uint signal);
        static readonly ConsoleHandler handler = signal => {
            if (signal > 1) return false;
            cancelled = true; return true;
        };
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetConsoleCtrlHandler(ConsoleHandler callback, bool add);
        [DllImport("kernel32.dll", EntryPoint = "SetConsoleCtrlHandler")] static extern bool SetIgnoreCtrlC(IntPtr callback, bool ignore);
        public static void Begin() {
            cancelled = false;
            // Noninteractive parent processes can pass down the ignore-Ctrl+C flag.
            SetIgnoreCtrlC(IntPtr.Zero, false);
            SetConsoleCtrlHandler(handler, true);
        }
        public static void Finish() { StopActive(); SetConsoleCtrlHandler(handler, false); }
        public static void StopActive() {
            var child = active;
            if (child != null) { try { if (!child.HasExited) child.Kill(true); } catch (InvalidOperationException) {} }
        }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
        public static bool HasDesktop() {
            if (!Environment.UserInteractive || Process.GetCurrentProcess().SessionId == 0) return false;
            var desktop = OpenInputDesktop(0, false, 1);
            if (desktop == IntPtr.Zero) return false;
            CloseDesktop(desktop); return true;
        }
        static async Task Drain(StreamReader reader, TextWriter log, StringBuilder captured, object gate, bool echo, bool error) {
            string line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
                captured.AppendLine(line);
                lock (gate) {
                    if (log != null) { log.WriteLine(line); log.Flush(); }
                    if (echo) { if (error) Console.Error.WriteLine(line); else Console.WriteLine(line); }
                }
            }
        }
        public static ProcessResult Run(string file, string[] args, string cwd, int seconds, string logPath, bool echo) {
            var result = new ProcessResult();
            using var log = string.IsNullOrEmpty(logPath) ? null : new StreamWriter(logPath, false, new UTF8Encoding(false));
            log?.WriteLine("Executable: " + file);
            log?.WriteLine("Arguments: " + System.Text.Json.JsonSerializer.Serialize(args));
            log?.WriteLine("WorkingDirectory: " + cwd);
            using var child = new Process();
            child.StartInfo = new ProcessStartInfo(file) { WorkingDirectory = cwd, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            foreach (var arg in args) child.StartInfo.ArgumentList.Add(arg);
            if (cancelled) { result.Status = "cancelled"; return result; }
            active = child;
            var output = new StringBuilder(); var error = new StringBuilder(); var gate = new object();
            bool started = false;
            try {
                child.Start();
                started = true;
                var stdout = Drain(child.StandardOutput, log, output, gate, echo, false);
                var stderr = Drain(child.StandardError, log, error, gate, echo, true);
                var watch = Stopwatch.StartNew();
                while (!child.WaitForExit(100)) {
                    if (cancelled || watch.Elapsed.TotalSeconds >= seconds) {
                        result.Status = cancelled ? "cancelled" : "timed-out";
                        child.Kill(true); child.WaitForExit(); break;
                    }
                    // A failed log/output writer must not leave a blocked, unobserved child.
                    if (stdout.IsFaulted || stderr.IsFaulted) { child.Kill(true); child.WaitForExit(); break; }
                }
                Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
                result.ExitCode = child.ExitCode;
                if (cancelled) result.Status = "cancelled";
                else if (result.Status != "timed-out") result.Status = child.ExitCode == 0 ? "passed" : "failed";
                result.StdOut = output.ToString(); result.StdErr = error.ToString();
                log?.WriteLine("Status: " + result.Status + "; ExitCode: " + result.ExitCode);
                return result;
            }
            finally {
                if (started && !child.HasExited) { child.Kill(true); child.WaitForExit(); }
                active = null;
            }
        }
    }
}
'@
    }
}
catch { [Console]::Error.WriteLine("RUNNER: $($_.Exception.Message)"); exit 4 }

$environmentInfo = [ordered]@{ powershell = $PSVersionTable.PSVersion.ToString(); os = [Environment]::OSVersion.VersionString; dotnet = $null; sdk = $null; desktop = $null; globalJson = (Join-Path $repoRoot 'global.json') }
function Assert-Environment {
    foreach ($path in @($solution, (Join-Path $repoRoot 'global.json'), $tests, $probe, $demo)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required file: $path" }
    }
    foreach ($scenario in $choices.Scenario | Where-Object { $_ -ne 'All' }) {
        $path = Join-Path $manualRoot "$scenario/README.md"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required file: $path" }
    }
    $dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $environmentInfo.dotnet = $dotnetCommand.Source
    $sdk = [KIDHarness.ProcessHost]::Run($environmentInfo.dotnet, @('--version'), $repoRoot, 30, $null, $false)
    if ($sdk.Status -ne 'passed') { throw "SDK selection through global.json failed: $($sdk.StdErr) $($sdk.StdOut)" }
    $environmentInfo.sdk = $sdk.StdOut.Trim()
    $environmentInfo.desktop = [KIDHarness.ProcessHost]::HasDesktop()
    Write-Output "INFO PowerShell $($environmentInfo.powershell); $($environmentInfo.os); SDK $($environmentInfo.sdk)"
    Write-Output "INFO dotnet: $($environmentInfo.dotnet); root: $repoRoot"
    Write-Output "INFO solution: $solution; tests: $tests; probe: $probe; demo: $demo"
    Write-Output "INFO manual READMEs: $manualRoot; desktop: $($environmentInfo.desktop)"
    if (($command -eq 'demo' -or ($command -eq 'check' -and $options.Profile -eq 'Full')) -and -not $environmentInfo.desktop) {
        throw 'A usable interactive Windows desktop is required for demo/Full; no checks were skipped as PASS.'
    }
}

if ($command -eq 'doctor') {
    try { Assert-Environment; exit 0 }
    catch { [Console]::Error.WriteLine("ENVIRONMENT: $($_.Exception.Message)"); exit 3 }
}

$runDirectory = $null; $lock = $null; $exitCode = 4; $currentOperation = $null
$operations = [Collections.Generic.List[object]]::new()
$selectedModes = @(); $selectedConsoles = @()
if ($command -in @('probe', 'check')) {
    $selectedModes = if ($command -eq 'check' -or $options.Mode -eq 'All') { $choices.Mode | Where-Object { $_ -ne 'All' } } else { @($options.Mode) }
}
if ($command -eq 'demo' -or ($command -eq 'check' -and $options.Profile -eq 'Full')) {
    $selectedConsoles = if ($command -eq 'check' -or $options.ConsoleMode -eq 'Both') { @('Without', 'With') } else { @($options.ConsoleMode) }
}
$summary = [ordered]@{
    command = $command; profile = $(if ($command -eq 'check') { $options.Profile } else { $null })
    modes = @($selectedModes); consoleModes = @($selectedConsoles); configuration = $options.Configuration
    noRestore = $options.NoRestore; timeoutSeconds = $options.TimeoutSeconds
    revision = 'unknown'; dirty = $null; environment = $environmentInfo
    startedUtc = [DateTime]::UtcNow.ToString('o'); endedUtc = $null; status = 'failed'; exitCode = $null
    error = $null; operations = $operations
}
function Add-Operation([string]$name, [string[]]$arguments) {
    $operations.Add([ordered]@{ name = $name; executable = 'dotnet'; arguments = $arguments
        workingDirectory = $repoRoot; startedUtc = $null; endedUtc = $null; durationSeconds = $null
        childExitCode = $null; status = 'not-run'; log = (Join-Path $runDirectory ('{0:D2}-{1}.log' -f ($operations.Count + 1), $name)) })
}
function Add-Restore([string]$name, [string]$project) {
    if (-not $options.NoRestore) { Add-Operation "restore-$name" @('restore', $project, '--nologo') }
}
function Save-Summary {
    $json = $summary | ConvertTo-Json -Depth 12
    $path = Join-Path $runDirectory 'summary.json'
    [IO.File]::WriteAllText("$path.tmp", $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::Move("$path.tmp", $path, $true)
}

try {
    try {
        $artifacts = Join-Path $repoRoot 'artifacts/harness'
        [IO.Directory]::CreateDirectory($artifacts) | Out-Null
        $runDirectory = Join-Path $artifacts ('{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [Guid]::NewGuid().ToString('N').Substring(0, 8))
        [IO.Directory]::CreateDirectory($runDirectory) | Out-Null
    }
    catch { $exitCode = 3; throw }
    $config = $options.Configuration
    if ($command -in @('build', 'test', 'check')) {
        Add-Restore 'solution' $solution
        Add-Operation 'build-solution' @('build', $solution, '-c', $config, '--no-restore', '--nologo')
    }
    if ($command -in @('test', 'check')) {
        Add-Operation 'test-solution' @('test', '--solution', $solution, '-c', $config, '--no-build', '--no-restore',
            '--results-directory', (Join-Path $runDirectory 'test-results'), '--report-xunit-trx', '--report-xunit-trx-filename', 'KID.Tests.trx')
    }
    if ($selectedModes.Count) {
        Add-Restore 'probe' $probe
        foreach ($mode in $selectedModes) {
            Add-Operation "probe-$mode" @('run', '--project', $probe, '-c', $config, '--no-restore', "-p:StandaloneContractStage=$mode")
        }
    }
    if ($selectedConsoles.Count) {
        Add-Restore 'demo' $demo
        Add-Operation 'build-demo' @('build', $demo, '-c', $config, '--no-restore', '--nologo')
        foreach ($consoleMode in $selectedConsoles) {
            $demoArgs = @('run', '--project', $demo, '-c', $config, '--no-build', '--no-restore', '--', '--verify')
            if ($consoleMode -eq 'With') { $demoArgs += '--with-console' }
            Add-Operation "demo-$consoleMode" $demoArgs
        }
    }
    Save-Summary
    try {
        $lockPath = Join-Path $artifacts 'runner.lock'
        $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $metadata = [Text.Encoding]::UTF8.GetBytes((@{ pid = $PID; runDirectory = $runDirectory; startedUtc = $summary.startedUtc } | ConvertTo-Json))
        $lock.SetLength(0); $lock.Write($metadata, 0, $metadata.Length); $lock.Flush()
    }
    catch { $exitCode = 3; throw "Cannot acquire runner lock ($lockPath): $($_.Exception.Message)" }
    [KIDHarness.ProcessHost]::Begin()
    try { Assert-Environment }
    catch { $exitCode = 3; throw }
    try {
        $git = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
        $revision = [KIDHarness.ProcessHost]::Run($git, @('-c', "safe.directory=$repoRoot", 'rev-parse', 'HEAD'), $repoRoot, 10, $null, $false)
        $dirty = [KIDHarness.ProcessHost]::Run($git, @('-c', "safe.directory=$repoRoot", 'status', '--porcelain'), $repoRoot, 10, $null, $false)
        if ($revision.Status -eq 'passed') { $summary.revision = $revision.StdOut.Trim() }
        if ($dirty.Status -eq 'passed') { $summary.dirty = -not [string]::IsNullOrWhiteSpace($dirty.StdOut) }
    }
    catch { Write-Output 'INFO Git revision unavailable; checks do not depend on Git.' }
    $exitCode = 0
    foreach ($operation in $operations) {
        $currentOperation = $operation
        if ([KIDHarness.ProcessHost]::Cancelled) { $operation.status = 'cancelled'; $exitCode = 130; break }
        if ($operation.name.StartsWith('demo-') -and -not [KIDHarness.ProcessHost]::HasDesktop()) {
            $exitCode = 3; throw 'The interactive desktop became unavailable before demo.'
        }
        Write-Output "RUN $($operation.name)"
        $operation.executable = $environmentInfo.dotnet
        $operation.startedUtc = [DateTime]::UtcNow.ToString('o')
        $watch = [Diagnostics.Stopwatch]::StartNew()
        try {
            $result = [KIDHarness.ProcessHost]::Run($environmentInfo.dotnet, $operation.arguments, $repoRoot, $options.TimeoutSeconds, $operation.log, $true)
            $operation.status = $result.Status; $operation.childExitCode = $result.ExitCode
        }
        finally { $operation.endedUtc = [DateTime]::UtcNow.ToString('o'); $operation.durationSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 3) }
        $exitCode = switch ($operation.status) { passed { 0 }; failed { 1 }; timed-out { 124 }; cancelled { 130 }; default { 4 } }
        if ($exitCode -eq 0 -and $operation.name -eq 'test-solution') {
            $trx = Get-ChildItem -LiteralPath $runDirectory -Recurse -Filter *.trx
            if (-not $trx) { $operation.status = 'failed'; $exitCode = 4; $summary.error = 'Test command did not save the required TRX report.' }
        }
        Save-Summary
        if ($exitCode -ne 0) { break }
        $currentOperation = $null
    }
}
catch [Management.Automation.PipelineStoppedException] { $exitCode = 130; $summary.error = 'Cancelled by user.' }
catch {
    if ($exitCode -eq 0) { $exitCode = 4 }
    $summary.error = $_.Exception.Message
    [Console]::Error.WriteLine("FAIL: $($summary.error)")
}
finally {
    try { [KIDHarness.ProcessHost]::Finish() }
    catch { $exitCode = 4; $summary.error = "Child cleanup failed: $($_.Exception.Message)" }
    if ([KIDHarness.ProcessHost]::Cancelled) { $exitCode = 130 }
    if ($null -ne $currentOperation -and $currentOperation.status -eq 'not-run' -and $currentOperation.startedUtc) {
        $currentOperation.status = if ($exitCode -eq 130) { 'cancelled' } else { 'failed' }
    }
    if ($null -ne $lock) { $lock.Dispose() }
    $summary.endedUtc = [DateTime]::UtcNow.ToString('o'); $summary.exitCode = $exitCode
    $summary.status = switch ($exitCode) { 0 { 'passed' }; 124 { 'timed-out' }; 130 { 'cancelled' }; default { 'failed' } }
    if ($runDirectory) {
        try { Save-Summary }
        catch { $exitCode = 4; [Console]::Error.WriteLine("FAIL: Cannot save required summary: $($_.Exception.Message)") }
    }
    $label = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
    [Console]::WriteLine("$label $command profile=$($summary.profile) configuration=$($options.Configuration) modes=$($selectedModes -join ',') console=$($selectedConsoles -join ',') exit=$exitCode")
    foreach ($operation in $operations) { [Console]::WriteLine("  $($operation.name): $($operation.status) childExit=$($operation.childExitCode)") }
    if ($runDirectory) { [Console]::WriteLine("Summary: $(Join-Path $runDirectory 'summary.json')") }
    if ($exitCode -eq 130) { exit 130 }
}
exit $exitCode
