# Behavioral contract checks in an isolated checkout with a child-process fixture.
# Requires PowerShell 7+, Windows and the SDK selected by the repository global.json.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$pwsh = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
$realDotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$workspace = Join-Path $repo ('artifacts/harness-runner-contract/{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [Guid]::NewGuid().ToString('N').Substring(0, 8))
$fixture = Join-Path $workspace 'checkout with spaces'
$runner = Join-Path $fixture 'harness/tools/Invoke-Harness.ps1'
$shimFolder = Join-Path $workspace 'shim'
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$checks = [Collections.Generic.List[string]]::new()

function Assert([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Write-File([string]$path, [string]$content) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
}
function Start-Runner([string[]]$parameters, [string]$behavior = '', [string]$path = $shimFolder) {
    $start = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.WorkingDirectory = $workspace
    foreach ($item in @('-NoProfile', '-File', $runner) + $parameters) { $start.ArgumentList.Add($item) }
    $start.Environment['PATH'] = $path
    $start.Environment['KID_HARNESS_FIXTURE_BEHAVIOR'] = $behavior
    $start.Environment['KID_HARNESS_FIXTURE_PIDS'] = Join-Path $workspace 'pids.txt'
    $process = [Diagnostics.Process]::Start($start); $children.Add($process)
    return @{ process = $process; stdout = $process.StandardOutput.ReadToEndAsync(); stderr = $process.StandardError.ReadToEndAsync() }
}
function Finish-Runner($child, [int]$expected) {
    if (-not $child.process.WaitForExit(60000)) { $child.process.Kill($true); throw 'Contract test runner exceeded 60 seconds.' }
    $output = $child.stdout.GetAwaiter().GetResult() + $child.stderr.GetAwaiter().GetResult()
    Assert ($child.process.ExitCode -eq $expected) "Expected exit $expected, got $($child.process.ExitCode): $output"
    $summary = $null
    if ($output -match '(?m)^Summary: (.+)\r?$') { $summary = Get-Content -LiteralPath $Matches[1].Trim() -Raw | ConvertFrom-Json }
    return @{ output = $output; summary = $summary }
}
function Run([string[]]$parameters, [int]$expected = 0, [string]$behavior = '', [string]$path = $shimFolder) {
    Finish-Runner (Start-Runner $parameters $behavior $path) $expected
}
function Passed([string]$name) { $checks.Add($name); Write-Output "PASS $name" }

try {
    Write-File $runner (Get-Content -LiteralPath (Join-Path $repo 'harness/tools/Invoke-Harness.ps1') -Raw)
    Write-File (Join-Path $fixture 'harness/tools/Test-HarnessStatic.ps1') (Get-Content -LiteralPath (Join-Path $repo 'harness/tools/Test-HarnessStatic.ps1') -Raw)
    Copy-Item -LiteralPath (Join-Path $repo 'global.json') -Destination (Join-Path $fixture 'global.json')
    Copy-Item -LiteralPath (Join-Path $repo 'KID.sln') -Destination (Join-Path $fixture 'KID.sln')
    $projects = @('KID.Library/KID.Library.csproj', 'KID.WPF.IDE/KID.WPF.IDE.csproj',
        'harness/checks/automated/KID.Tests/KID.Tests.csproj',
        'harness/checks/probes/KID.Library.StandaloneProbe/KID.Library.StandaloneProbe.csproj',
        'harness/checks/probes/KID.Library.StandaloneDemo/KID.Library.StandaloneDemo.csproj')
    foreach ($project in $projects) { Write-File (Join-Path $fixture $project) '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>' }
    foreach ($scenario in @('standalone-library', 'music-download', 'first-launch-language', 'persistence-recovery', 'editor-session-recovery')) {
        Write-File (Join-Path $fixture "harness/checks/manual/$scenario/README.md") "Fixture README: $scenario"
    }
    $buildDirectory = Join-Path $workspace 'fixture-build'
    Write-File (Join-Path $buildDirectory 'Fixture.csproj') '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><AssemblyName>dotnet</AssemblyName></PropertyGroup></Project>'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ProcessFixture.cs') -Destination (Join-Path $buildDirectory 'Program.cs')
    & $realDotnet build (Join-Path $buildDirectory 'Fixture.csproj') -c Release -o $shimFolder --nologo
    Assert ($LASTEXITCODE -eq 0) 'Cannot build process fixture.'

    $null = Run @() 0 '' (Join-Path $workspace 'no-dotnet')
    $null = Run @('-Help') 0 '' (Join-Path $workspace 'no-dotnet')
    Assert (-not (Test-Path (Join-Path $fixture 'artifacts'))) 'Help created artifacts.'
    Passed 'help without dotnet or artifacts'
    foreach ($parameters in @(
        @('-Command', 'unknown'), @('-Unknown'), @('-Command', 'test', '-Mode', 'Session'),
        @('-Command', 'check', '-Profile', 'Full', '-Mode', 'Regression'), @('-Command', 'build', '-TimeoutSeconds', '0'),
        @('-Command', 'build', '-TimeoutSeconds', '86401'), @('-Help', '-Configuration', 'Debug'), @('-Command', 'demo', '-ConsoleMode', 'bad'),
        @('-Command', 'validate', '-Profile', 'Full'), @('-Command', 'contracts', '-NoRestore')
    )) { $null = Run $parameters 2 }
    Assert (-not (Test-Path (Join-Path $fixture 'artifacts'))) 'Invalid parameters started a working run.'
    Passed 'usage errors precede children and artifacts'
    $null = Run @('-Command', 'doctor')
    $manual = Run @('-Command', 'manual', '-Scenario', 'persistence-recovery')
    Assert ($manual.output.Contains('Fixture README: persistence-recovery')) 'manual did not show the README.'
    $null = Run @('-Command', 'manual')
    Assert (-not (Test-Path (Join-Path $fixture 'artifacts'))) 'Information commands created artifacts.'
    Passed 'doctor/manual are information only'

    $unavailable = Run @('-Command', 'build', '-NoRestore') 3 '' (Join-Path $workspace 'no-dotnet')
    Assert (@($unavailable.summary.operations | Where-Object status -ne 'not-run').Count -eq 0) 'Unavailable SDK ran checks.'
    Passed 'unavailable SDK exits 3 and keeps mandatory operations not-run'

    $headless = (Run @('-Command', 'ChEcK', '-Configuration', 'debug', '-NoRestore')).summary
    $expected = @('validate-static', 'build-solution', 'test-solution', 'probe-Regression', 'probe-Session', 'probe-SessionFailure', 'probe-CanvasSwitching', 'probe-CanvasObjects')
    Assert (($headless.operations.name -join ',') -eq ($expected -join ',')) 'Wrong Headless operation order.'
    Assert ($headless.configuration -eq 'Debug' -and $headless.profile -eq 'Headless') 'Defaults/canonical configuration were lost.'
    foreach ($op in $headless.operations) {
        if ($op.name -ne 'validate-static') { Assert ($op.arguments -contains '--no-restore') 'NoRestore lost.' }
        Assert ($op.workingDirectory -eq $fixture -and $op.childExitCode -eq 0 -and $op.status -eq 'passed') 'Incorrect child result or working directory.'
        Assert (Test-Path -LiteralPath $op.log) 'Missing operation log.'
        if ($op.name.StartsWith('probe-')) { Assert ($op.arguments -notcontains '--no-build') 'Probe was not rebuilt.' }
    }
    Passed 'Headless/Debug/NoRestore, paths with spaces, foreign cwd, isolated probe order and logs'

    $full = (Run @('-Command', 'check', '-Profile', 'Full')).summary
    Assert ($full.operations.Count -eq 14) 'Full did not schedule all 14 validation/restore/build/test/probe/demo operations.'
    Assert (($full.consoleModes -join ',') -eq 'Without,With') 'Full omitted a console mode.'
    Assert (@($full.operations | Where-Object name -eq 'build-solution').Count -eq 1) 'Repeated solution build.'
    Assert (@($full.operations | Where-Object name -eq 'build-demo').Count -eq 1) 'Repeated demo build.'
    Assert ($full.operations[-1].arguments -contains '--with-console') 'With-console argument was lost.'
    Passed 'Full complete schedule, restore, single solution/demo builds'

    $failure = (Run @('-Command', 'build', '-NoRestore') 1 'fail').summary
    Assert ($failure.operations[0].childExitCode -eq 37 -and $failure.operations[0].status -eq 'failed') 'Original child error lost.'
    Assert (@($failure.operations | Select-Object -Skip 1 | Where-Object status -ne 'not-run').Count -eq 0) 'Runner continued after failure.'
    Passed 'child exit 37 preserved; fail-fast leaves remaining checks not-run'

    $noReport = (Run @('-Command', 'test', '-NoRestore') 4 'no-trx').summary
    Assert ($noReport.operations[-1].status -eq 'failed' -and $noReport.operations[-1].childExitCode -eq 0) 'Missing mandatory TRX was masked by child success.'
    Passed 'missing mandatory TRX exits 4 despite child success'

    $empty = (Run @('-Command', 'test', '-NoRestore') 1 'empty-trx').summary
    Assert ($empty.error.Contains('no executed tests')) 'Empty selection was accepted.'
    Passed 'zero executed tests fail acceptance'
    $static = (Run @('-Command', 'validate')).summary
    Assert ($static.operations.Count -eq 1 -and $static.operations[0].name -eq 'validate-static') 'validate schedule changed'
    Write-File (Join-Path $fixture 'harness/broken.md') '[broken](absent.md)'
    $invalid = (Run @('-Command', 'check', '-NoRestore') 1).summary
    Assert ($invalid.operations[0].name -eq 'validate-static' -and $invalid.operations[1].status -eq 'not-run') 'Validation failure did not stop general check'
    Write-File (Join-Path $fixture 'harness/broken.md') '# repaired'
    Passed 'static child logs, validate command, check fail-fast without recursive contracts'
    $noSdkValidation = Run @('-Command', 'validate') 0 '' (Join-Path $workspace 'no-dotnet')
    Assert ($noSdkValidation.summary.operations.Count -eq 1) 'Static validation depends on SDK'
    Write-File (Join-Path $fixture 'harness/checks/automated/runner/Verify-Harness.ps1') "Write-Output 'contract child'; exit 0"
    Write-File (Join-Path $fixture 'harness/checks/automated/static/Verify-Static.ps1') "Write-Output 'static fixture child'; exit 0"
    $contracts = (Run @('-Command', 'contracts')).summary
    Assert (($contracts.operations.name -join ',') -eq 'contracts-runner,contracts-static') 'contracts command scheduled general check or omitted fixtures'
    Passed 'validate without SDK and contracts command isolated schedule'

    $pids = Join-Path $workspace 'pids.txt'
    $sleeper = Start-Runner @('-Command', 'build', '-NoRestore', '-TimeoutSeconds', '15') 'sleep'
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $pids) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    Assert (Test-Path -LiteralPath $pids) 'Sleeping child did not start.'
    $busy = Run @('-Command', 'build', '-NoRestore') 3
    Assert ($busy.output.Contains('lock')) 'Busy-lock classification lost.'
    $timed = (Finish-Runner $sleeper 124).summary
    Assert ($timed.operations[0].status -eq 'timed-out') 'Timeout status lost.'
    foreach ($childId in (Get-Content $pids).Split(',')) { Assert ($null -eq (Get-Process -Id ([int]$childId) -ErrorAction SilentlyContinue)) 'Timeout left its process tree running.' }
    $null = Run @('-Command', 'build', '-NoRestore')
    Passed 'busy lock, timeout 124, child-tree termination and lock reuse'

    Remove-Item -LiteralPath $pids
    $cancelOut = Join-Path $workspace 'cancel.out'; $cancelErr = Join-Path $workspace 'cancel.err'
    # A hidden console lets a real CTRL_C_EVENT exercise the same managed handler as Ctrl+C.
    $cancelScript = Join-Path $workspace 'cancel-run.ps1'
    $escapedRunner = $runner.Replace("'", "''"); $escapedShim = $shimFolder.Replace("'", "''"); $escapedPids = $pids.Replace("'", "''")
    Write-File $cancelScript "`$env:PATH = '$escapedShim'; `$env:KID_HARNESS_FIXTURE_BEHAVIOR = 'sleep'; `$env:KID_HARNESS_FIXTURE_PIDS = '$escapedPids'; & '$escapedRunner' -Command build -NoRestore -TimeoutSeconds 50; exit `$LASTEXITCODE"
    $cancelProcess = Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile', '-File', ('"{0}"' -f $cancelScript)) -WindowStyle Hidden -PassThru -RedirectStandardOutput $cancelOut -RedirectStandardError $cancelErr
    $children.Add($cancelProcess)
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $pids) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    Assert (Test-Path -LiteralPath $pids) 'Cancellation fixture did not start.'
    & (Join-Path $shimFolder 'dotnet.exe') --cancel-console $cancelProcess.Id
    Assert ($LASTEXITCODE -eq 0) 'Could not send CTRL_C_EVENT to the hidden test console.'
    Assert ($cancelProcess.WaitForExit(20000)) 'Cancellation did not finish.'
    Assert ($cancelProcess.ExitCode -eq 130) "Cancellation exit was $($cancelProcess.ExitCode). See $cancelOut and $cancelErr."
    $cancelText = Get-Content -LiteralPath $cancelOut -Raw
    Assert ($cancelText -match '(?m)^Summary: (.+)\r?$') 'Cancellation did not print a summary.'
    $cancelSummary = Get-Content -LiteralPath $Matches[1].Trim() -Raw | ConvertFrom-Json
    Assert ($cancelSummary.status -eq 'cancelled' -and $cancelSummary.operations[0].status -eq 'cancelled') 'Cancellation summary lost.'
    foreach ($childId in (Get-Content $pids).Split(',')) { Assert ($null -eq (Get-Process -Id ([int]$childId) -ErrorAction SilentlyContinue)) 'Cancellation left a child running.' }
    $null = Run @('-Command', 'build', '-NoRestore')
    Passed 'real Ctrl+C event, exit 130, cancelled summary, child cleanup and lock reuse'

    # Real SDK with intentionally absent assets: no network restore or product files involved.
    $noAssets = Run @('-Command', 'build', '-NoRestore') 1 '' ([IO.Path]::GetDirectoryName($realDotnet))
    Assert ($noAssets.output.Contains('NETSDK1004')) 'Missing assets did not produce the expected SDK failure.'
    Assert (($noAssets.summary.operations.name -join ',') -eq 'build-solution') 'NoRestore fell back to restore.'
    Passed 'real SDK NoRestore without assets fails and never restores'
    Write-File (Join-Path $workspace 'result.json') (@{ status = 'passed'; checks = $checks; workspace = $workspace } | ConvertTo-Json -Depth 5)
    Write-Output "PASS runner contract: $($checks.Count) groups. Artifacts: $workspace"
}
finally {
    foreach ($child in $children) { if (-not $child.HasExited) { $child.Kill($true); $child.WaitForExit() }; $child.Dispose() }
}
