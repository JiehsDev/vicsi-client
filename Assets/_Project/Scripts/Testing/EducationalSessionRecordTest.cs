using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>Real current managers and conclusion capture, isolated upload transport.
/// The case fixture covers earned questions; closeout timing is a logged fixture here,
/// independently exercised through the real closeout controls by SOCOCloseoutTest.</summary>
public sealed class EducationalSessionRecordTest : MonoBehaviour
{
    public bool IsDone {get;private set;}
    public string Report {get;private set;}
    readonly StringBuilder report=new("=== EducationalSessionRecordTest ===\n");
    int failures;
    [Serializable] class ParityCase
    {
        public DeductionAssessmentInput input;
        public DeductionAssessmentResult result;
        public string reasoning,relationships,conclusion,total;
    }
    [Serializable] class ParityCases {public System.Collections.Generic.List<ParityCase> cases=new();}
    void Check(string label,bool ok){report.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)failures++;}
    public void RunAllTests(){AutomatedTestSandbox.Activate(nameof(EducationalSessionRecordTest));StartCoroutine(Run());}
    IEnumerator Run()
    {
        var fixture=gameObject.AddComponent<BrotherPathTest>();fixture.RunAllTests();
        while(!fixture.IsDone)yield return null;
        report.AppendLine(fixture.Report);Check("Current full-case fixture passed",fixture.Report.Contains("=== PASS"));
        if (!fixture.Report.Contains("=== PASS"))
        {
            report.AppendLine("=== FAIL ===");Report=report.ToString();IsDone=true;yield break;
        }
        var logger=SessionLogger.Instance;
        logger.LogEvent(SessionEventType.FinalSceneSurveyCompleted,"fixture-survey");
        logger.LogEvent(SessionEventType.SceneReleaseConfirmed,"fixture-release");
        logger.LogEvent(SessionEventType.InvestigationCompleted,"fixture-closeout");
        logger.EndSession();logger.Flush();
        var config=ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();config.uploadRawEventLogEnabled=false;
        var scenario=Resources.FindObjectsOfTypeAll<ScenarioDefinition>().First(s=>s.scenarioId==FinalDeductionAssessment.Rules.scenarioId);
        var dto=AssessmentSessionBuilder.Build(logger.ActiveLogFilePath,config,scenario,EndingManager.Instance.SelectedConclusionId,EndingManager.Instance.ResolvedEndingId);
        var raw=SessionLogReader.ReadEvents(logger.ActiveLogFilePath);
        var frozen=dto.orderedEvents.Single(e=>e.eventType==FinalDeductionAssessment.SnapshotEvent);
        var input=JsonUtility.FromJson<DeductionAssessmentInput>(frozen.payload.Find(p=>p.key=="assessment").value);
        Check("Thirteen current question records frozen at submission",input.questionRecordVersion==1&&input.questionRecords.Count==13);
        Check("Six reasoning questions have all four current options",input.questionRecords.Where(q=>q.category=="Reasoning").Count()==6
            && input.questionRecords.Where(q=>q.category=="Reasoning").All(q=>q.options.Count==4&&q.options.Any(a=>a.id==q.selectedAnswerId)&&q.maximumWeight==4));
        Check("Six relationship questions have all options and signed penalty weights",input.questionRecords.Where(q=>q.category=="Evidence Relationships").Count()==6
            && input.questionRecords.Where(q=>q.category=="Evidence Relationships").All(q=>q.options.Count==4&&q.options.Count(a=>a.weight==1)==1&&q.options.Count(a=>a.weight==-1)==3));
        var conclusion=input.questionRecords.Single(q=>q.id=="FINAL-CONCLUSION");
        Check("Conclusion snapshots actual offered Paul/Peter options; hidden Inconclusive not invented",conclusion.options.Count==2
            && conclusion.options.Any(a=>a.id=="conclusion-brother"&&a.weight==1)&&conclusion.options.Any(a=>a.id=="conclusion-peter"&&a.weight==0));
        Check("All selected weights match the frozen option",input.questionRecords.All(q=>q.options.Find(a=>a.id==q.selectedAnswerId)?.weight==q.selectedAnswerWeight));
        Check("Authoritative deduction unchanged at 60 + 25 + 15 = 100",dto.deductionAssessment.hasFinalScore&&dto.deductionAssessment.totalScore==100
            &&dto.deductionAssessment.reasoningComponent==60&&dto.deductionAssessment.relationshipComponent==25&&dto.deductionAssessment.conclusionComponent==15);
        Check("Only procedure and verification records uploaded",dto.orderedEvents.All(e=>EducationalSessionRecord.VerificationEvents.Contains(e.eventType)
            ||EducationalSessionRecord.IsProcedure(new SessionEvent{eventType=e.eventType,payload=e.payload},EducationalSessionRecord.Timeline)));
        Check("Original timestamp and sequence of every retained event preserved",dto.orderedEvents.All(e=>raw.Any(r=>r.sequenceNumber==e.sequenceNumber&&r.timestampMs==e.timestampMs&&r.eventType==e.eventType)));
        Check("No duplicated analytics sections",dto.deductionAssessmentEvents.Count==0&&dto.evidenceResults.Count==0&&dto.relationshipResults.Count==0
            &&dto.hypothesisResults.Count==0&&!dto.hasScenePhotography&&!dto.hasMeasurement&&!dto.hasClientReportedScores&&!dto.includesRawEventLog);
        Check("Completed fingerprint processing has a genuine timer event",dto.orderedEvents.Any(e=>e.eventType=="EvidenceFingerprintProcessed"));
        var json=EducationalSessionRecord.Serialize(dto);
        Check("Serialized request omits legacy analytics rather than uploading zero metrics",!json.Contains("\"measurement\"")&&!json.Contains("\"scenePhotography\"")
            &&!json.Contains("\"proceduralViolations\"")&&!json.Contains("\"evidenceResults\"")&&!json.Contains("\"deductionAssessmentEvents\""));
        var transport=new MockAssessmentUploadTransport();var task=transport.UploadAsync("http://localhost/api/v1/assessment-sessions",json,"isolated-test-token",5,CancellationToken.None);
        while(!task.IsCompleted)yield return null;
        Check("Current payload accepted by isolated Unity upload transport",task.Result.HttpStatusCode==200&&transport.Calls.Count==1);
        Directory.CreateDirectory("docs/evidence/session-simplification");
        File.WriteAllText("docs/evidence/session-simplification/current-unity-upload.json",EducationalSessionRecord.Serialize(dto,true));
        File.WriteAllText("docs/evidence/session-simplification/frozen-questions.json",JsonUtility.ToJson(input,true));
        var parity=new ParityCases();
        var parityInput=JsonUtility.FromJson<DeductionAssessmentInput>(JsonUtility.ToJson(input));
        parityInput.questionRecords.Clear();
        string parityJson=JsonUtility.ToJson(parityInput);
        foreach(int strength in new[]{1,3,4})for(int mask=0;mask<64;mask++)foreach(int outcome in new[]{0,1,2})
        {
            var trial=JsonUtility.FromJson<DeductionAssessmentInput>(parityJson);
            foreach(var checkpoint in trial.checkpoints)
                checkpoint.responseId=FinalDeductionAssessment.Rules.checkpoints.Find(q=>q.id==checkpoint.checkpointId).responses.First(a=>a.weight==strength).id;
            for(int n=0;n<trial.relationshipResponses.Count;n++)
            {var question=FinalDeductionAssessment.Rules.relationships[n];trial.relationshipResponses.Find(a=>a.relationshipId==question.id).responseId=(mask&(1<<n))!=0?question.correctInterpretationId:question.interpretations.First(a=>a.id!=question.correctInterpretationId).id;}
            var rules=FinalDeductionAssessment.Rules;
            trial.selectedConclusionId=outcome==0?rules.peterConclusionId:outcome==1?rules.inconclusiveConclusionId:rules.supportedConclusionId;
            trial.resolvedEndingId=outcome==0?rules.peterEndingId:outcome==1?rules.inconclusiveEndingId:rules.supportedEndingId;
            var result=FinalDeductionAssessment.Score(trial,true,rules);
            if(outcome==2&&!result.hasFinalScore){trial.resolvedEndingId=rules.prematureEndingId;result=FinalDeductionAssessment.Score(trial,true,rules);}
            string Format(float v)=>v.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
            parity.cases.Add(new ParityCase{input=trial,result=result,reasoning=Format(result.reasoningComponent),relationships=Format(result.relationshipComponent),conclusion=Format(result.conclusionComponent),total=Format(result.totalScore)});
        }
        File.WriteAllText("docs/evidence/session-simplification/unity-parity-cases.json",JsonUtility.ToJson(parity));
        Check("576 fractional/penalty/context scoring cases captured from the real Unity scorer",parity.cases.Count==576&&parity.cases.All(p=>p.result.hasFinalScore));
        Destroy(config);report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failures ===");Report=report.ToString();IsDone=true;
    }
}
