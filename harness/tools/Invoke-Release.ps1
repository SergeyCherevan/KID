param(
    [ValidateSet('Unsigned','Signed')][string]$Mode='Unsigned',
    [string]$OutputDirectory,
    [string]$ToolsDirectory,
    [string]$ReleaseMetadata,
    [string]$CIResult,
    [switch]$AllowDirty,
    [switch]$SkipChecks
)
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$releaseCommands=@();$lockStream=$null;$code=1
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repo ('artifacts/harness/release-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)) }
$output=Assert-ReleasePath $OutputDirectory (Join-Path $repo 'artifacts/harness')
if (Test-Path -LiteralPath $output) { throw 'Every candidate requires a new, empty output directory' }
New-Item -ItemType Directory -Path $output | Out-Null
$result=[ordered]@{schemaVersion=1;mode=$Mode;status='failed';accepted=$false;commands=@();startedUtc=[DateTime]::UtcNow.ToString('o')}
Push-Location $repo
try {
    $safeDirectory='safe.directory='+$repo.Replace('\','/')
    $revision=(& git -c $safeDirectory rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE) { throw 'Cannot identify source revision' }
    $dirty=@(& git -c $safeDirectory status --porcelain)
    if ($LASTEXITCODE) { throw 'Cannot inspect source state' }
    $result.revision=$revision;$result.dirty=$dirty.Count -gt 0
    $result.sdk=(& dotnet --version).Trim();if ($LASTEXITCODE) { throw 'SDK selection failed' }
    $policy=Get-Content (Join-Path $repo 'build/distribution/release-policy.json') -Raw | ConvertFrom-Json
    if ($result.sdk -ne $policy.sdkVersion) { throw 'Release SDK differs from the pinned and verified SDK' }
    $result.os=[Runtime.InteropServices.RuntimeInformation]::OSDescription;$result.powershell=$PSVersionTable.PSVersion.ToString()
    if ($result.dirty -and (!$AllowDirty -or $Mode -eq 'Signed')) { throw 'Release requires a clean checkout (AllowDirty is for unsigned development only)' }
    [xml]$props=Get-Content (Join-Path $repo 'build/Version.props') -Raw
    $version=[string]$props.Project.PropertyGroup.VersionPrefix
    if ($props.Project.PropertyGroup.VersionSuffix) { $version+='-'+$props.Project.PropertyGroup.VersionSuffix }
    $fileVersion=[string]$props.Project.PropertyGroup.FileVersion
    if ($version -notmatch '^0\.1\.0(?:-rc\.[1-9]\d*)?$' -or $fileVersion -notmatch '^0\.1\.0\.[1-9]\d*$' -or
        [int]$fileVersion.Split('.')[-1] -gt 65535 -or
        ($version.Contains('-rc.') -and $version.Split('.')[-1] -ne $fileVersion.Split('.')[-1])) { throw 'Inconsistent candidate version policy' }
    if ((Get-Content (Join-Path $repo 'CHANGELOG.md') -Raw) -notmatch [regex]::Escape($version)) { throw 'Changelog does not identify the candidate' }
    $result.version=$version;$result.fileVersion=$fileVersion;$result.rid='win-x64'
    $metadata=$null
    if ($ReleaseMetadata) { $metadata=Get-Content -LiteralPath $ReleaseMetadata -Raw | ConvertFrom-Json }
    if ($Mode -eq 'Signed') {
        if ($SkipChecks -or $AllowDirty) { throw 'Signed mode forbids bypassing clean checkout/checks' }
        foreach ($field in @('publisherSubject','certificateThumbprint','timestampUrl','supplier','namespaceUri','license','copyrightHolder','securityContact','distributionChannel','retentionDays')) {
            if (!$metadata -or !$metadata.$field) { throw "Author decision/access required: $field" }
        }
        if ($metadata.certificateThumbprint -notmatch '^[0-9a-fA-F]{40}$' -or
            ([Uri]$metadata.timestampUrl).Scheme -ne 'https' -or ([Uri]$metadata.namespaceUri).Scheme -ne 'https') { throw 'Invalid signing/SBOM metadata' }
        foreach ($name in @('LICENSE','SECURITY.md')) { if (!(Test-Path (Join-Path $repo $name))) { throw "Required approved document missing: $name" } }
        if (!$metadata.noticesReviewed -or !$metadata.materialRightsReviewed) { throw 'Final license/notices/material rights review required' }
        if (!$env:KID_SIGNTOOL_PATH -or !(Test-Path -LiteralPath $env:KID_SIGNTOOL_PATH)) { throw 'Provision the selected signing backend; Windows signtool adapter is available but not preconfigured' }
    }
    if ($CIResult) {
        $ci=Get-Content -LiteralPath $CIResult -Raw | ConvertFrom-Json
        if ($ci.status -ne 'passed' -or $ci.revision -ne $revision -or $ci.dirty) { throw 'CI evidence is not a clean successful run for this revision' }
        $result.ciEvidence=(Get-FileHash -LiteralPath $CIResult).Hash
    }
    elseif (!$SkipChecks) {
        Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Invoke-CI.ps1')) (Join-Path $output 'ci.log')
    }
    $result.checksSkipped=[bool]$SkipChecks
    # Share the existing runner lock only after the runner has finished (no nested lock).
    $lockStream=[IO.File]::Open((Join-Path $repo 'artifacts/harness/runner.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if (!$ToolsDirectory) { $ToolsDirectory=Join-Path $repo 'artifacts/harness/release-tools' }
    $tools=Assert-ReleasePath $ToolsDirectory (Join-Path $repo 'artifacts/harness')
    Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Initialize-ReleaseTools.ps1'),'-OutputDirectory',$tools) (Join-Path $output 'tools.log')
    $payload=Join-Path $output 'publish';$publishLog=Join-Path $output 'publish.json'
    Invoke-ReleaseProcess 'dotnet' @('publish','KID.WPF.IDE/KID.WPF.IDE.csproj','-c','Release','-r','win-x64','--self-contained','true',
        '-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:PublishAot=false','-p:PublishReadyToRun=false',
        '-p:NuGetAudit=true','-p:NuGetAuditMode=all','-p:NuGetAuditLevel=high',('-p:SourceRevisionId='+$revision),('-p:RuntimeFrameworkVersion='+$policy.runtimeVersion),
        '-o',$payload,'-getItem:RuntimePackAsset,ResolvedRuntimePack,ResolvedFileToPublish') $publishLog
    $checks=Join-Path $repo 'harness/checks/automated/distribution'
    Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $checks 'Verify-FrameworkPublish.ps1'),'-PublishDirectory',$payload,'-PackMetadata',$publishLog,'-ResultPath',(Join-Path $output 'framework-result.json')) (Join-Path $output 'framework.log')
    Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $checks 'Verify-Payload.ps1'),'-PayloadDirectory',$payload,'-PublishMetadata',$publishLog,'-Version',$version,'-FileVersion',$fileVersion,'-Revision',$revision,'-ResultPath',(Join-Path $output 'payload-result.json')) (Join-Path $output 'payload.log')
    Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Test-ReleaseVulnerabilities.ps1'),'-PayloadDirectory',$payload,'-OutputDirectory',(Join-Path $output 'scan')) (Join-Path $output 'scan.log')
    $packageRoot=(& dotnet nuget locals global-packages --list) -replace '^.*?:\s*',''
    Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $PSScriptRoot 'New-DistributionInventory.ps1'),'-PayloadDirectory',$payload,'-OutputDirectory',(Join-Path $output 'notices-review'),'-PackageRoot',$packageRoot.Trim(),'-FetchSourceLicenses') (Join-Path $output 'notices.log')
    $inventory=Get-Content (Join-Path $output 'notices-review/components.json') -Raw | ConvertFrom-Json
    if ($Mode -eq 'Signed' -and $inventory.unreviewed.Count -ne 0) { throw 'Missing third-party license texts; final notices cannot be accepted' }
    # Assemble the staging copy once. The compiler never republishes or changes this payload.
    $stage=Join-Path $output 'payload';New-Item -ItemType Directory $stage | Out-Null
    Copy-Item -Path (Join-Path $payload '*') -Destination $stage -Recurse
    $symbols=Join-Path $output 'symbols';New-Item -ItemType Directory $symbols | Out-Null
    foreach ($symbol in Get-ChildItem $stage -Recurse -Filter *.pdb) {
        $relative=[IO.Path]::GetRelativePath($stage,$symbol.FullName);$destination=Join-Path $symbols $relative
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Move-Item -LiteralPath $symbol.FullName -Destination $destination
    }
    foreach ($lesson in Get-ChildItem (Join-Path $repo 'course') -Directory) {
        $lessonOutput=Join-Path $stage ('course/'+$lesson.Name);New-Item -ItemType Directory -Force $lessonOutput | Out-Null
        Copy-Item -LiteralPath (Join-Path $lesson.FullName 'Lesson.md') -Destination $lessonOutput
        foreach ($directory in @('Scripts','Images')) {
            if (Test-Path (Join-Path $lesson.FullName $directory)) { Copy-Item -LiteralPath (Join-Path $lesson.FullName $directory) -Destination $lessonOutput -Recurse }
        }
    }
    Copy-Item -LiteralPath (Join-Path $repo 'CHANGELOG.md') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $output 'notices-review') -Destination (Join-Path $stage 'third-party') -Recurse
    Copy-Item -LiteralPath (Join-Path $output 'notices-review/THIRD-PARTY-NOTICES.txt') -Destination $stage
    if ($Mode -eq 'Signed') {
        $inventory.reviewComplete=$true
        $inventory | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $stage 'third-party/components.json')
        foreach ($notice in @((Join-Path $stage 'THIRD-PARTY-NOTICES.txt'),(Join-Path $stage 'third-party/THIRD-PARTY-NOTICES.txt'))) {
            (Get-Content -LiteralPath $notice -Raw).Replace('Licensing review is required before distribution.','License texts and attribution reviewed for this candidate.') | Set-Content -LiteralPath $notice
        }
        Copy-Item -LiteralPath (Join-Path $repo 'LICENSE'),(Join-Path $repo 'SECURITY.md') -Destination $stage
        foreach ($name in @('KID.WPF.IDE.exe','KID.WPF.IDE.dll','KID.Library.dll','ru/KID.WPF.IDE.resources.dll','uk/KID.WPF.IDE.resources.dll')) {
            $path=Join-Path $stage $name
            Invoke-ReleaseProcess $env:KID_SIGNTOOL_PATH @('sign','/sha1',$metadata.certificateThumbprint,'/fd','SHA256','/tr',$metadata.timestampUrl,'/td','SHA256',$path) (Join-Path $output ($name.Replace('/','-')+'.sign.log'))
            Assert-Authenticode $path $metadata.publisherSubject $env:KID_SIGNTOOL_PATH (Join-Path $output ($name.Replace('/','-')+'.verify.log'))
        }
    }
    else { 'Unsigned development candidate; licensing, trust, external CI and Windows acceptance remain open.' | Set-Content (Join-Path $stage 'UNSIGNED-DEVELOPMENT.txt') }
    $sbom=Join-Path $tools 'sbom-tool-win-x64.exe'
    if ($metadata -and $metadata.supplier -and $metadata.namespaceUri) {
        # Only publish-specific component inputs are exposed to the detector, never the solution graph.
        $components=Join-Path $output 'sbom-components';New-Item -ItemType Directory $components | Out-Null
        Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $PSScriptRoot 'New-SbomComponents.ps1'),'-InventoryPath',(Join-Path $output 'notices-review/components.json'),'-OutputDirectory',$components) (Join-Path $output 'sbom-components.log')
        Invoke-ReleaseProcess $sbom @('generate','-b',$stage,'-bc',$components,'-pn','.KID','-pv',$version,'-ps',$metadata.supplier,'-nsb',$metadata.namespaceUri,'-mi','SPDX:2.2','-F','false') (Join-Path $output 'sbom-generate.log')
        Invoke-ReleaseProcess $sbom @('validate','-b',$stage,'-o',(Join-Path $output 'sbom-validation.json'),'-mi','SPDX:2.2','-n','true','-F','false') (Join-Path $output 'sbom-validate.log')
        Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $checks 'Verify-Sbom.ps1'),'-SbomPath',(Join-Path $stage '_manifest/spdx_2.2/manifest.spdx.json'),'-PayloadDirectory',$stage,'-InventoryPath',(Join-Path $output 'notices-review/components.json'),'-Version',$version,'-ResultPath',(Join-Path $output 'sbom-result.json')) (Join-Path $output 'sbom-completeness.log')
    }
    elseif ($Mode -eq 'Signed') { throw 'Missing approved SBOM supplier/namespace' }
    $toolLock=Get-Content (Join-Path $repo 'build/distribution/tools.lock.json') -Raw | ConvertFrom-Json
    $iscc=Join-Path $tools "inno-$($toolLock.inno.version)/ISCC.exe"
    $installerDir=Join-Path $output 'installer'
    $innoArgs=@("/DPayloadDir=$stage","/DOutputDir=$installerDir","/DProductVersion=$version","/DNumericVersion=$fileVersion")
    if ($Mode -eq 'Signed') {
        $signCommand='"'+$env:KID_SIGNTOOL_PATH+'" sign /sha1 '+$metadata.certificateThumbprint+' /fd SHA256 /tr '+$metadata.timestampUrl+' /td SHA256 $f'
        $innoArgs+=@('/DProductionSigned',"/DPublisher=$($metadata.publisherSubject)","/Skid=$signCommand")
    }
    $innoArgs+=Join-Path $repo 'build/distribution/KID.iss'
    Invoke-ReleaseProcess $iscc $innoArgs (Join-Path $output 'installer.log')
    $installer=Join-Path $installerDir "KID-$version-win-x64-setup.exe"
    $installerVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($installer)
    if ($installerVersion.FileVersion.Trim() -ne $fileVersion -or $installerVersion.ProductVersion.Trim() -ne $version) { throw 'Installer binary version mismatch' }
    if ($Mode -eq 'Signed') { Assert-Authenticode $installer $metadata.publisherSubject $env:KID_SIGNTOOL_PATH (Join-Path $output 'installer-signature.log') }
    $files=@(Get-ChildItem $stage -Recurse -File | Sort-Object FullName | ForEach-Object { @{path=[IO.Path]::GetRelativePath($stage,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} })
    $manifest=[ordered]@{schemaVersion=1;version=$version;fileVersion=$fileVersion;revision=$revision;dirty=$result.dirty;rid='win-x64';sdk=$result.sdk;frameworks=(Get-Content (Join-Path $payload 'kid-framework-assemblies.v1.json') -Raw | ConvertFrom-Json).frameworks;mode=$Mode;accepted=$false;installer=@{name=[IO.Path]::GetFileName($installer);sha256=(Get-FileHash $installer).Hash.ToLowerInvariant()};payload=$files;openAcceptance=@('clean Windows 11 without SDK/shared runtime','same-candidate install/upgrade/uninstall/recovery/UI','external CI archive verification')}
    $manifest | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'release-manifest.json')
    Compress-Archive -Path $stage,$installerDir,(Join-Path $output 'release-manifest.json') -DestinationPath (Join-Path $output 'candidate.zip')
    Compress-Archive -Path (Join-Path $symbols '*') -DestinationPath (Join-Path $output 'symbols.zip')
    $result.status='built-not-accepted';$result.installer=$manifest.installer;$code=0
}
catch { $result.error=$_.Exception.Message;[Console]::Error.WriteLine($_.Exception.Message) }
finally {
    if ($lockStream) { $lockStream.Dispose() }
    $result.commands=$releaseCommands;$result.exitCode=$code;$result.endedUtc=[DateTime]::UtcNow.ToString('o')
    $result | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $output 'release-result.json')
    Pop-Location
    Write-Output "Release $($result.status): $output"
}
exit $code
