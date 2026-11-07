param(
    [Parameter(Mandatory)][string]$PayloadDirectory,
    [Parameter(Mandatory)][string]$PublishMetadata,
    [Parameter(Mandatory)][string]$OutputDirectory
)
. (Join-Path $PSScriptRoot '../../../tools/Release.Common.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$root=Assert-ReleasePath $OutputDirectory (Join-Path $repo 'artifacts/harness')
if (Test-Path -LiteralPath $root) { throw 'Gate fixtures require a new output' }
New-Item -ItemType Directory $root | Out-Null
$result=[ordered]@{status='failed';exitCode=1;checks=@()}
try {
    $fixture=Join-Path $root 'payload';New-Item -ItemType Directory $fixture | Out-Null
    Copy-Item -Path (Join-Path $PayloadDirectory '*') -Destination $fixture -Recurse
    $info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $fixture 'KID.WPF.IDE.dll'))
    $version,$revision=$info.ProductVersion.Split('+',2)
    $common=@('-NoProfile','-File',(Join-Path $PSScriptRoot 'Verify-Payload.ps1'),'-PayloadDirectory',$fixture,'-PublishMetadata',[IO.Path]::GetFullPath($PublishMetadata),'-FileVersion',$info.FileVersion,'-Revision',$revision)
    foreach ($case in @('valid','wrong-version','missing-file','unexpected-file')) {
        if ($case -eq 'missing-file') { Move-Item -LiteralPath (Join-Path $fixture 'DefaultWindowConfiguration.json') -Destination (Join-Path $root 'held-config.json') }
        if ($case -eq 'unexpected-file') { 'owned fixture' | Set-Content (Join-Path $fixture 'unexpected.dll') }
        $testVersion=if ($case -eq 'wrong-version') {'99.0.0-rc.1'} else {$version}
        & pwsh @common -Version $testVersion -ResultPath (Join-Path $root "$case-result.json") *> (Join-Path $root "$case.log")
        $exit=$LASTEXITCODE
        if (($case -eq 'valid' -and $exit -ne 0) -or ($case -ne 'valid' -and $exit -eq 0)) { throw "Payload gate fixture failed: $case exit=$exit" }
        $result.checks+=@{name=$case;exitCode=$exit}
        if ($case -eq 'missing-file') { Move-Item -LiteralPath (Join-Path $root 'held-config.json') -Destination (Join-Path $fixture 'DefaultWindowConfiguration.json') }
    }
    $signature=Get-AuthenticodeSignature -LiteralPath (Join-Path $fixture 'KID.WPF.IDE.exe')
    if ($signature.Status -eq 'Valid') { throw 'Expected an unsigned fixture' }
    $rejected=$false
    try { Assert-Authenticode (Join-Path $fixture 'KID.WPF.IDE.exe') 'fixture-only' 'not-called' (Join-Path $root 'signature.log') } catch { $rejected=$true }
    if (!$rejected) { throw 'Unsigned payload was accepted' }
    $result.checks+=@{name='unsigned-signature';rejected=$true}
    '{}' | Set-Content (Join-Path $root 'invalid-sbom.json')
    '{"components":[]}' | Set-Content (Join-Path $root 'empty-inventory.json')
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Verify-Sbom.ps1') -SbomPath (Join-Path $root 'invalid-sbom.json') -PayloadDirectory $fixture -InventoryPath (Join-Path $root 'empty-inventory.json') -Version $version -ResultPath (Join-Path $root 'sbom-result.json') *> (Join-Path $root 'sbom.log')
    if ($LASTEXITCODE -eq 0) { throw 'Invalid SBOM accepted' }
    $result.checks+=@{name='invalid-sbom';rejected=$true}
    # Actual scan refusal evidence belongs to the vulnerable 10.0.13 publish;
    # it is recorded separately, never replaced with a fabricated safe report.
    $result.status='passed';$result.exitCode=0
}
catch {$result.error=$_.Exception.Message;[Console]::Error.WriteLine($_.Exception.Message)}
finally {$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'result.json')}
exit $result.exitCode
