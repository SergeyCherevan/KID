param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$PackMetadata,
    [Parameter(Mandatory)][string]$ResultPath,
    [string]$BaselinePath
)
$ErrorActionPreference = 'Stop'
$result = [ordered]@{ status='failed'; publish=[IO.Path]::GetFullPath($PublishDirectory); checks=@(); exitCode=1 }
try {
    $root = $result.publish
    $metadata = Get-Content -LiteralPath $PackMetadata -Raw | ConvertFrom-Json
    $manifest = Get-Content -LiteralPath (Join-Path $root 'kid-framework-assemblies.v1.json') -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.targetFramework -ne 'net10.0-windows' -or $manifest.runtimeIdentifier -ne 'win-x64') { throw 'Invalid manifest deployment metadata' }
    # Independent source: pack RuntimeList.xml, not RuntimePackAsset or the generated manifest.
    # Windows Desktop overrides three NETCore facades in the full framework graph.
    $expected = @{}
    foreach ($owner in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
        $packs = @($metadata.Items.ResolvedRuntimePack | Where-Object FrameworkName -eq $owner)
        if ($packs.Count -ne 1) { throw "Expected one pack for $owner" }
        $pack = $packs[0]
        $owners = @($manifest.frameworks | Where-Object name -eq $owner)
        if ($owners.Count -ne 1 -or $owners[0].runtimePackVersion -ne $pack.NuGetPackageVersion -or $owners[0].runtimePackId -ne $pack.NuGetPackageId) { throw "Pack identity/version mismatch: $owner" }
        [xml]$list = Get-Content -LiteralPath (Join-Path $pack.PackageDirectory 'data/RuntimeList.xml') -Raw
        foreach ($asset in @($list.FileList.File | Where-Object Type -eq Managed)) {
            $name = [IO.Path]::GetFileName($asset.Path)
            $expected[$name] = @{ owner=$owner; source=(Join-Path $pack.PackageDirectory $asset.Path); name=$asset.AssemblyName; version=$asset.AssemblyVersion; token=$asset.PublicKeyToken }
        }
    }
    if ($expected.Count -ne $manifest.assemblies.Count) { throw "Managed count mismatch: RuntimeList=$($expected.Count), manifest=$($manifest.assemblies.Count)" }
    $seen = @{}
    $baseline = @()
    foreach ($entry in $manifest.assemblies) {
        if (!$expected.ContainsKey($entry.path) -or $seen.ContainsKey($entry.path)) { throw "Unexpected/duplicate manifest entry $($entry.path)" }
        $seen[$entry.path] = $true
        $asset = $expected[$entry.path]
        $target = Join-Path $root $entry.path
        if ($entry.framework -ne $asset.owner -or $entry.name -ne $asset.name -or $entry.version -ne $asset.version -or $entry.publicKeyToken -ne $asset.token) { throw "RuntimeList identity mismatch $($entry.path)" }
        if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse asset $target" }
        $actual = [Reflection.AssemblyName]::GetAssemblyName($target)
        if ($actual.FullName -ne [Reflection.AssemblyName]::GetAssemblyName($asset.source).FullName -or $entry.culture -ne $actual.CultureName) { throw "PE identity mismatch $target" }
        if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $asset.source).Hash) { throw "Publish bytes differ from runtime pack: $target" }
        $baseline += @{path=$target;identity=$actual.FullName}
    }
    if ($BaselinePath) { $baseline | Sort-Object path | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $BaselinePath }
    $result.checks += "RuntimeList full framework identities, owners and byte hashes: $($expected.Count)"
    $result.frameworks = $manifest.frameworks
    $result.status='passed'; $result.exitCode=0
}
catch { $result.error=$_.Exception.Message; [Console]::Error.WriteLine($_.Exception.Message) }
finally {
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath
    Write-Output "Framework publish $($result.status): $ResultPath"
}
exit $result.exitCode
