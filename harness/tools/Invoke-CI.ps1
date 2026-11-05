# Shared CI/local clean-checkout entry point. No publishing, checkout deletion or cache.
[CmdletBinding()]
param([switch]$AllowDirty)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$pwsh = (Get-Process -Id $PID).Path
$directory = Join-Path $repo ('artifacts/harness/ci-{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($directory) | Out-Null
$evidence = [ordered]@{ revision = $null; dirty = $null; sdk = $null; powershell = $PSVersionTable.PSVersion.ToString(); os = [Environment]::OSVersion.VersionString; startedUtc = [DateTime]::UtcNow.ToString('o'); endedUtc = $null; status = 'failed'; exitCode = 4; commands = @() }
$code = 4
try {
    Push-Location $repo
    $evidence.revision = (& git -c "safe.directory=$repo" rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE) { throw 'Cannot identify CI revision' }
    $state = & git -c "safe.directory=$repo" status --porcelain
    if ($LASTEXITCODE) { throw 'Cannot inspect CI working tree' }
    $evidence.dirty = -not [string]::IsNullOrWhiteSpace(($state | Out-String))
    if ($evidence.dirty -and -not $AllowDirty) { $code = 3; throw 'Clean tracked/untracked checkout required (ignored outputs are permitted).' }
    $evidence.sdk = (& dotnet --version | Out-String).Trim()
    if ($LASTEXITCODE) { $code = 3; throw 'SDK selection through global.json failed' }
    foreach ($command in @('validate','contracts','check')) {
        $arguments = @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Invoke-Harness.ps1'),'-Command',$command)
        if ($command -eq 'check') { $arguments += @('-Profile','Headless') }
        $log = Join-Path $directory "$command.log"
        & $pwsh @arguments *> $log
        $code = $LASTEXITCODE
        $evidence.commands += [ordered]@{ executable = $pwsh; arguments = $arguments; exitCode = $code; log = $log }
        Get-Content -LiteralPath $log
        if ($code -ne 0) { break }
    }
    if ($code -eq 0) {
        $dotnetArgs = @('build', (Join-Path $repo 'harness/checks/probes/KID.Data.Smoke/KID.Data.Smoke.csproj'), '-c', 'Release', '--nologo')
        $log = Join-Path $directory 'build-data-smoke.log'
        & dotnet @dotnetArgs *> $log
        $code = $LASTEXITCODE
        $evidence.commands += [ordered]@{ executable = 'dotnet'; arguments = $dotnetArgs; exitCode = $code; log = $log }
        Get-Content -LiteralPath $log
    }
    if ($code -eq 0) {
        $smokeArgs = @('-NoProfile','-File',(Join-Path $repo 'harness/checks/automated/static/Verify-DataIsolation.ps1'))
        $log = Join-Path $directory 'data-isolation.log'
        & $pwsh @smokeArgs *> $log
        $code = $LASTEXITCODE
        $evidence.commands += [ordered]@{ executable = $pwsh; arguments = $smokeArgs; exitCode = $code; log = $log }
        Get-Content -LiteralPath $log
    }
    if ($code -eq 0) { $evidence.status = 'passed' }
}
catch {
    if ($code -eq 0) { $code = 4 }
    $evidence['error'] = $_.Exception.Message
    [Console]::Error.WriteLine($_.Exception.Message)
}
finally {
    Pop-Location
    $evidence.endedUtc = [DateTime]::UtcNow.ToString('o'); $evidence.exitCode = $code
    [IO.File]::WriteAllText((Join-Path $directory 'ci-result.json'), ($evidence | ConvertTo-Json -Depth 10))
    Write-Output "CI evidence: $directory"
}
exit $code
