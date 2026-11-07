param(
    [Parameter(Mandatory)][string]$PayloadDirectory,
    [Parameter(Mandatory)][string]$PublishMetadata,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$FileVersion,
    [Parameter(Mandatory)][string]$Revision,
    [Parameter(Mandatory)][string]$ResultPath
)
$ErrorActionPreference='Stop'
$result=[ordered]@{status='failed';exitCode=1;version=$Version;revision=$Revision}
try {
    $root=[IO.Path]::GetFullPath($PayloadDirectory)
    $metadata=Get-Content -LiteralPath $PublishMetadata -Raw | ConvertFrom-Json
    $expected=@($metadata.Items.ResolvedFileToPublish | ForEach-Object RelativePath | Sort-Object -Unique)
    if (!$expected.Count) { throw 'Empty publish metadata' }
    $actual=@(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName) } | Sort-Object -Unique)
    $difference=@(Compare-Object $expected $actual)
    if ($difference.Count) { throw ('Missing or unexpected payload file: '+($difference | ConvertTo-Json -Compress)) }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Force) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse payload: $($file.FullName)" }
        if ($file.Name -match '(?i)(KID\.Tests|xunit|testhost|\.trx$|\.log$|settings\.json|editor-session|\.kid-harness-owner|fixture|StandaloneProbe|Data\.Smoke)') { throw "Developer/test data in payload: $($file.Name)" }
    }
    foreach ($required in @('KID.WPF.IDE.exe','KID.WPF.IDE.dll','KID.Library.dll','NAudio.Core.dll','DefaultWindowConfiguration.json','ru/KID.WPF.IDE.resources.dll','uk/KID.WPF.IDE.resources.dll','kid-framework-assemblies.v1.json','coreclr.dll','hostfxr.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $root $required))) { throw "Missing required payload $required" }
    }
    foreach ($name in @('KID.WPF.IDE.dll','KID.Library.dll','KID.WPF.IDE.exe')) {
        $path=Join-Path $root $name
        $info=[Diagnostics.FileVersionInfo]::GetVersionInfo($path)
        if ($info.FileVersion -ne $FileVersion -or $info.ProductVersion -ne "$Version+$Revision") { throw "Binary version/SHA mismatch: $name ($($info.FileVersion)/$($info.ProductVersion))" }
        if ($name.EndsWith('.dll') -and [Reflection.AssemblyName]::GetAssemblyName($path).Version.ToString() -ne '1.0.0.0') { throw "Assembly identity changed: $name" }
    }
    $result.files=@(Get-ChildItem -LiteralPath $root -Recurse -File | Sort-Object FullName | ForEach-Object { @{path=[IO.Path]::GetRelativePath($root,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant();bytes=$_.Length} })
    $result.status='passed';$result.exitCode=0
}
catch { $result.error=$_.Exception.Message;[Console]::Error.WriteLine($_.Exception.Message) }
finally { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath }
exit $result.exitCode
