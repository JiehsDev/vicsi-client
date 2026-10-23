param([string]$Batch, [string[]]$Suites, [string]$Attempt = 'run1', [switch]$Fresh, [ValidateSet('Normal','WrongOrder')][string]$MeasurementMode = 'Normal')
$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { Split-Path -Parent $PSScriptRoot } else { (Get-Location).Path }
$reportRoot = Join-Path $projectRoot "docs/evidence/ioc-soco-phase5/$Batch/$Attempt"
$requestPath = Join-Path $projectRoot 'Tools/phase5-file-request.json'
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$initialEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like '*-projectPath*' } | Select-Object -First 1
$baselinePrivateGB = if ($initialEditor) { (Get-Process -Id $initialEditor.ProcessId).PrivateMemorySize64/1GB } else { 0 }
foreach ($suite in $Suites) {
    $reportPath = Join-Path $reportRoot "$suite.txt"
    if (Test-Path -LiteralPath $reportPath) { throw "Report already exists: $reportPath" }
    if (Test-Path -LiteralPath $requestPath) { throw 'An earlier request is still queued.' }
    $suiteProcess = $null
    if ($Fresh) {
        if (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like '*-projectPath*' }) {
            throw 'An Editor already owns the project; no competing Unity process started.'
        }
        $logPath = Join-Path $reportRoot "$suite.log"
        $arguments = @('-batchmode','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','Phase5PlayModeBatchRunner.Run',
            '-phase5Suite',$suite,'-phase5Output',('"'+$reportPath+'"'),'-phase5SingleSuite','-phase5MeasurementMode',$MeasurementMode,'-logFile',('"'+$logPath+'"'))
        $suiteProcess = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    } else {
        @{ suite = $suite; output = $reportPath.Replace('\','/') } | ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding UTF8
    }
    Write-Output "START $Batch/$Attempt $suite"
    $deadline = (Get-Date).AddSeconds(360)
    while (!(Test-Path -LiteralPath $reportPath)) {
        if ($suiteProcess -and $suiteProcess.HasExited) { throw "$suite exited without a report; inspect its log." }
        if ((Get-Date) -gt $deadline) { throw "Timed out awaiting $suite; stop before queuing anything else." }
        Start-Sleep -Seconds 1
    }
    $report = Get-Content -LiteralPath $reportPath -Raw
    Write-Output (($report -split "`n" | Where-Object { $_ -match '^=== (PASS|FAIL)' }) -join ' ')
    if ($suiteProcess) {
        $suiteProcess.WaitForExit(60000) | Out-Null
        if (!$suiteProcess.HasExited) { throw 'Unity is still shutting down; stop before starting another process.' }
    }
    # The controlled runner exits Play mode before it will consume the next request.
    Start-Sleep -Seconds 3
    $editorInfo = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like '*-projectPath*' } | Select-Object -First 1
    $editorProcess = if ($editorInfo) { Get-Process -Id $editorInfo.ProcessId -ErrorAction SilentlyContinue }
    $os = Get-CimInstance Win32_OperatingSystem
    $memory = [pscustomobject]@{ suite=$suite; editorPid=$editorProcess.Id; privateGB=[math]::Round($editorProcess.PrivateMemorySize64/1GB,2); freeGB=[math]::Round($os.FreePhysicalMemory/1MB,2) }
    $memory | Export-Csv -LiteralPath (Join-Path $reportRoot 'memory.csv') -NoTypeInformation -Append
    Write-Output "MEMORY private=$($memory.privateGB) GB free=$($memory.freeGB) GB"
    if ($report -notmatch '=== PASS') { Write-Output "STOP_FAILURE $suite"; break }
    if ($memory.privateGB -gt ($baselinePrivateGB + 1.5) -or $memory.freeGB -lt .25) { Write-Output "STOP_MEMORY after $suite; continue only after a clean Editor review/restart."; break }
}
