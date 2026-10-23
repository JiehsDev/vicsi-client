param([string[]]$Suites=@('RecorderDocumentationAccuracyTest','RecorderReportLimitTest','BodyRemovalTest','BottleFingerprintProcedureTest','FinalDeductionAssessmentTest','AssessmentUploadServiceTest','ProcedureTimelineUploadTest'),[string]$Attempt='run1')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$outputRoot=Join-Path $projectRoot ('docs/evidence/recorder-documentation-2026-10-21/'+$Attempt)
[IO.Directory]::CreateDirectory($outputRoot)|Out-Null
foreach($suite in $Suites){
    $output=Join-Path $outputRoot ($suite+'.txt')
    if(Test-Path -LiteralPath $output){throw "Report exists: $output"}
    $requestPath=Join-Path $projectRoot 'Temp/recorder-documentation-validation.json'
    if(Test-Path -LiteralPath $requestPath){throw 'A validation request is already pending.'}
    $request=@{suite=$suite;output=$output}|ConvertTo-Json
    [IO.File]::WriteAllText($requestPath,$request,[Text.UTF8Encoding]::new($false))
    Write-Output "START $suite"
    $deadline=[DateTime]::UtcNow.AddMinutes(9)
    while(!(Test-Path -LiteralPath $output)){
        if([DateTime]::UtcNow -gt $deadline){throw "Timed out: $suite. Editor state retained."}
        Start-Sleep -Seconds 2
    }
    $body=Get-Content -LiteralPath $output -Raw
    $passed=[regex]::Matches($body,'(?m)^\s*PASS:').Count;$failed=[regex]::Matches($body,'(?m)^\s*FAIL:').Count
    Write-Output "$suite : $passed PASS / $failed FAIL"
    if($failed -gt 0 -or $body -notmatch '=== [^\r\n]*PASS'){throw "Failed $suite. Report retained."}
}
