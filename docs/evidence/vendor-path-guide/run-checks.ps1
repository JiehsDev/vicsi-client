$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe'
$attempt = 'vendor-path-guide-2026-10-18-3'
$reportRoot = Join-Path $projectRoot ('docs/evidence/evidence-display/' + $attempt)
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$output = Join-Path $reportRoot 'VendorPathGuideTest.txt'
$log = Join-Path $reportRoot 'VendorPathGuideTest.log'
if (Test-Path -LiteralPath $output) { throw 'Refusing to overwrite a previous test report.' }
$owners = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $projectRoot + '*') -and $_.CommandLine -notlike '*AssetImportWorker*' }
if ($owners) { throw 'An Editor already owns the project.' }
$arguments = @('-batchmode','-force-d3d11','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','Phase5PlayModeBatchRunner.Run','-phase5Suite','VendorPathGuideTest','-phase5Output',('"'+$output+'"'),'-phase5SingleSuite','-vendorPathPreview','-logFile',('"'+$log+'"'))
Write-Output 'START VendorPathGuideTest (Direct3D 11)'
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddMinutes(8)
while (!$process.HasExited) {
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Test timed out; no Editor was killed.' }
    Start-Sleep -Seconds 2
    $process.Refresh()
}
if (!(Test-Path -LiteralPath $output)) { throw 'Missing focused report; inspect log.' }
$report = Get-Content -LiteralPath $output -Raw
$passed = [regex]::Matches($report,'(?m)^\s*PASS:').Count
$failed = [regex]::Matches($report,'(?m)^\s*FAIL:').Count
Write-Output "VendorPathGuideTest: $passed PASS / $failed FAIL / exit $($process.ExitCode)"
if ($process.ExitCode -ne 0 -or $failed -gt 0 -or $report -notmatch '=== PASS') { Write-Output $report; throw 'Focused vendor-path check failed.' }
& (Join-Path $projectRoot 'Tools/RunEvidenceDisplayChecks.ps1') -Suites @('VendorFollowupTest') -Attempt $attempt
