param([string[]]$Suites=@('UIWorkflowRefreshTest','PhotographicScaleTest','PhotographyShutterOwnershipTest','UIComfortInputTest','EvidenceProcedureMapTest','EvidenceProcedurePanelTest','SamplePreparationTest'),[string]$Attempt='run1')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$outputRoot=Join-Path $projectRoot ('docs/evidence/ui-refresh-2026-10-20/'+$Attempt)
[IO.Directory]::CreateDirectory($outputRoot)|Out-Null
foreach($suite in $Suites){
    $owners=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object{$_.CommandLine.Replace('\','/') -like ('*'+$projectRoot.Replace('\','/')+'*') -and $_.CommandLine -notlike '*AssetImportWorker*'}
    if($owners){throw 'An Editor owns the project. No competing process started.'}
    $output=Join-Path $outputRoot ($suite+'.txt')
    if(Test-Path -LiteralPath $output){throw "Report exists: $output. Choose a new attempt."}
    $log=Join-Path $outputRoot ($suite+'.log')
    $arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','Phase5PlayModeBatchRunner.Run','-phase5Suite',$suite,'-phase5Output',('"'+$output+'"'),'-phase5SingleSuite','-logFile',('"'+$log+'"'))
    Write-Output "START $suite"
    $process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while(!$process.HasExited){
        if([DateTime]::UtcNow -gt $deadline){throw "Timed out: $suite. No Editor was killed."}
        Start-Sleep -Seconds 2;$process.Refresh()
    }
    if(!(Test-Path -LiteralPath $output)){throw "No report for $suite; inspect $log (exit $($process.ExitCode))."}
    $report=Get-Content -LiteralPath $output -Raw
    $passed=[regex]::Matches($report,'(?m)^\s*PASS:').Count
    $failed=[regex]::Matches($report,'(?m)^\s*FAIL:').Count
    Write-Output "$suite : $passed PASS / $failed FAIL / exit $($process.ExitCode)"
    if($report -notmatch '=== PASS' -or $failed -gt 0 -or $process.ExitCode -ne 0){throw "Failed $suite. Preserved report and log."}
}
