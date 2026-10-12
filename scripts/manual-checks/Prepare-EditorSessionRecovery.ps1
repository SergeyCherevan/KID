[CmdletBinding()]
# Close KID before preparing a case and before restoring the original session.
# Run from the repository root with -Case Autosave, RestoreFixture, NullTabs, or Restore.
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Autosave', 'RestoreFixture', 'NullTabs', 'Restore')]
    [string]$Case
)

$settingsDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) 'KID'
$sessionPath = Join-Path $settingsDirectory 'editor-session.json'
$referencePath = Join-Path $settingsDirectory '.codex-manual-session-reference.cs'
$statePath = Join-Path $settingsDirectory '.codex-manual-session-state.json'
$sessionBackupPath = Join-Path $settingsDirectory '.codex-manual-session-backup.json'
$referenceBackupPath = Join-Path $settingsDirectory '.codex-manual-reference-backup.cs'

if ($Case -eq 'Restore') {
    if (-not (Test-Path -LiteralPath $statePath)) {
        throw "No prepared manual-check state exists at '$statePath'."
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.HadOriginalSession) {
        if (-not (Test-Path -LiteralPath $sessionBackupPath)) {
            throw "The original editor session backup is missing: '$sessionBackupPath'."
        }
        Move-Item -LiteralPath $sessionBackupPath -Destination $sessionPath -Force
    }
    elseif (Test-Path -LiteralPath $sessionPath) {
        Remove-Item -LiteralPath $sessionPath -Force
    }

    if ($state.HadOriginalReferenceFile) {
        if (-not (Test-Path -LiteralPath $referenceBackupPath)) {
            throw "The original reference file backup is missing: '$referenceBackupPath'."
        }
        Move-Item -LiteralPath $referenceBackupPath -Destination $referencePath -Force
    }
    elseif (Test-Path -LiteralPath $referencePath) {
        Remove-Item -LiteralPath $referencePath -Force
    }

    Remove-Item -LiteralPath $statePath -Force
    Write-Output 'Restored the original editor session and removed manual-check files.'
    return
}

if (Test-Path -LiteralPath $statePath) {
    throw "A manual-check backup is already active. Restore it first with -Case Restore."
}
if ($Case -eq 'RestoreFixture' -and (Test-Path -LiteralPath $referencePath)) {
    throw "The reference file already exists; refusing to replace '$referencePath'."
}

New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
$hadOriginalSession = Test-Path -LiteralPath $sessionPath
$hadOriginalReferenceFile = Test-Path -LiteralPath $referencePath
if ((Test-Path -LiteralPath $sessionBackupPath) -or (Test-Path -LiteralPath $referenceBackupPath)) {
    throw 'A manual-check backup file already exists; refusing to overwrite it.'
}
if ($hadOriginalSession) {
    Copy-Item -LiteralPath $sessionPath -Destination $sessionBackupPath
}
if ($hadOriginalReferenceFile) {
    Copy-Item -LiteralPath $referencePath -Destination $referenceBackupPath
}

$state = @{
    HadOriginalSession = $hadOriginalSession
    HadOriginalReferenceFile = $hadOriginalReferenceFile
} | ConvertTo-Json -Compress
[IO.File]::WriteAllText($statePath, $state, [Text.UTF8Encoding]::new($false))

if ($Case -eq 'NullTabs') {
    $json = '{"Version":1,"ActiveTabIndex":0,"Tabs":null}'
    [IO.File]::WriteAllText($sessionPath, $json, [Text.UTF8Encoding]::new($false))
    Write-Output "Prepared Tabs:null in '$sessionPath'. Start KID; expect the session-restore error and a working default tab."
}
elseif ($Case -eq 'Autosave') {
    if (Test-Path -LiteralPath $sessionPath) {
        Remove-Item -LiteralPath $sessionPath -Force
    }
    Write-Output 'Prepared a clean session start. Open KID, type a unique marker in a new unsaved tab, wait at least 6 seconds, then end KID from Task Manager without closing the window normally. Reopen KID and check that the marker and modified-tab indicator return.'
}
else {
    $referenceCode = 'Console.WriteLine("KID_SESSION_DISK_TAB_MARKER");'
    [IO.File]::WriteAllText($referencePath, $referenceCode, [Text.UTF8Encoding]::new($false))
    $session = @{
        Version = 1
        ActiveTabIndex = 99
        Tabs = @(
            @{
                FilePath = '/NewFile.cs'
                Content = 'KID_SESSION_UNSAVED_TAB_MARKER'
                SavedContent = 'KID_SESSION_BASELINE_MARKER'
            },
            @{
                FilePath = $referencePath
                Content = $referenceCode
                SavedContent = $referenceCode
            }
        )
    } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($sessionPath, $session, [Text.UTF8Encoding]::new($false))
    Write-Output "Prepared two tabs in '$sessionPath'. Start KID; expect both markers, the first tab marked modified, and the last tab active (index clamped from 99)."
}

Write-Output "After closing KID, restore the original with: ./scripts/manual-checks/Prepare-EditorSessionRecovery.ps1 -Case Restore"
