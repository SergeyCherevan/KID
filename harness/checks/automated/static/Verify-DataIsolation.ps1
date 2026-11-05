[CmdletBinding()]
param([switch]$IncludeWpf)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Import-Module (Join-Path $repo 'harness/tools/HarnessData.psm1') -Force
$exe = Join-Path $repo 'harness/checks/probes/KID.Data.Smoke/bin/Release/net10.0-windows/KID.Data.Smoke.exe'
$workspace = New-HarnessDataRoot
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$results = [Collections.Generic.List[object]]::new()
$lock = $null
function Snapshot {
    $settings = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'KID'
    $logs = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'KID/Logs'
    $paths = @((Join-Path $settings 'settings.json'), (Join-Path $settings 'editor-session.json'))
    if (Test-Path -LiteralPath $logs) { $paths += @(Get-ChildItem -LiteralPath $logs -File | ForEach-Object FullName) }
    $state = [ordered]@{}
    foreach ($path in ($paths | Sort-Object)) { $state[$path] = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'absent' } }
    $state | ConvertTo-Json -Compress
}
function Start-Child([string]$program, [string[]]$arguments, [string]$root) {
    $start = [Diagnostics.ProcessStartInfo]::new($program)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.WorkingDirectory = $repo; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($arg in $arguments) { $start.ArgumentList.Add($arg) }
    $null = $start.Environment.Remove('KID_TEST_DATA_ROOT')
    if ($null -ne $root -and $root -ne '') { $start.Environment['KID_TEST_DATA_ROOT'] = $root }
    $p = [Diagnostics.Process]::Start($start); $processes.Add($p)
    @{ process = $p; stdout = $p.StandardOutput.ReadToEndAsync(); stderr = $p.StandardError.ReadToEndAsync(); executable = $program; arguments = $arguments; dataRoot = $root }
}
function Finish-Child($child, [string]$label, [int]$expected = 0, [switch]$AnyFailure) {
    if (-not $child.process.WaitForExit(45000)) { $child.process.Kill($true); throw "$label exceeded timeout" }
    $output = $child.stdout.GetAwaiter().GetResult() + $child.stderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $workspace "$label.log"), $output)
    $code = $child.process.ExitCode
    if (($AnyFailure -and $code -eq 0) -or (-not $AnyFailure -and $code -ne $expected)) { throw "$label unexpected exit $code : $output" }
    if ((Snapshot) -ne $script:baseline) { throw "$label changed real profile files" }
    $results.Add(@{ name = $label; exitCode = $code; realProfileUnchanged = $true; executable = $child.executable; arguments = $child.arguments; dataRoot = $child.dataRoot })
    Write-Output "PASS $label exit=$code; real profile unchanged"
}
function Run($mode, $root, $label, $expected = 0) { Finish-Child (Start-Child $exe @($mode) $root) $label $expected }
try {
    $lock = [IO.File]::Open((Join-Path $repo 'artifacts/harness/runner.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    $script:baseline = Snapshot
    [IO.File]::WriteAllText((Join-Path $workspace 'profile-before.json'), $baseline)
    Run 'paths' '' 'normal-constructors'
    $a = New-HarnessDataRoot; $b = New-HarnessDataRoot
    Run 'save' $a 'success-a'; Run 'verify' $a 'restart-a'
    if (-not (Get-ChildItem -LiteralPath (Join-Path $a 'Logs') -Filter 'kid-*.jsonl')) { throw 'Structured log was not isolated' }
    Run 'save' $b 'success-b'; Run 'verify' $a 'independent-a'; Run 'verify' $b 'independent-b'
    Run 'failure' $b 'intentional-failure' 37
    Run 'corrupt' $b 'corrupt-recovery'
    $cancelRoot = New-HarnessDataRoot
    $child = Start-Child $exe @('wait') $cancelRoot
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while (-not (Test-Path -LiteralPath (Join-Path $cancelRoot 'ready')) -and [DateTime]::UtcNow -lt $deadline -and -not $child.process.HasExited) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path -LiteralPath (Join-Path $cancelRoot 'ready'))) { throw 'Cancellation child never became ready' }
    $child.process.Kill($true)
    Finish-Child $child 'cancelled-owned-process' -AnyFailure
    Run 'verify' $cancelRoot 'restart-after-cancel'
    foreach ($invalid in @('relative-root', (Join-Path $workspace 'missing'), [IO.Path]::GetPathRoot($workspace))) {
        Finish-Child (Start-Child $exe @('save') $invalid) ('invalid-' + $results.Count) -AnyFailure
    }
    $blocked = New-HarnessDataRoot
    [IO.File]::WriteAllText((Join-Path $blocked 'Logs'), 'blocked')
    Finish-Child (Start-Child $exe @('save') $blocked) 'blocked-logs' -AnyFailure
    $lockedRoot = New-HarnessDataRoot
    $ownedMarker = [IO.File]::Open((Join-Path $lockedRoot '.kid-harness-owner'), 'Open', 'ReadWrite', 'None')
    try { Finish-Child (Start-Child $exe @('save') $lockedRoot) 'inaccessible-marker' -AnyFailure }
    finally { $ownedMarker.Dispose() }
    $junctionParent = New-HarnessDataRoot
    $link = Join-Path $junctionParent 'junction'
    New-Item -ItemType Junction -Path $link -Target $a | Out-Null
    Finish-Child (Start-Child $exe @('save') $link) 'reparse-root' -AnyFailure
    # Exercise both real recovery fixture scripts against the same explicit root.
    $fixtureRoot = New-HarnessDataRoot
    & (Join-Path $repo 'harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1') -Case NullFontFamily -DataRoot $fixtureRoot
    Run 'check-settings' $fixtureRoot 'prepared-settings-root'
    & (Join-Path $repo 'harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1') -Case Restore -DataRoot $fixtureRoot
    & (Join-Path $repo 'harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1') -Case DuplicateFontSize -DataRoot $fixtureRoot
    Run 'check-settings' $fixtureRoot 'prepared-duplicate-settings'
    & (Join-Path $repo 'harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1') -Case Restore -DataRoot $fixtureRoot
    & (Join-Path $repo 'harness/checks/manual/editor-session-recovery/Prepare-EditorSessionRecovery.ps1') -Case NullTabs -DataRoot $fixtureRoot
    Run 'check-invalid-session' $fixtureRoot 'prepared-session-root'
    & (Join-Path $repo 'harness/checks/manual/editor-session-recovery/Prepare-EditorSessionRecovery.ps1') -Case Restore -DataRoot $fixtureRoot
    & (Join-Path $repo 'harness/checks/manual/editor-session-recovery/Prepare-EditorSessionRecovery.ps1') -Case RestoreFixture -DataRoot $fixtureRoot
    Run 'check-fixture' $fixtureRoot 'prepared-two-tabs'
    & (Join-Path $repo 'harness/checks/manual/editor-session-recovery/Prepare-EditorSessionRecovery.ps1') -Case Restore -DataRoot $fixtureRoot
    $cleanup = New-HarnessDataRoot; $owner = (Get-Content (Join-Path $cleanup '.kid-harness-owner') -Raw).Trim()
    $rejected = $false
    try { Remove-HarnessDataRoot $cleanup 'wrong-owner' } catch { $rejected = $true }
    if (-not $rejected -or -not (Test-Path -LiteralPath $cleanup)) { throw 'Cleanup ownership guard failed' }
    Remove-HarnessDataRoot $cleanup $owner
    if (Test-Path -LiteralPath $cleanup) { throw 'Owned cleanup failed' }
    $results.Add(@{ name = 'owned-cleanup'; exitCode = 0 })
    if ($IncludeWpf) {
        $wpfRoot = New-HarnessDataRoot
        $appExe = Join-Path $repo 'KID.WPF.IDE/bin/Release/net10.0-windows/KID.WPF.IDE.exe'
        foreach ($iteration in 1..2) {
            $window = Start-Child $appExe @() $wpfRoot
            $deadline = [DateTime]::UtcNow.AddSeconds(30)
            do { Start-Sleep -Milliseconds 200; $window.process.Refresh() } while (-not $window.process.HasExited -and $window.process.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
            if ($window.process.HasExited -or $window.process.MainWindowHandle -eq 0) { throw 'IDE did not open its WPF window' }
            Start-Sleep -Seconds 3
            if (-not $window.process.CloseMainWindow()) { throw 'Could not request normal IDE close' }
            Finish-Child $window "wpf-open-close-$iteration"
            if (-not (Test-Path (Join-Path $wpfRoot 'settings.json')) -or -not (Test-Path (Join-Path $wpfRoot 'editor-session.json'))) { throw 'WPF did not persist isolated settings/session' }
        }
    }
    [IO.File]::WriteAllText((Join-Path $workspace 'result.json'), (@{ status = 'passed'; includeWpf = [bool]$IncludeWpf; checks = $results; roots = @($a,$b,$cancelRoot); revision = (& git -c "safe.directory=$repo" rev-parse HEAD | Out-String).Trim() } | ConvertTo-Json -Depth 8))
    Write-Output "PASS data isolation; evidence: $workspace"
}
finally {
    foreach ($p in $processes) { if (-not $p.HasExited) { $p.Kill($true); $p.WaitForExit() }; $p.Dispose() }
    try {
        if (-not (Test-Path -LiteralPath (Join-Path $workspace 'result.json'))) {
            [IO.File]::WriteAllText((Join-Path $workspace 'result.json'), (@{ status = 'failed'; checks = $results } | ConvertTo-Json -Depth 8))
        }
        if ($script:baseline) {
            $after = Snapshot
            [IO.File]::WriteAllText((Join-Path $workspace 'profile-after.json'), $after)
            if ($after -ne $script:baseline) {
                [IO.File]::WriteAllText((Join-Path $workspace 'result.json'), (@{ status = 'failed'; error = 'Real profile changed'; checks = $results } | ConvertTo-Json -Depth 8))
                throw 'Real profile changed during data isolation checks'
            }
        }
    }
    finally { if ($lock) { $lock.Dispose() } }
}
