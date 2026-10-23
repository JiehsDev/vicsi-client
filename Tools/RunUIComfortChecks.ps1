param([string[]]$Suites = @('UIComfortInputTest','ReasoningCheckpointTest','DialogueCatalogTest','NarrativeRoleFlowTest','ScenarioPhaseFoundationTest','VendorFollowupTest','EvidenceProcedurePanelTest','EvidenceInventoryTest','CollectionFeedbackTest','BiologicalSamplingTest','FingerprintStorageTest','SOCOCloseoutTest','EndingManagerTest','CrimeSceneSketchTest','GrabReachTest'), [string]$OutputDirectory = 'docs/evidence/ui-comfort-input/final')
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
foreach ($suite in $Suites) {
    $output = Join-Path $OutputDirectory ($suite + '.txt')
    if (Test-Path -LiteralPath $output) { throw "Refusing to overwrite existing evidence: $output" }
    if (Test-Path -LiteralPath 'Tools/phase5-file-request.json') { throw 'Another diagnostic request is pending.' }
    @{ suite=$suite; output=$output.Replace('\','/') } | ConvertTo-Json -Compress | Set-Content -LiteralPath 'Tools/phase5-file-request.json'
    $deadline = [DateTime]::UtcNow.AddMinutes(8)
    while (!(Test-Path -LiteralPath $output)) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "Timed out waiting for $suite" }
        Start-Sleep -Milliseconds 1000
    }
    $lines = Get-Content -LiteralPath $output
    $pass = @($lines | Where-Object { $_ -match '^\s*PASS:' }).Count
    $fail = @($lines | Where-Object { $_ -match '^\s*FAIL:' }).Count
    Write-Output "$suite : $pass PASS / $fail FAIL"
    $lines | Where-Object { $_ -match '^\s*FAIL:|^===' } | Write-Output
}
