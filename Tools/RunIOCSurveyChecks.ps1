param([string[]]$Suites = @('DialogueCatalogTest','ScenarioPhaseFoundationTest','IOCAssessmentGuideTest','ArrivalPPETest','ArrivalEntryTest','SceneAccessRegisterTest','NarrativeRoleFlowTest','SceneTestStartTest'), [string]$Attempt = 'run1')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$reportRoot = Join-Path $projectRoot ('docs/evidence/ioc-survey/' + $Attempt)
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$authoring = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like '*-executeMethod ArrivalPPEAuthoring.Build*' }
foreach ($owner in $authoring) {
    $process = Get-Process -Id $owner.ProcessId -ErrorAction SilentlyContinue
    if ($process) { $process.WaitForExit(180000) | Out-Null; if (!$process.HasExited) { throw 'Authoring still running; no competing Editor started.' } }
}
foreach ($suite in $Suites) {
    $output = Join-Path $reportRoot ($suite + '.txt')
    if (Test-Path -LiteralPath $output) { throw "Report already exists: $output" }
    $owners = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $projectRoot + '*') -and $_.CommandLine -notlike '*AssetImportWorker*' }
    if ($owners) { throw 'An Editor already owns the project; no competing process started.' }
    $graphics = if ($suite -in @('SceneAccessRegisterTest','ArrivalPPETest','SceneTestStartTest')) { '-force-d3d11' } else { '-nographics' }
    $arguments = @('-batchmode',$graphics,'-projectPath',('"' + $projectRoot + '"'),'-executeMethod','Phase5PlayModeBatchRunner.Run','-phase5Suite',$suite,'-phase5Output',('"'+$output+'"'),'-phase5SingleSuite','-logFile',('"'+(Join-Path $reportRoot ($suite+'.log'))+'"'))
    Write-Output "START $suite"
    $process = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(480000)) { throw "Timed out: $suite. No Editor was killed." }
    if (!(Test-Path -LiteralPath $output)) { throw "Missing report: $suite, exit $($process.ExitCode)" }
    $report = Get-Content -LiteralPath $output -Raw
    $passed = [regex]::Matches($report, '(?m)^\s*PASS:').Count
    $failed = [regex]::Matches($report, '(?m)^\s*FAIL:').Count
    Write-Output "$suite : $passed PASS / $failed FAIL / exit $($process.ExitCode)"
    if ($failed -gt 0 -or $report -notmatch '=== PASS' -or $process.ExitCode -ne 0) { throw "Failed $suite; inspect preserved report." }
}
