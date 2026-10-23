$ErrorActionPreference='Stop'
$deadline=(Get-Date).AddMinutes(20)
$report='docs/evidence/ioc-soco-phase5/closing-audit/F/run1/PreSessionFlowControllerTest.txt'
while(!(Test-Path -LiteralPath $report)){if((Get-Date)-gt $deadline){throw 'Final suite pending; no Editor launched'};Start-Sleep -Seconds 3}
Get-ChildItem docs/evidence/ioc-soco-phase5/closing-audit/F/run1 -Filter *.txt | ForEach-Object {if((Get-Content -LiteralPath $_.FullName -Raw)-notmatch '=== PASS'){throw 'Final batch failed'}}
while(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*-projectPath*'}){if((Get-Date)-gt $deadline){throw 'Shutdown pending'};Start-Sleep -Seconds 3}
$scene=Join-Path (Get-Location).Path 'Assets/_Project/Scenes/Scenarios/CSI_Environment.unity'
$content=[IO.File]::ReadAllText($scene)
$content=$content.Replace("      value: `n","      value:`n").Replace("      value: `r`n","      value:`r`n")
[IO.File]::WriteAllText($scene,$content,[Text.UTF8Encoding]::new($false))
python docs/evidence/closing-audit/aggregate-final.py
$project=(Get-Location).Path
$log=Join-Path $project 'Logs/Editor-closing-final.log'
$editor=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe' -ArgumentList @('-projectPath',('"'+$project+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "FINAL EDITOR STARTED $($editor.Id)"
