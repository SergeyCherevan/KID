$ErrorActionPreference = 'Stop'
function Assert-HarnessDataRoot([string]$DataRoot) {
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $boundary = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/harness')) + [IO.Path]::DirectorySeparatorChar
    if (-not [IO.Path]::IsPathFullyQualified($DataRoot)) { throw 'DataRoot must be absolute.' }
    $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($DataRoot))
    if (-not $full.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'DataRoot must be inside this checkout artifacts/harness.' }
    for ($path = $full; $path; $path = [IO.Path]::GetDirectoryName($path)) {
        if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point prohibited: $path" }
    }
    $owner = Join-Path $full '.kid-harness-owner'
    $id = [Guid]::Empty
    if (-not (Test-Path -LiteralPath $owner -PathType Leaf) -or -not [Guid]::TryParse((Get-Content -LiteralPath $owner -Raw).Trim(), [ref]$id)) { throw 'Owned root marker is required.' }
    foreach ($item in (Get-ChildItem -LiteralPath $full -Recurse -Force)) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse target prohibited: $($item.FullName)" }
    }
    return $full
}
function New-HarnessDataRoot {
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $id = [Guid]::NewGuid().ToString('N')
    $root = Join-Path $repo "artifacts/harness/data-$id"
    # Check ancestors before creation; never write through a junction.
    for ($path = [IO.Path]::GetDirectoryName($root); $path; $path = [IO.Path]::GetDirectoryName($path)) {
        if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point prohibited: $path" }
    }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    [IO.File]::WriteAllText((Join-Path $root '.kid-harness-owner'), $id)
    Assert-HarnessDataRoot $root
}
function Remove-HarnessDataRoot([string]$DataRoot, [string]$OwnerId) {
    $root = Assert-HarnessDataRoot $DataRoot
    if ((Get-Content -LiteralPath (Join-Path $root '.kid-harness-owner') -Raw).Trim() -ne $OwnerId) { throw 'Cleanup owner does not match this run.' }
    # Resolved absolute boundary, descendants and marker have all been checked.
    Remove-Item -LiteralPath $root -Recurse -Force
}
Export-ModuleMember -Function Assert-HarnessDataRoot, New-HarnessDataRoot, Remove-HarnessDataRoot
