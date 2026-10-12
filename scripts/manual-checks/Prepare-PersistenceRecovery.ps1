[CmdletBinding()]
# Close KID before preparing a case and before restoring the original settings.
# Run from the repository root with -Case NullFontFamily, DuplicateFontSize, or Restore.
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('NullFontFamily', 'DuplicateFontSize', 'Restore')]
    [string]$Case
)

$settingsDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) 'KID'
$settingsPath = Join-Path $settingsDirectory 'settings.json'
$backupPath = Join-Path $settingsDirectory '.codex-manual-settings-backup.json'
$statePath = Join-Path $settingsDirectory '.codex-manual-settings-state.json'

if ($Case -eq 'Restore') {
    if (-not (Test-Path -LiteralPath $statePath)) {
        throw "No prepared manual-check state exists at '$statePath'."
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.HadOriginalSettings) {
        if (-not (Test-Path -LiteralPath $backupPath)) {
            throw "The original settings backup is missing: '$backupPath'."
        }
        Move-Item -LiteralPath $backupPath -Destination $settingsPath -Force
        Write-Output "Restored original settings to '$settingsPath'."
    }
    else {
        if (Test-Path -LiteralPath $settingsPath) {
            Remove-Item -LiteralPath $settingsPath -Force
        }
        Write-Output 'Removed test settings; no settings.json existed before this check.'
    }

    Remove-Item -LiteralPath $statePath -Force
    return
}

if (Test-Path -LiteralPath $statePath) {
    throw "A manual-check backup is already active. Restore it first with -Case Restore."
}

New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
$hadOriginalSettings = Test-Path -LiteralPath $settingsPath
if ($hadOriginalSettings) {
    if (Test-Path -LiteralPath $backupPath) {
        throw "A backup file already exists; refusing to overwrite '$backupPath'."
    }
    Copy-Item -LiteralPath $settingsPath -Destination $backupPath
}

$state = @{ HadOriginalSettings = $hadOriginalSettings } | ConvertTo-Json -Compress
[IO.File]::WriteAllText($statePath, $state, [Text.UTF8Encoding]::new($false))

$json = switch ($Case) {
    'NullFontFamily' { '{"FontFamily":null}' }
    'DuplicateFontSize' { '{"FontSize":14,"FontSize":22}' }
}
[IO.File]::WriteAllText($settingsPath, $json, [Text.UTF8Encoding]::new($false))
Write-Output "Prepared '$Case' in '$settingsPath'. Start KID and check that defaults load with no crash."
Write-Output "After closing KID, restore the original with: ./scripts/manual-checks/Prepare-PersistenceRecovery.ps1 -Case Restore"
