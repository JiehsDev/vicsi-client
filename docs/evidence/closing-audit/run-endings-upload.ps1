$ErrorActionPreference='Stop'
$root=Join-Path (Get-Location).Path 'docs/evidence/ioc-soco-phase5/closing-audit'
$deadline=(Get-Date).AddMinutes(10)
$p=Join-Path $root 'D/run3/StoryContentTest.txt'
while(!(Test-Path -LiteralPath $p)){if((Get-Date)-gt $deadline){throw 'Story test not finished; no later batches queued.'};Start-Sleep -Seconds 2}
if((Get-Content -LiteralPath $p -Raw)-notmatch '=== PASS'){throw 'Story test failed; no later batches queued.'}
while(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*-projectPath*'}){if((Get-Date)-gt $deadline){throw 'Editor shutdown pending'};Start-Sleep -Seconds 2}
foreach($group in @(@{name='E';suites=@('RelationshipStateManagerTest','InconclusivePathTest','EndingManagerTest')},@{name='F';suites=@('AssessmentUploadServiceTest','SessionPersistenceTest','VrIdentityUploadBridgeTest','OfflineQueueRetentionTest','PreSessionFlowControllerTest')})){
 & ./Tools/RunIocSocoBatch.ps1 -Batch "closing-audit/$($group.name)" -Suites $group.suites -Fresh
 foreach($suite in $group.suites){$report=Join-Path $root "$($group.name)/run1/$suite.txt";if(!(Test-Path -LiteralPath $report)-or (Get-Content -LiteralPath $report -Raw)-notmatch '=== PASS'){throw "Stopped at $suite"}}
}
Write-Output 'BATCHES E AND F COMPLETE'
