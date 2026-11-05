# Static only: never executes inspected scripts or visits external URLs.
[CmdletBinding()]
param([string]$Root = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
$issues = [Collections.Generic.List[string]]::new()
function Issue($path, $line, $message) { $issues.Add("${path}:${line}: $message") }
function Read-Prose([string]$path) {
    $lines = [IO.File]::ReadAllLines($path)
    $fence = ''; $size = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s{0,3}(`{3,}|~{3,})') {
            $mark = $Matches[1]
            if (-not $fence) { $fence = $mark.Substring(0,1); $size = $mark.Length }
            elseif ($mark.StartsWith($fence) -and $mark.Length -ge $size) { $fence = '' }
            [pscustomobject]@{ line = $i + 1; text = '' }; continue
        }
        [pscustomobject]@{ line = $i + 1; text = $(if ($fence) { '' } else { $line }) }
    }
}
function Get-Anchors([string]$path) {
    $seen = @{}; $anchors = [Collections.Generic.List[string]]::new()
    foreach ($row in (Read-Prose $path)) {
        if ($row.text -match '^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$') {
            $slug = $Matches[1].ToLowerInvariant() -replace '<[^>]*>', '' -replace '[^\p{L}\p{M}\p{N}_\-\s]', '' -replace '\s', '-'
            $anchor = $slug
            if ($seen.ContainsKey($slug)) { $seen[$slug]++; $anchor += '-' + $seen[$slug] } else { $seen[$slug] = 0 }
            $anchors.Add($anchor)
        }
        foreach ($m in [regex]::Matches($row.text, '<a\s+(?:name|id)=["'']([^"'']+)["'']')) { $anchors.Add($m.Groups[1].Value) }
    }
    return ,$anchors
}
function Check-Link($path, $line, [string]$target) {
    if ($target -match '^(?:[a-zA-Z][\w+.-]*:|//)') { return }
    $target = [Uri]::UnescapeDataString($target)
    $parts = $target.Split('#', 2)
    $file = $parts[0].Split('?', 2)[0]
    try { $resolved = if ($file) { [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($path)) $file)) } else { $path } }
    catch { Issue $path $line "Invalid link: $target"; return }
    if (-not (Test-Path -LiteralPath $resolved)) { Issue $path $line "Missing target: $target"; return }
    if ($parts.Count -eq 2 -and $parts[1] -and [IO.Path]::GetExtension($resolved) -eq '.md') {
        if ($parts[1] -notin (Get-Anchors $resolved)) { Issue $path $line "Missing fragment: $target" }
    }
}
try {
    # Scope is explicit: product/course prose is not Harness-owned. No Git dependency,
    # no traversal into bin/obj/artifacts or directory links/reparse points.
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    function Collect([string]$directory) {
        foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($item.PSIsContainer) { if ($item.Name -notin @('bin','obj','artifacts','.git')) { Collect $item.FullName } }
            elseif ($item.Extension -in @('.md','.ps1','.psm1','.psd1')) { $files.Add($item) }
        }
    }
    Collect (Join-Path $Root 'harness')
    foreach ($name in @('README.md', 'AGENTS.md')) {
        $path = Join-Path $Root $name
        if (Test-Path -LiteralPath $path) { $files.Add([IO.FileInfo]$path) }
    }
    foreach ($file in $files) {
        $path = $file.FullName
        if ($file.Extension -ne '.md') {
            $tokens = $null; $errors = $null
            $null = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
            foreach ($e in $errors) { Issue $path $e.Extent.StartLineNumber "PowerShell: $($e.Message)" }
            continue
        }
        $rows = @(Read-Prose $path); $refs = @{}
        foreach ($row in $rows) {
            if ($row.text -match '^\s*\[([^\]]+)\]:\s*(?:<([^>]+)>|(\S+))') {
                $refs[$Matches[1]] = if ($Matches[2]) { $Matches[2] } else { $Matches[3] }
            }
        }
        foreach ($row in $rows) {
            # Inline code and historical commands in fenced blocks are examples,
            # not active documentation links. Inline HTML comments aren't targets.
            $text = $row.text -replace '`[^`]*`', '' -replace '<!--.*?-->', ''
            foreach ($m in [regex]::Matches($text, '\[[^\]]*\]\(\s*(?:<([^>]+)>|([^\s)]+(?:\([^)]*\)[^\s)]*)*))\s*(?:"[^"]*")?\s*\)')) {
                $target = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
                Check-Link $path $row.line $target
            }
            foreach ($m in [regex]::Matches($text, '\[([^\]]+)\]\[([^\]]*)\]')) {
                $id = if ($m.Groups[2].Value) { $m.Groups[2].Value } else { $m.Groups[1].Value }
                if ($refs.ContainsKey($id)) { Check-Link $path $row.line $refs[$id] }
                else { Issue $path $row.line "Missing reference definition: $id" }
            }
            if ($text -match '^\s*\[([^\]]+)\]:') { Check-Link $path $row.line $refs[$Matches[1]] }
        }
        $content = [IO.File]::ReadAllText($path)
        # Three pre-schema historical plans are deliberately grandfathered. New
        # files cannot bypass required fields by omitting the schema marker.
        $legacy = @('harness/work/plans/HARNESS_STRUCTURE.plan.md','harness/work/plans/HARNESS_AUTONOMY.plan.md','harness/work/plans/D005_KID.Library_STANDALONE_RESTORE.plan.md')
        $relative = [IO.Path]::GetRelativePath($Root, $path).Replace('\', '/')
        if ($file.Name -like '*.plan.md' -and $relative -notin $legacy) {
            if (-not $content.Contains('<!-- harness-plan:v1 -->')) { Issue $path 1 'Required plan schema marker: harness-plan:v1' }
            foreach ($heading in @('Цель','Статус','Границы','Требования и предложения','Приёмка','Проверки','Открытые решения','Журнал','Продолжение','Следующий шаг')) {
                $pattern = '(?ms)^## ' + [regex]::Escape($heading) + '\r?\n(.*?)(?=^## |\z)'
                $match = [regex]::Match($content, $pattern)
                if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value) -or $match.Groups[1].Value -match '<[^!][^>]*>|\bTODO\b|\bTBD\b') {
                    Issue $path 1 "Required plan field missing/unfilled: $heading"
                }
            }
            if ($content -notmatch '(?m)^## Статус\r?\n\s*(proposed|in progress|completed|superseded)\s*\r?$') { Issue $path 1 'Invalid plan status' }
        }
    }
    # Validate the active SDK statement by reference, not by searching historical numbers.
    $global = Get-Content -LiteralPath (Join-Path $Root 'global.json') -Raw | ConvertFrom-Json
    if (-not $global.sdk.version -or -not $global.sdk.rollForward -or $global.test.runner -ne 'Microsoft.Testing.Platform') { Issue 'global.json' 1 'SDK/rollForward/MTP policy missing' }
    $development = Join-Path $Root 'harness/context/DEVELOPMENT.md'
    if (Test-Path -LiteralPath $development) {
        $doc = Get-Content -LiteralPath $development -Raw
        if ($doc -notmatch 'harness-sdk-policy: global.json' -or $doc -match '\*\*\.NET 8\.0 SDK\*\* или выше') { Issue $development 1 'Active SDK requirements must reference global.json policy' }
    }
    foreach ($issue in $issues) { [Console]::Error.WriteLine($issue) }
    if ($issues.Count) { Write-Output "FAIL static: $($issues.Count) issue(s)"; exit 1 }
    Write-Output "PASS static: $($files.Count) Markdown/PowerShell files; local links/fragments, routing links, v1 plans, SDK policy"; exit 0
}
catch { [Console]::Error.WriteLine("STATIC ERROR: $($_.Exception.Message)"); exit 4 }
