param([Parameter(Mandatory)][string]$InventoryPath,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$inventory=Get-Content -LiteralPath $InventoryPath -Raw | ConvertFrom-Json
# Component Detection's NuGet detector reads the assets library graph. Supply a
# synthetic, publish-only graph rather than unrelated solution/build packages.
$libraries=[ordered]@{};$target=[ordered]@{}
foreach ($component in $inventory.components) {
    $key=$component.id+'/'+$component.version
    if ($libraries.Contains($key)) { throw "Duplicate component: $key" }
    $libraries[$key]=@{type='package';path=$key.ToLowerInvariant();files=@()}
    $target[$key]=@{type='package'}
}
[ordered]@{version=3;targets=@{'.NETCoreApp,Version=v10.0/win-x64'=$target};libraries=$libraries;projectFileDependencyGroups=@{'.NETCoreApp,Version=v10.0'=@()};packageFolders=@{};project=@{version='0.0.0';restore=@{projectName='KID.PublishedComponents';projectPath='KID.PublishedComponents.csproj';projectStyle='PackageReference'};frameworks=@{net10=@{}}}} | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $OutputDirectory 'project.assets.json')
