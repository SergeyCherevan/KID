[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$DataRoot,
    [ValidateSet('Release','Debug')] [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HarnessData.psm1') -Force
$root = Assert-HarnessDataRoot $DataRoot
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe = Join-Path $repo "KID.WPF.IDE/bin/$Configuration/net10.0-windows/KID.WPF.IDE.exe"
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Build IDE first: $exe" }
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $repo
$start.Environment['KID_TEST_DATA_ROOT'] = $root
$process = [Diagnostics.Process]::Start($start)
Write-Output "Started isolated KID pid=$($process.Id) DataRoot=$root; close this window before preparing/restoring its fixtures."
$process.Dispose()
