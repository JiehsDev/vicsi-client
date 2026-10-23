$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$scene = Join-Path $root 'Assets/_Project/Scenes/Scenarios/CSI_Environment.unity'
$before = Join-Path $PSScriptRoot 'before/CSI_Environment.unity'
$fixed = [IO.File]::ReadAllBytes($scene)
$original = [IO.File]::ReadAllBytes($before)
try {
    [IO.File]::WriteAllBytes($scene, $original)
    & (Join-Path $root 'Tools/RunEvidenceDisplayChecks.ps1') -Suites @('ScenePhotographyProcedureTest') -Attempt 'exterior-photo-baseline-control-2026-10-18'
}
finally {
    $actual = [IO.File]::ReadAllBytes($scene)
    if ([Convert]::ToBase64String($actual) -ne [Convert]::ToBase64String($original)) {
        throw 'Scene changed during control test; refusing to overwrite unexpected edits.'
    }
    [IO.File]::WriteAllBytes($scene, $fixed)
    Write-Output 'Restored the exact verified station-height fix.'
}
