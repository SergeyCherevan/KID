param([Parameter(Mandatory)][string]$PayloadDirectory,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$result=[ordered]@{status='failed';exitCode=1;policy='High/Critical fail; unavailable/incomplete source fails; no implicit exceptions';sources=@();findings=@()}
try {
    $sdk=(& dotnet --version).Trim(); if ($LASTEXITCODE) { throw 'SDK unavailable' }
    $dotnetRoot=[IO.Path]::GetDirectoryName((Get-Command dotnet).Source)
    Add-Type -Path (Join-Path $dotnetRoot "sdk/$sdk/NuGet.Versioning.dll")
    $deps=Get-Content -LiteralPath (Join-Path $PayloadDirectory 'KID.WPF.IDE.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    $packages=@($deps.libraries.Keys | Where-Object { $deps.libraries[$_].type -in @('package','runtimepack') } | ForEach-Object {
        $parts=($_ -replace '^runtimepack\.','').Split('/'); @{id=$parts[0].ToLowerInvariant();version=$parts[1]}
    })
    $indexUri='https://api.nuget.org/v3/vulnerabilities/index.json'
    $index=Invoke-RestMethod $indexUri
    $index | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $OutputDirectory 'nuget-index.json')
    if (!$index.Count) { throw 'Empty NuGet vulnerability index' }
    foreach ($page in $index) {
        $uri=[Uri]$page.'@id'
        if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'api.nuget.org') { throw 'Unexpected advisory source' }
        $raw=(Invoke-WebRequest $uri.AbsoluteUri).Content
        $pagePath=Join-Path $OutputDirectory ($page.'@name'+'.json')
        [IO.File]::WriteAllText($pagePath,$raw)
        $advisories=$raw | ConvertFrom-Json -AsHashtable
        $result.sources+=@{uri=$uri.AbsoluteUri;sha256=(Get-FileHash $pagePath).Hash;updated=$page.'@updated'}
        foreach ($package in $packages) {
            if (!$advisories.Contains($package.id)) { continue }
            foreach ($advisory in $advisories[$package.id]) {
                $range=[NuGet.Versioning.VersionRange]::Parse($advisory.versions)
                if ($range.Satisfies([NuGet.Versioning.NuGetVersion]::Parse($package.version))) {
                    $result.findings+=@{id=$package.id;version=$package.version;severity=[int]$advisory.severity;url=$advisory.url;range=$advisory.versions}
                }
            }
        }
    }
    # NuGet pack advisories are supplemented by the .NET security-release feed.
    # A newer security release needs CVE severity adjudication; absence is never called safe.
    $runtimeUri='https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $runtime=Invoke-RestMethod $runtimeUri
    $runtime | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $OutputDirectory 'dotnet-releases.json')
    if (!$runtime.releases.Count -or $runtime.'channel-version' -ne '10.0') { throw 'Invalid .NET release source' }
    $frameworks=(Get-Content (Join-Path $PayloadDirectory 'kid-framework-assemblies.v1.json') -Raw | ConvertFrom-Json).frameworks
    $unassessed=@()
    foreach ($framework in $frameworks) {
        $property=if ($framework.name -eq 'Microsoft.NETCore.App') {'runtime'} else {'windowsdesktop'}
        $known=@($runtime.releases | Where-Object { $_.$property.version -eq $framework.runtimePackVersion })
        if (!$known.Count) { throw "Included runtime not found in release source: $($framework.name) $($framework.runtimePackVersion)" }
        $unassessed+=@($runtime.releases | Where-Object {
            $_.security -eq $true -and $_.$property.version -and
            [NuGet.Versioning.NuGetVersion]::Parse($_.$property.version).CompareTo([NuGet.Versioning.NuGetVersion]::Parse($framework.runtimePackVersion)) -gt 0
        } | ForEach-Object { @{framework=$framework.name;included=$framework.runtimePackVersion;fixedIn=$_.$property.version;cves=$_.'cve-list'} })
    }
    $result.sources+=@{uri=$runtimeUri;sha256=(Get-FileHash (Join-Path $OutputDirectory 'dotnet-releases.json')).Hash}
    $result.packages=$packages;$result.runtimeAdjudicationRequired=$unassessed
    if (@($result.findings | Where-Object severity -ge 2).Count) { throw 'High/Critical advisories affect the payload' }
    if ($unassessed.Count) { throw 'Newer runtime security fixes require CVE applicability/severity review; scan coverage is incomplete' }
    $result.status='passed';$result.exitCode=0
}
catch { $result.error=$_.Exception.Message;[Console]::Error.WriteLine($_.Exception.Message) }
finally { $result | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $OutputDirectory 'scan-result.json') }
exit $result.exitCode
