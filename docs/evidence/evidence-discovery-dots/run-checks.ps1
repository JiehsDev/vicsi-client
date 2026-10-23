param([string]$Attempt='evidence-discovery-dots-2026-10-18')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe'
$outputRoot = Join-Path $root ('docs/evidence/evidence-display/' + $Attempt)
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
foreach ($suite in @('EvidenceDiscoveryDotTest','EvidenceDiscoveryTest','PhotographyGuideTest')) {
    $owners = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like ('*'+$root+'*') -and $_.CommandLine -notlike '*AssetImportWorker*' }
    if ($owners) { throw 'An Editor owns this project; do not start a competing process.' }
    $output = Join-Path $outputRoot ($suite+'.txt')
    $log = Join-Path $outputRoot ($suite+'.log')
    if (Test-Path -LiteralPath $output) { throw "Refusing to overwrite $output" }
    $graphics = if ($suite -eq 'EvidenceDiscoveryDotTest') { '-force-d3d11' } else { '-nographics' }
    $arguments = @('-batchmode',$graphics,'-projectPath',('"'+$root+'"'),'-executeMethod','Phase5PlayModeBatchRunner.Run','-phase5Suite',$suite,'-phase5Output',('"'+$output+'"'),'-phase5SingleSuite','-logFile',('"'+$log+'"'))
    Write-Output "START $suite"
    $process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Timed out; no Editor was killed.' }
        Start-Sleep -Seconds 2
        $process.Refresh()
    }
    if (!(Test-Path -LiteralPath $output)) { throw "No report: $suite" }
    $report=Get-Content -LiteralPath $output -Raw
    $passed=[regex]::Matches($report,'(?m)^\s*PASS:').Count
    $failed=[regex]::Matches($report,'(?m)^\s*FAIL:').Count
    Write-Output "$suite : $passed PASS / $failed FAIL / exit $($process.ExitCode)"
    if ($failed -gt 0 -or $report -notmatch '=== PASS' -or $process.ExitCode -ne 0) { Write-Output $report; throw "Failed $suite" }
    if ((Get-Content -LiteralPath $log -Raw) -match 'error CS\d+') { throw "C# compilation error: $suite" }
}
