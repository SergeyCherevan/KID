param([Parameter(Mandatory)][string]$OutputDirectory)
. (Join-Path $PSScriptRoot '../../../tools/Release.Common.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$root=Assert-ReleasePath $OutputDirectory (Join-Path $repo 'artifacts/harness')
if (Test-Path -LiteralPath $root) { throw 'Publish matrix requires a new output' }
New-Item -ItemType Directory $root | Out-Null
$releaseCommands=@();$code=1
$result=[ordered]@{status='failed';checks=@()}
Push-Location $repo
try {
    $project=Join-Path $repo 'KID.WPF.IDE/KID.WPF.IDE.csproj'
    $common=@('publish',$project,'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:PublishAot=false','-p:PublishReadyToRun=false')
    foreach ($case in @('clean','no-build','repeat','patch-12','patch-13')) {
        $output=Join-Path $root $case;$log=Join-Path $root "$case.json"
        if ($case -eq 'no-build') {
            # Deliberately poison obj. --no-build must regenerate, never reuse it.
            [IO.File]::WriteAllText((Join-Path $repo 'KID.WPF.IDE/obj/Release/net10.0-windows/win-x64/kid-framework-assemblies.v1.json'),'{"stale":true}')
        }
        $extra=if ($case -in @('no-build','repeat')) { @('--no-build') } elseif ($case -eq 'patch-12') { @('-p:RuntimeFrameworkVersion=10.0.12') } elseif ($case -eq 'patch-13') { @('-p:RuntimeFrameworkVersion=10.0.13') } else { @() }
        Invoke-ReleaseProcess 'dotnet' ($common+$extra+@('-o',$output,'-getItem:RuntimePackAsset,ResolvedRuntimePack,ResolvedFileToPublish')) $log
        Invoke-ReleaseProcess 'pwsh' @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Verify-FrameworkPublish.ps1'),'-PublishDirectory',$output,'-PackMetadata',$log,'-ResultPath',(Join-Path $root "$case-result.json")) (Join-Path $root "$case-verify.log")
        $result.checks+="$case publish + independent RuntimeList/hash verifier"
    }
    $hashes=@('clean','no-build','repeat' | ForEach-Object { (Get-FileHash (Join-Path $root "$_/kid-framework-assemblies.v1.json")).Hash })
    if (@($hashes | Sort-Object -Unique).Count -ne 1) { throw 'Repeated/no-build manifest is nondeterministic or stale' }
    foreach ($property in @('PublishSingleFile','PublishTrimmed','PublishAot','PublishReadyToRun')) {
        $log=Join-Path $root "$property.log"
        $exit=0
        & dotnet msbuild $project '-t:KIDValidateCompilationPublish' '-p:Configuration=Release' '-p:TargetFramework=net10.0-windows' '-p:RuntimeIdentifier=win-x64' '-p:SelfContained=true' "-p:${property}=true" *> $log
        $exit=$LASTEXITCODE
        if ($exit -eq 0 -or (Get-Content $log -Raw) -notmatch 'KID compilation profile requires file-based') { throw "Unsupported mode was not rejected by the intended guard: $property" }
        $releaseCommands+=@{executable='dotnet';arguments=@('msbuild','KIDValidateCompilationPublish',"$property=true");exitCode=$exit;log=$log}
        $result.checks+="reject $property"
    }
    $metadata=Get-Content (Join-Path $root 'patch-13.json') -Raw | ConvertFrom-Json
    foreach ($case in @('valid','missing-pack','pack-version','missing-publish','substituted-publish','duplicate-publish','empty-owner','missing-asset')) {
        $document=[xml]'<Project />'
        $import=$document.CreateElement('Import');$import.SetAttribute('Project',(Join-Path $repo 'build/compilation-profile/KID.CompilationProfile.targets'));$null=$document.DocumentElement.AppendChild($import)
        $group=$document.CreateElement('ItemGroup');$null=$document.DocumentElement.AppendChild($group)
        foreach ($kind in @('ResolvedRuntimePack','RuntimePackAsset','ResolvedFileToPublish')) {
            foreach ($item in $metadata.Items.$kind) {
                if ($case -eq 'missing-pack' -and $kind -eq 'ResolvedRuntimePack' -and $item.FrameworkName -eq 'Microsoft.WindowsDesktop.App') { continue }
                if ($case -eq 'empty-owner' -and $kind -eq 'RuntimePackAsset' -and $item.NuGetPackageId -like '*WindowsDesktop*') { continue }
                if ($case -eq 'missing-publish' -and $kind -eq 'ResolvedFileToPublish' -and $item.RelativePath -eq 'System.Console.dll') { continue }
                if ($case -eq 'missing-asset' -and $kind -eq 'RuntimePackAsset' -and $item.DestinationSubPath -eq 'System.Console.dll') { continue }
                $node=$document.CreateElement($kind);$node.SetAttribute('Include',$item.Identity)
                foreach ($field in @('FrameworkName','NuGetPackageId','NuGetPackageVersion','PackageDirectory','RuntimeIdentifier','AssetType','DestinationSubPath','RelativePath')) {
                    if ($item.$field) { $node.SetAttribute($field,[string]$item.$field) }
                }
                if ($case -eq 'pack-version' -and $kind -eq 'ResolvedRuntimePack') { $node.SetAttribute('NuGetPackageVersion','10.0.1') }
                if ($case -eq 'substituted-publish' -and $kind -eq 'ResolvedFileToPublish' -and $item.RelativePath -eq 'System.Console.dll') { $node.SetAttribute('Include',(Join-Path $repo 'KID.WPF.IDE/bin/Release/net10.0-windows/KID.WPF.IDE.dll')) }
                $null=$group.AppendChild($node)
                if ($case -eq 'duplicate-publish' -and $kind -eq 'ResolvedFileToPublish' -and $item.RelativePath -eq 'System.Console.dll') { $copy=$node.CloneNode($true);$copy.SetAttribute('Include',$item.Identity+'.substitute');$null=$group.AppendChild($copy) }
            }
        }
        $target=$document.CreateElement('Target');$target.SetAttribute('Name','TestGenerator');$null=$document.DocumentElement.AppendChild($target)
        $task=$document.CreateElement('KID.Build.GenerateFrameworkManifest')
        foreach ($pair in @{Assets='@(RuntimePackAsset)';Packs='@(ResolvedRuntimePack)';PublishFiles='@(ResolvedFileToPublish)';TargetFramework='net10.0-windows';RuntimeIdentifier='win-x64';OutputPath=(Join-Path $root "$case-manifest.json")}.GetEnumerator()) { $task.SetAttribute($pair.Key,$pair.Value) }
        $null=$target.AppendChild($task)
        $fixture=Join-Path $root "$case.proj";$document.Save($fixture)
        # A stale successful manifest must be removed on generator failure.
        'stale' | Set-Content (Join-Path $root "$case-manifest.json")
        & dotnet msbuild $fixture '-t:TestGenerator' *> (Join-Path $root "generator-$case.log")
        $exit=$LASTEXITCODE
        $releaseCommands+=@{executable='dotnet';arguments=@('msbuild',$fixture,'-t:TestGenerator');exitCode=$exit;log=(Join-Path $root "generator-$case.log")}
        if ($case -eq 'valid') { if ($exit) { throw 'Positive generator fixture failed' } }
        elseif ($exit -eq 0 -or (Test-Path (Join-Path $root "$case-manifest.json"))) { throw "Negative generator fixture accepted or retained stale output: $case" }
        $result.checks+="generator $case"
    }
    $result.status='passed';$code=0
}
catch { $result.error=$_.Exception.Message;[Console]::Error.WriteLine($_.Exception.Message) }
finally { $result.commands=$releaseCommands;$result.exitCode=$code;$result | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $root 'result.json');Pop-Location }
exit $code
