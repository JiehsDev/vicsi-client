param([string[]]$Suites = @('EvidenceProcedurePanelTest','InventoryUILayoutTest','BottleFingerprintProcedureTest','ReasoningCheckpointTest','EvidenceRelationshipsReviewTest','AssessmentUploadServiceTest','SOCOCloseoutTest','CustodyPassTest','EducationalSessionRecordTest','MeasurementToolTest','SceneRelationalMeasurementTest'), [string]$Attempt='gameplay-final-2')
$ErrorActionPreference='Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe'
$reportRoot = Join-Path $projectRoot ('docs/evidence/evidence-display/' + $Attempt)
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$inFlight = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -like '*-phase5Suite*' -and $_.CommandLine.Replace('\','/').Contains($reportRoot.Replace('\','/'))
}
foreach($existing in $inFlight) {
    Write-Output ('Waiting for this attempt''s existing Unity suite, PID ' + $existing.ProcessId)
    $running = Get-Process -Id $existing.ProcessId -ErrorAction SilentlyContinue
    $deadline = [DateTime]::UtcNow.AddMinutes(8)
    while($running -and !$running.HasExited) {
        if([DateTime]::UtcNow -gt $deadline) { throw 'Existing suite did not exit; no competing process started.' }
        Start-Sleep -Seconds 2
        $running.Refresh()
    }
}
foreach($suite in $Suites) {
    $output = Join-Path $reportRoot ($suite + '.txt')
    if(Test-Path -LiteralPath $output) {
        if((Get-Content -LiteralPath $output -Raw) -match '=== PASS') { Write-Output "PRESERVED $suite"; continue }
        throw "Refusing to overwrite failed report $output; choose another attempt directory."
    }
    $owners = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $projectRoot + '*') -and $_.CommandLine -notlike '*AssetImportWorker*' }
    if($owners) { throw 'An Editor already owns this project; do not start a competing process.' }
    $log = Join-Path $reportRoot ($suite + '.log')
    $graphics = if($suite -in @('EvidenceProcedurePanelTest','CustodyPassTest','InventoryUILayoutTest','CrimeSceneSketchTest')) { '-force-d3d11' } else { '-nographics' }
    $arguments = @('-batchmode',$graphics,'-projectPath',('"'+$projectRoot+'"'),'-executeMethod','Phase5PlayModeBatchRunner.Run','-phase5Suite',$suite,'-phase5Output',('"'+$output+'"'),'-phase5SingleSuite','-logFile',('"'+$log+'"'))
    Write-Output "START $suite"
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddMinutes(8)
    while(!$process.HasExited) {
        if([DateTime]::UtcNow -gt $deadline) { throw "Timed out: $suite. No Editor was killed." }
        Start-Sleep -Seconds 2
        $process.Refresh()
    }
    if(!(Test-Path -LiteralPath $output)) { throw "No report for $suite; inspect $log" }
    $report = Get-Content -LiteralPath $output -Raw
    $passed = [regex]::Matches($report,'(?m)^\s*PASS:').Count
    $failed = [regex]::Matches($report,'(?m)^\s*FAIL:').Count
    Write-Output "$suite : $passed PASS / $failed FAIL / exit $($process.ExitCode)"
    if($failed -gt 0 -or $report -notmatch '=== PASS') { Write-Output $report; throw "Failed $suite" }
}
