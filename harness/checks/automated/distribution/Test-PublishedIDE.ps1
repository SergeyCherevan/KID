param([Parameter(Mandatory)][string]$PublishDirectory,[Parameter(Mandatory)][string]$OutputDirectory)
. (Join-Path $PSScriptRoot '../../../tools/Release.Common.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Import-Module (Join-Path $repo 'harness/tools/HarnessData.psm1') -Force
$output=Assert-ReleasePath $OutputDirectory (Join-Path $repo 'artifacts/harness')
if (Test-Path -LiteralPath $output) { throw 'Smoke requires a new output' }
New-Item -ItemType Directory $output | Out-Null
$result=[ordered]@{status='failed';exitCode=1;checks=@();scope='Window startup/normal close/persistence only; no physical input, Run/Stop or clean-Windows claim'}
function Get-ProfileSnapshot {
    $paths=@((Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'KID/settings.json'),(Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'KID/editor-session.json'))
    $logs=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'KID/Logs'
    if (Test-Path -LiteralPath $logs) { $paths+=@(Get-ChildItem $logs -File | ForEach-Object FullName) }
    $snapshot=[ordered]@{}
    foreach ($path in $paths | Sort-Object) { $snapshot[$path]=if (Test-Path -LiteralPath $path) {(Get-FileHash -LiteralPath $path).Hash} else {'absent'} }
    return ($snapshot | ConvertTo-Json -Compress)
}
$before=Get-ProfileSnapshot;$process=$null;$lock=$null
try {
    $lock=[IO.File]::Open((Join-Path $repo 'artifacts/harness/runner.lock'),'OpenOrCreate','ReadWrite','None')
    $publish=[IO.Path]::GetFullPath($PublishDirectory)
    $copy=Join-Path $output 'путь с пробелами/publish'
    New-Item -ItemType Directory -Force $copy | Out-Null
    Copy-Item -Path (Join-Path $publish '*') -Destination $copy -Recurse
    $result.manifestSha256=(Get-FileHash (Join-Path $publish 'kid-framework-assemblies.v1.json')).Hash
    foreach ($location in @($publish,$copy)) {
        $dataRoot=New-HarnessDataRoot
        foreach ($iteration in 1..2) {
            $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $location 'KID.WPF.IDE.exe'))
            $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$output
            $start.Environment['KID_TEST_DATA_ROOT']=$dataRoot
            $process=[Diagnostics.Process]::Start($start)
            $deadline=[DateTime]::UtcNow.AddSeconds(40)
            do { Start-Sleep -Milliseconds 200;$process.Refresh() } while (!$process.HasExited -and $process.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
            if ($process.HasExited -or $process.MainWindowHandle -eq 0) { throw "Published IDE did not open: $location" }
            Start-Sleep -Seconds 2
            if (!$process.CloseMainWindow()) { throw 'Normal close request failed' }
            if (!$process.WaitForExit(30000)) { throw 'Published IDE did not close normally' }
            if ($process.ExitCode) { throw "IDE exit $($process.ExitCode)" }
            foreach ($file in @('settings.json','editor-session.json')) { if (!(Test-Path (Join-Path $dataRoot $file))) { throw "Missing isolated persisted $file" } }
            $result.checks+=@{location=$location;workingDirectory=$output;iteration=$iteration;dataRoot=$dataRoot;exitCode=$process.ExitCode;windowOpened=$true;normalClose=$true}
            $process.Dispose();$process=$null
        }
    }
    $result.status='passed';$result.exitCode=0
}
catch { $result.error=$_.Exception.Message;[Console]::Error.WriteLine($_.Exception.Message) }
finally {
    if ($process) { if (!$process.HasExited) {$process.Kill($true);$process.WaitForExit()};$process.Dispose() }
    $after=Get-ProfileSnapshot
    [IO.File]::WriteAllText((Join-Path $output 'profile-before.json'),$before);[IO.File]::WriteAllText((Join-Path $output 'profile-after.json'),$after)
    if ($before -ne $after) { $result.status='failed';$result.exitCode=1;$result.error='Real profile changed during smoke' }
    $result | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'result.json')
    if ($lock) {$lock.Dispose()}
}
exit $result.exitCode
