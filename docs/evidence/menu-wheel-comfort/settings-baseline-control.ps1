$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$source = Join-Path $root 'Assets/_Project/Scripts/UI/UtilityMenuController.cs'
$original = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'UtilityMenuController.before.cs.txt'))
$fixed = [IO.File]::ReadAllBytes($source)
try {
    [IO.File]::WriteAllBytes($source, $original)
    & (Join-Path $root 'Tools/RunEvidenceDisplayChecks.ps1') -Suites @('SettingsPauseMenuTest') -Attempt 'menu-wheel-settings-baseline-control-2026-10-18'
}
finally {
    $actual = [IO.File]::ReadAllBytes($source)
    if ([Convert]::ToBase64String($actual) -ne [Convert]::ToBase64String($original)) {
        throw 'Menu source changed during control; refusing to overwrite unexpected edits.'
    }
    [IO.File]::WriteAllBytes($source, $fixed)
    Write-Output 'Restored the exact verified menu comfort source.'
}
