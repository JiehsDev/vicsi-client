$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe'
$outputRoot = Join-Path $root 'docs/evidence/evidence-display/menu-wheel-comfort-regression-2026-10-18'
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
foreach ($suite in @('MenuWheelComfortTest','UIComfortInputTest','SettingsPauseMenuTest')) {
    $owners = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $root + '*') -and $_.CommandLine -notlike '*AssetImportWorker*' }
    if ($owners) { throw 'An Editor owns this project; do not start a competing Editor.' }
    $output = Join-Path $outputRoot ($suite + '.txt')
    $log = Join-Path $outputRoot ($suite + '.log')
    if (Test-Path -LiteralPath $output) { throw "Refusing to overwrite $output" }
    $arguments = @('-batchmode','-nographics','-projectPath',('"'+$root+'"'),'-executeMethod','Phase5PlayModeBatchRunner.Run','-phase5Suite',$suite,'-phase5Output',('"'+$output+'"'),'-phase5SingleSuite','-logFile',('"'+$log+'"'))
    Write-Output "START $suite"
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddMinutes(8)
    while (!$process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Timed out; no process was killed.' }
        Start-Sleep -Seconds 2
        $process.Refresh()
    }
    if (!(Test-Path -LiteralPath $output)) { throw "No report: $suite" }
    $report = Get-Content -LiteralPath $output -Raw
    $pass = [regex]::Matches($report,'(?m)^\s*PASS:').Count
    $fail = [regex]::Matches($report,'(?m)^\s*FAIL:').Count
    Write-Output "$suite : $pass PASS / $fail FAIL / runner exit $($process.ExitCode)"
    # The existing batch runner only recognizes '=== PASS', while UIComfortInputTest
    # has always emitted '=== ALL PASS ==='. Preserve both sources and validate the
    # actual assertion report; record the runner status rather than hiding it.
    if ($fail -ne 0 -or $report -notmatch '=== (ALL )?PASS') { throw "Assertions failed: $suite" }
    if ((Get-Content -LiteralPath $log -Raw) -match 'error CS\d+') { throw "C# compilation error: $suite" }
}
