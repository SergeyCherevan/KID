param(
    [Parameter(Mandatory)][string]$PayloadDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$PackageRoot,
    [switch]$FetchSourceLicenses
)
$ErrorActionPreference='Stop'
$payload=[IO.Path]::GetFullPath($PayloadDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$deps=Get-Content -LiteralPath (Join-Path $payload 'KID.WPF.IDE.deps.json') -Raw | ConvertFrom-Json -AsHashtable
$target=$deps.targets[$deps.runtimeTarget.name]
$components=@(); $missing=@(); $notices=[Collections.Generic.List[string]]::new()
$notices.Add(".KID third-party inventory (generated from the published .deps.json).`nLicensing review is required before distribution.`n")
foreach ($key in @($target.Keys | Sort-Object)) {
    $library=$deps.libraries[$key]
    if ($library.type -notin @('package','runtimepack')) { continue }
    $parts=$key.Split('/'); $id=$parts[0] -replace '^runtimepack\.',''; $version=$parts[1]
    $package=Join-Path $PackageRoot ($id.ToLowerInvariant()+'/'+$version.ToLowerInvariant())
    $nuspec=Join-Path $package ($id.ToLowerInvariant()+'.nuspec')
    if (!(Test-Path -LiteralPath $nuspec)) { $missing+=$key; continue }
    [xml]$xml=Get-Content -LiteralPath $nuspec -Raw
    $meta=$xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
    $licenseNode=$meta.SelectSingleNode('*[local-name()="license"]')
    $copyrightNode=$meta.SelectSingleNode('*[local-name()="copyright"]')
    $repositoryNode=$meta.SelectSingleNode('*[local-name()="repository"]')
    $license=if ($licenseNode) { $licenseNode.InnerText } else { 'NOASSERTION' }
    $assets=@()
    foreach ($kind in @('runtime','native','resources','runtimeTargets')) {
        if ($target[$key].Contains($kind)) { $assets+=@($target[$key][$kind].Keys) }
    }
    $componentDir=Join-Path $OutputDirectory ($id+'/'+$version)
    New-Item -ItemType Directory -Force $componentDir | Out-Null
    Copy-Item -LiteralPath $nuspec -Destination (Join-Path $componentDir 'package.nuspec')
    $texts=@(Get-ChildItem -LiteralPath $package -File | Where-Object Name -Match '(?i)^(license|licence|notice|third.party)')
    if ($licenseNode -and $licenseNode.GetAttribute('type') -eq 'file') {
        $declared=[IO.Path]::GetFullPath((Join-Path $package $license))
        if (!$declared.StartsWith($package+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Escaping license path $key" }
        if (Test-Path -LiteralPath $declared) { $texts+=Get-Item -LiteralPath $declared }
        else { $missing+="$key declared license text" }
    }
    foreach ($text in @($texts | Sort-Object FullName -Unique)) { Copy-Item -LiteralPath $text.FullName -Destination (Join-Path $componentDir $text.Name) }
    $sourceLicense=$null
    if ($texts.Count -eq 0 -and $FetchSourceLicenses -and $repositoryNode -and
        $repositoryNode.GetAttribute('url') -match '^https://github\.com/([^/]+)/([^/]+?)(?:\.git)?/?$' -and
        $repositoryNode.GetAttribute('commit') -match '^[0-9a-fA-F]{40}$') {
        $owner=$Matches[1];$repository=$Matches[2]
        # Re-read captures: the commit regex above deliberately does not determine a URL.
        $null=$repositoryNode.GetAttribute('url') -match '^https://github\.com/([^/]+)/([^/]+?)(?:\.git)?/?$'
        $owner=$Matches[1];$repository=$Matches[2];$commit=$repositoryNode.GetAttribute('commit')
        $cache=Join-Path $OutputDirectory ('source-'+$owner+'-'+$repository+'-'+$commit+'.json')
        try {
            if (!(Test-Path -LiteralPath $cache)) {
                Invoke-RestMethod "https://api.github.com/repos/$owner/$repository/license?ref=$commit" | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $cache
            }
            $response=Get-Content -LiteralPath $cache -Raw | ConvertFrom-Json
            $sourceLicense=$response.html_url
            $licensePath=Join-Path $componentDir 'UPSTREAM-LICENSE.txt'
            [IO.File]::WriteAllBytes($licensePath,[Convert]::FromBase64String($response.content))
            $texts=@(Get-Item -LiteralPath $licensePath)
        }
        catch { Write-Warning "Pinned source license unavailable for ${key}: $($_.Exception.Message)" }
    }
    if ($texts.Count -eq 0 -and $FetchSourceLicenses -and $license -eq 'MIT' -and $copyrightNode -and $copyrightNode.InnerText) {
        # The package declares MIT and its attribution, but old nuspecs may lack
        # a source commit. Preserve that declared attribution with the pinned SPDX text.
        try {
            $sourceLicense='https://raw.githubusercontent.com/spdx/license-list-data/v3.27.0/text/MIT.txt'
            $canonical=Join-Path $OutputDirectory 'spdx-MIT-3.27.0.txt'
            if (!(Test-Path -LiteralPath $canonical)) { Invoke-WebRequest $sourceLicense -OutFile $canonical }
            $text=(Get-Content -LiteralPath $canonical -Raw).Replace('Copyright (c) <year> <copyright holders>',$copyrightNode.InnerText)
            $licensePath=Join-Path $componentDir 'DECLARED-MIT-LICENSE.txt'
            [IO.File]::WriteAllText($licensePath,$text)
            $texts=@(Get-Item -LiteralPath $licensePath)
        }
        catch { Write-Warning "Declared MIT text unavailable for ${key}: $($_.Exception.Message)" }
    }
    $components+=@{id=$id;version=$version;license=$license;assets=$assets;licenseTexts=@($texts.Name);sourceLicense=$sourceLicense;copyright=if ($copyrightNode) {$copyrightNode.InnerText} else {''}}
    $notices.Add("$id $version`nLicense: $license`n$(if ($copyrightNode) {$copyrightNode.InnerText})`n")
    if ($texts.Count -eq 0) { $missing+="$key license text/review" }
}
$notices | Set-Content -LiteralPath (Join-Path $OutputDirectory 'THIRD-PARTY-NOTICES.txt')
[ordered]@{schemaVersion=1;source='published runtime target in KID.WPF.IDE.deps.json';components=$components;unreviewed=$missing;reviewComplete=$false} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'components.json')
Write-Output "Payload inventory: $($components.Count) components, $($missing.Count) unresolved license texts/reviews. Not a license grant or final notices."
