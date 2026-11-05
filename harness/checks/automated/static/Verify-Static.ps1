# Negative and positive parser fixtures; no product launch, no external requests.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$validator = Join-Path $repo 'harness/tools/Test-HarnessStatic.ps1'
$pwsh = (Get-Process -Id $PID).Path
$workspace = Join-Path $repo ('artifacts/harness/static-fixtures/{0}' -f [Guid]::NewGuid().ToString('N'))
$root = Join-Path $workspace 'checkout with spaces'
[IO.Directory]::CreateDirectory((Join-Path $root 'harness/work/plans')) | Out-Null
function Write-File($path, $text) { [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null; [IO.File]::WriteAllText($path, $text) }
Write-File (Join-Path $root 'global.json') (Get-Content (Join-Path $repo 'global.json') -Raw)
Write-File (Join-Path $root 'harness/work/plans/TASK_TEMPLATE.md') (Get-Content (Join-Path $repo 'harness/work/plans/TASK_TEMPLATE.md') -Raw)
Write-File (Join-Path $root 'harness/space target.md') "# 🧪 Заголовок`n# Repeat`n# Repeat`n<a id=`"custom`"></a>"
$valid = @'
# Fixture
[space](<space target.md#-заголовок>)
[duplicate](space%20target.md#repeat-1)
[custom](<space target.md#custom>)
[external](https://example.invalid/no-fetch)
[ref][good]
[good]: <space target.md>
```powershell
[historical](missing-history.md)
& Broken-Example
```
~~~text
[historical](missing-history2.md)
~~~
`[inline example](missing-inline.md)`
'@
$doc = Join-Path $root 'harness/README.md'
Write-File $doc $valid
function Verify($name, $expected, $diagnostic = '') {
    $output = & $pwsh -NoProfile -File $validator -Root $root 2>&1 | Out-String
    $code = $LASTEXITCODE
    Write-File (Join-Path $workspace "$name.log") $output
    if ($code -ne $expected -or ($diagnostic -and -not $output.Contains($diagnostic))) { throw "$name expected=$expected got=$code : $output" }
    Write-Output "PASS $name exit=$code"
}
try {
    Push-Location $workspace
    Verify 'positive-space-template-history-reference-fragments' 0
    Write-File $doc ($valid + "`n[bad](missing.md)")
    Verify 'missing-link' 1 'README.md:'
    Write-File $doc ($valid + "`n[bad](<space target.md#wrong>)")
    Verify 'bad-fragment' 1 'Missing fragment'
    Write-File $doc $valid
    $script = Join-Path $root 'harness/broken.ps1'
    Write-File $script 'function Broken {'
    Verify 'powershell-syntax' 1 'broken.ps1:'
    # Only our own single fixture file is replaced; no cleanup of arbitrary roots.
    Write-File $script '# valid, never executed'
    $routing = Join-Path $root 'harness/checks/CHECK_ROUTING.md'
    Write-File $routing '[missing map path](../../no-such-subsystem/)'
    Verify 'routing-path' 1 'CHECK_ROUTING.md:'
    Write-File $routing '[existing path](../work/plans/)'
    $plan = Join-Path $root 'harness/work/plans/NEW.plan.md'
    Write-File $plan '# Plan without schema'
    Verify 'missing-plan-schema' 1 'Required plan schema'
    Write-File $plan '<!-- harness-plan:v1 -->'
    Verify 'missing-plan-fields' 1 'Required plan field'
    $filled = "# Task`n<!-- harness-plan:v1 -->`n"
    foreach ($heading in @('Цель','Статус','Границы','Требования и предложения','Приёмка','Проверки','Открытые решения','Журнал','Продолжение','Следующий шаг')) {
        $value = if ($heading -eq 'Статус') { 'in progress' } else { 'Конкретное заполненное значение.' }
        $filled += "## $heading`n$value`n`n"
    }
    Write-File $plan ($filled.Replace('Конкретное заполненное значение.', '<Заполнить>'))
    Verify 'placeholder-plan-fields' 1 'Required plan field'
    Write-File $plan $filled
    Verify 'filled-plan' 0
    $lookalike = Join-Path $root 'harness/work/plans/new/HARNESS_STRUCTURE.plan.md'
    Write-File $lookalike '# New plan cannot inherit a historical exception by name'
    Verify 'legacy-name-at-new-path' 1 'Required plan schema'
    Write-Output "PASS static fixtures; evidence: $workspace"
}
finally { Pop-Location }
