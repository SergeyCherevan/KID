param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if (!$output.StartsWith((Join-Path $repo 'artifacts/harness/') , [StringComparison]::OrdinalIgnoreCase)) { throw 'Tools must be installed under artifacts/harness' }
New-Item -ItemType Directory -Force $output | Out-Null
$lock=Get-Content (Join-Path $repo 'build/distribution/tools.lock.json') -Raw | ConvertFrom-Json
foreach ($tool in @($lock.inno,$lock.sbom)) {
    $path=Join-Path $output ([Uri]$tool.url).Segments[-1]
    if (!(Test-Path -LiteralPath $path)) { Invoke-WebRequest -Uri $tool.url -OutFile $path }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $tool.sha256) { throw "Pinned tool hash mismatch: $path" }
}
$inno=Join-Path $output "inno-$($lock.inno.version)"
if (!(Test-Path -LiteralPath (Join-Path $inno 'ISCC.exe'))) {
    # Official portable mode disables uninstall registration and icons. No machine tool install.
    $process=Start-Process -FilePath (Join-Path $output "innosetup-$($lock.inno.version).exe") -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/PORTABLE=1',
        ('/DIR="'+$inno+'"'),('/LOG="'+(Join-Path $output 'inno-portable.log')+'"'))
    if ($process.ExitCode) { throw "Inno portable extraction failed: $($process.ExitCode)" }
}
if ((Get-FileHash -LiteralPath (Join-Path $inno 'ISCC.exe')).Hash -ne $lock.inno.compilerSha256) { throw 'Wrong Inno compiler bytes' }
Write-Output "Pinned release tools: $output"
