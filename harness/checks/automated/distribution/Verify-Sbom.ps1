param(
    [Parameter(Mandatory)][string]$SbomPath,
    [Parameter(Mandatory)][string]$PayloadDirectory,
    [Parameter(Mandatory)][string]$InventoryPath,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$ResultPath
)
$ErrorActionPreference='Stop'
$result=[ordered]@{status='failed';exitCode=1}
try {
    # Run the pinned SBOM Tool schema/conformance validator before this completeness verifier.
    $sbom=Get-Content -LiteralPath $SbomPath -Raw | ConvertFrom-Json
    $inventory=Get-Content -LiteralPath $InventoryPath -Raw | ConvertFrom-Json
    if ($sbom.spdxVersion -ne 'SPDX-2.2' -or $sbom.dataLicense -ne 'CC0-1.0' -or !$sbom.documentNamespace -or !$sbom.creationInfo) { throw 'Invalid SPDX document metadata' }
    if (!@($sbom.packages | Where-Object { $_.name -eq '.KID' -and $_.versionInfo -eq $Version }).Count) { throw 'Missing .KID root package/version' }
    $expected=@($inventory.components | ForEach-Object { ($_.id+'/'+$_.version).ToLowerInvariant() } | Sort-Object -Unique)
    $actual=@($sbom.packages | Where-Object name -ne '.KID' | ForEach-Object { ($_.name+'/'+$_.versionInfo).ToLowerInvariant() } | Sort-Object -Unique)
    $diff=@(Compare-Object $expected $actual)
    if ($diff.Count) { throw ('SBOM package set differs from published runtime graph: '+($diff | ConvertTo-Json -Compress)) }
    $root=[IO.Path]::GetFullPath($PayloadDirectory);$seen=@{}
    foreach ($file in $sbom.files) {
        $relative=$file.fileName -replace '^\./',''
        $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
        if (!$path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $seen.ContainsKey($relative)) { throw 'Escaping/duplicate SBOM file' }
        $seen[$relative]=$true
        $hashes=@($file.checksums | Where-Object algorithm -eq SHA256)
        if ($hashes.Count -ne 1 -or (Get-FileHash -LiteralPath $path).Hash -ne $hashes[0].checksumValue) { throw "SBOM file hash mismatch: $relative" }
    }
    $files=@(Get-ChildItem $root -Recurse -File | Where-Object { !$_.FullName.StartsWith((Join-Path $root '_manifest')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) })
    foreach ($file in $files) {
        $relative=[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
        if (!$seen.ContainsKey($relative)) { throw "SBOM omits payload file: $relative" }
    }
    $result.status='passed';$result.exitCode=0;$result.packages=$expected.Count;$result.files=$files.Count
}
catch {$result.error=$_.Exception.Message;[Console]::Error.WriteLine($_.Exception.Message)}
finally {$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultPath}
exit $result.exitCode
