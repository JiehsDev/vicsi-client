param([string[]]$Suites, [string]$OutputFolder='Temp/phase5-run', [string]$BatchPlanPath)
$ErrorActionPreference='Stop'
if ($BatchPlanPath) {
    foreach ($batch in (Get-Content -LiteralPath $BatchPlanPath -Raw | ConvertFrom-Json)) {
        Write-Output "BATCH $($batch.batch)"
        & $PSCommandPath -Suites $batch.suites -OutputFolder "$OutputFolder/batch$($batch.batch)"
    }
    exit
}
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
function Editor-State {
    $raw = & "$PSScriptRoot/Phase5Mcp.ps1" -Method resources/read -Params '{"uri":"mcpforunity://editor/state"}'
    $reply = $raw | ConvertFrom-Json
    return ($reply.result.contents[0].text | ConvertFrom-Json).data
}
foreach ($suiteName in $Suites) {
    $ready=$false
    for ($attempt=0;$attempt -lt 120;$attempt++) {
        $state=Editor-State
        if ($state.advice.ready_for_tools -and !$state.editor.play_mode.is_playing -and !$state.editor.play_mode.is_changing) {$ready=$true;break}
        if ($attempt % 10 -eq 0) {Write-Output "WAIT Editor reload/transition before $suiteName"}
        Start-Sleep -Seconds 2
    }
    if (!$ready) {throw "Editor not idle before $suiteName; stopped without touching processes or locks."}
    $output="$OutputFolder/$suiteName.txt"
    if (Test-Path -LiteralPath $output) {throw "Existing report $output; choose a new output folder."}
    @{suite=$suiteName;output=$output} | ConvertTo-Json | Set-Content Temp/phase5-request.json
    $null = & "$PSScriptRoot/Phase5Mcp.ps1" -Method tools/call -ParamsPath Temp/phase5-launch.json
    Write-Output "START $suiteName"
    $limit=(Get-Date).AddMinutes(7)
    while (!(Test-Path -LiteralPath $output)) {
        if ((Get-Date) -gt $limit) {throw "No report for $suiteName after seven minutes; stopped safely."}
        Start-Sleep -Seconds 2
    }
    $report=Get-Content -LiteralPath $output -Raw
    $passed=([regex]::Matches($report,'(?m)^\s*PASS:')).Count
    $failed=([regex]::Matches($report,'(?m)^\s*FAIL:')).Count
    Write-Output "RESULT $suiteName : $passed PASS checks, $failed FAIL checks"
    if (!$report.Contains('=== PASS')) {Write-Output $report}
}
Write-Output 'All requested suites have reports; verifying final Edit mode.'
$state=Editor-State
if ($state.editor.play_mode.is_playing) {throw 'Play mode has not exited yet.'}
