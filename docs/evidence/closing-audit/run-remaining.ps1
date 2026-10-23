$ErrorActionPreference = 'Stop'
$projectRoot = (Get-Location).Path
$reportRoot = Join-Path $projectRoot 'docs/evidence/ioc-soco-phase5/closing-audit'
# The existing B driver owns the Editor until its final suite has exited.
$deadline = (Get-Date).AddMinutes(20)
while (!(Test-Path -LiteralPath (Join-Path $reportRoot 'B/run1/EvidenceProcedurePanelTest.txt'))) {
    if ((Get-Date) -gt $deadline) { throw 'Batch B did not finish; no further tests queued.' }
    Start-Sleep -Seconds 2
}
foreach ($report in Get-ChildItem -LiteralPath (Join-Path $reportRoot 'B/run1') -Filter '*.txt') {
    if ((Get-Content -LiteralPath $report.FullName -Raw) -notmatch '=== PASS') { throw "Batch B failure: $($report.Name)" }
}
while (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like '*-projectPath*' }) {
    if ((Get-Date) -gt $deadline) { throw 'Editor has not shut down; no competing process started.' }
    Start-Sleep -Seconds 2
}
function RunGroup([string]$name, [string[]]$suites, [string]$mode = 'Normal') {
    & ./Tools/RunIocSocoBatch.ps1 -Batch "closing-audit/$name" -Suites $suites -Fresh -MeasurementMode $mode
    foreach ($suite in $suites) {
        $p = Join-Path $reportRoot "$name/run1/$suite.txt"
        if (!(Test-Path -LiteralPath $p) -or (Get-Content -LiteralPath $p -Raw) -notmatch '=== PASS') {
            throw "STOP: $name/$suite failed or has no report; remaining batches were not queued."
        }
    }
}
RunGroup 'C1' @('GreyboxFlowTest','ChalkProcedureTest','BiologicalSamplingTest','MeasurementProcedureTest','PerEvidenceMeasurementTest')
RunGroup 'C2' @('SceneRelationalMeasurementTest','CollectionFeedbackTest','EvidenceInventoryTest','EvidenceTraceMarkerTest')
RunGroup 'C3-wrong-order' @('MeasurementProcedureTest') 'WrongOrder'
RunGroup 'D' @('EvidenceNotesTest','NarrativeEventManagerTest','RequirementEvaluatorTest','BrotherPathTest','StoryContentTest')
RunGroup 'E' @('RelationshipStateManagerTest','InconclusivePathTest','EndingManagerTest')
RunGroup 'F' @('AssessmentUploadServiceTest','SessionPersistenceTest','VrIdentityUploadBridgeTest','OfflineQueueRetentionTest','PreSessionFlowControllerTest')
Write-Output 'ALL REQUESTED C-F BATCHES COMPLETE; every suite ran in a fresh process.'
