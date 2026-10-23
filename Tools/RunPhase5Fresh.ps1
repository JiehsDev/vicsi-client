param([string]$OutputRoot='docs/evidence/phase5-last-drink/unity', [string]$PlanPath='Tools/phase5-batches.json', [string[]]$Suites)
$ErrorActionPreference='Stop'
$projectPath=(Get-Location).Path
$editor='C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe'
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$groups = if ($Suites) { @(@{batch='recheck';suites=$Suites}) } else {
    @{batch=1;suites=@('FirstResponderTurnoverTest','DialogueHistoryTest','StoryContentTest','ObjectiveStateManagerTest')}
    foreach ($plannedBatch in (Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json)) { $plannedBatch }
}
foreach($batch in $groups) {
    $folder=Join-Path $OutputRoot "batch$($batch.batch)"
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    foreach($suiteName in $batch.suites) {
        $output=Join-Path $folder "$suiteName.txt"
        if(Test-Path -LiteralPath $output) { Write-Output "EXISTING $suiteName (preserved)";continue }
        $absoluteOutput=Join-Path $projectPath $output
        $log=Join-Path $projectPath "$folder/$suiteName.log"
        $arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$projectPath+'"'),'-executeMethod','Phase5PlayModeBatchRunner.Run','-phase5Suite',$suiteName,'-phase5Output',('"'+$absoluteOutput+'"'),'-logFile',('"'+$log+'"'))
        Write-Output "START batch $($batch.batch): $suiteName (fresh Editor)"
        $process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $deadline=(Get-Date).AddMinutes(8)
        while(!$process.HasExited) {
            if((Get-Date) -gt $deadline) {throw "Fresh Editor exceeded eight minutes for $suiteName; stopped automation without killing Unity. See $log."}
            Start-Sleep -Seconds 2
            $process.Refresh()
        }
        if(!(Test-Path -LiteralPath $output)) {throw "No suite report for $suiteName (exit $($process.ExitCode)); see $log. No lock files were altered."}
        $report=Get-Content -LiteralPath $output -Raw
        $passed=([regex]::Matches($report,'(?m)^\s*PASS:')).Count
        $failed=([regex]::Matches($report,'(?m)^\s*FAIL:')).Count
        Write-Output "RESULT $suiteName : $passed PASS checks, $failed FAIL checks (exit $($process.ExitCode))"
        if(!$report.Contains('=== PASS')) {Write-Output $report}
    }
}
Write-Output 'Fresh-process sweep finished; every Editor has exited.'
