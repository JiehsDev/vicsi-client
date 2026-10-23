using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small scoring/state fixtures, not a full case playthrough.</summary>
public sealed class FinalDeductionAssessmentTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== FinalDeductionAssessmentTest ===\n");
    private int checks, failures;
    private DeductionScoringDefinition Rules => FinalDeductionAssessment.Rules;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private void Check(string label, bool pass) { checks++; if (!pass) failures++; report.AppendLine((pass ? "PASS: " : "FAIL: ") + label); }
    private static bool Near(float a,float b) => Mathf.Abs(a-b)<.0001f;
    private DeductionAssessmentInput Fixture(int[] weights = null)
    {
        weights ??= new[]{4,4,4,4,4,4};
        var i = new DeductionAssessmentInput { scoringRulesVersion=Rules.scoringRulesVersion,contentVersion=Rules.contentVersion,
            selectedConclusionId=Rules.supportedConclusionId,resolvedEndingId=Rules.supportedEndingId,atConclusion=true,requiresSceneRelease=true,
            checkpointSystemReady=true,form1Received=true,victimObserved=true,sceneSecurityAcknowledged=true,
            iocObservationIds=new(){"ioc-possible-weapon","ioc-bloodstain-area"},confirmedFindingIds=new(Rules.supportedFindingIds),
            discoveredStatementIds=new(){Rules.supportedStatementId},observedEvidenceIds=new(){"BODY-A","EVD-014","EVD-015","EVD-018"},
            completedRelationshipIds=Rules.relationships.Select(r=>r.id).ToList(),boardConnections=Rules.relationships.Select(r=>new DeductionBoardPair{sourceId=r.sourceId,targetId=r.targetId}).ToList() };
        for(int n=0;n<Rules.checkpoints.Count;n++)
            i.checkpoints.Add(new DeductionCheckpointState {checkpointId=Rules.checkpoints[n].id,
                responseId=Rules.checkpoints[n].responses.Find(r=>r.weight==weights[n]).id,wasEligible=true,responseOrder=n+1,eligibleSeconds=n*10,answeredSeconds=n*10+5});
        return i;
    }
    private DeductionAssessmentResult Score(DeductionAssessmentInput input,bool completed=true) => FinalDeductionAssessment.Score(input,completed,Rules);
    private IEnumerator Run()
    {
        yield return null;
        Check("Active scenario selects append-only 1.7.0",FinalDeductionAssessment.ActiveInScene && AssessmentUploadService.Instance.ScoringRulesVersion=="1.7.0");
        Check("One research configuration contains 60/25/15 and six checkpoints",Rules.checkpointWeight==60 && Rules.relationshipWeight==25 && Rules.conclusionWeight==15
            && Rules.checkpoints.Count==6 && Rules.validationStatus=="RESEARCH_CONFIGURATION_REQUIRES_EXPERT_VALIDATION");
        var input=Fixture();var r=Score(input);
        Check("Six max-weight responses earn full 60/24 maximum",r.checkpointEarned==24 && r.checkpointMaximum==24 && Near(r.reasoningComponent,60));
        Check("Six supported relationships earn 25 including timeline contradiction",Rules.relationships.Count==6 && Near(r.relationshipComponent,25));
        Check("Supported Paul full chain earns 15 and total 100",r.hasFinalScore && Near(r.conclusionComponent,15) && Near(r.totalScore,100));
        input=Fixture(new[]{3,4,4,4,4,4});r=Score(input);
        Check("Worked strong example computes 57.5 + 25 + 15 = 97.5",Near(r.reasoningComponent,57.5f) && Near(r.totalScore,97.5f));
        input=Fixture(new[]{3,3,3,3,2,2});input.completedRelationshipIds=input.completedRelationshipIds.Take(3).ToList();input.boardConnections.Clear();input.resolvedEndingId=Rules.prematureEndingId;r=Score(input);
        Check("Worked mixed example computes 40 + 12.5 + 3.75 = 56.25",Near(r.reasoningComponent,40) && Near(r.relationshipComponent,12.5f) && Near(r.conclusionComponent,3.75f) && Near(r.totalScore,56.25f));
        input=Fixture(new[]{1,1,1,1,2,2});input.completedRelationshipIds.Clear();input.boardConnections=new(){new(){sourceId="EVD-014",targetId="statement_peter_timeline"}};
        input.selectedConclusionId=Rules.peterConclusionId;input.resolvedEndingId=Rules.peterEndingId;r=Score(input);
        Check("Worked weak/Peter example computes 20 + 0 + 0 = 20",Near(r.totalScore,20) && Near(r.conclusionComponent,0));
        input=Fixture();input.checkpoints[0].responseId="";input.checkpoints[0].responseOrder=0;r=Score(input);
        Check("Eligible unanswered is zero earned with denominator still 24",r.eligibleUnansweredCount==1 && r.checkpointMaximum==24 && Near(r.reasoningComponent,50));
        input=Fixture();input.confirmedFindingIds.Remove("FIND-SPATTER-RECONSTRUCTION");input.completedRelationshipIds.Remove("REL-spatter-consistent-reconstruction");
        input.checkpoints[5].wasEligible=false;input.checkpoints[5].responseId="";input.checkpoints[5].responseOrder=0;input.resolvedEndingId=Rules.prematureEndingId;r=Score(input);
        Check("Conclusion before later discovery retains missing opportunity",r.hasFinalScore && r.undiscoveredCount==1 && r.checkpointMaximum==24 && Near(r.reasoningComponent,50));
        input=Fixture();input.checkpoints[2].wasEligible=false;input.checkpoints[2].responseId="";r=Score(input);
        Check("Reached facts without recorded eligibility require review without grade",r.status=="requires_review" && !r.hasFinalScore);
        input=Fixture();input.checkpointSystemReady=false;
        Check("Missing checkpoint system cannot produce a polished grade",!Score(input).hasFinalScore);
        input=Fixture();input.contentVersion="bad";
        Check("Content mismatch requires review",Score(input).status=="requires_review");
        input=Fixture();input.completedRelationshipIds.Clear();input.boardConnections.Clear();input.resolvedEndingId=Rules.prematureEndingId;
        Check("No completed relationships earns zero relationship component",Near(Score(input).relationshipComponent,0));
        input.completedRelationshipIds.Add("REL-paul-timeline-contradiction");
        Check("An established supported timeline contradiction earns normalized credit",Near(Score(input).relationshipComponent,25f/6));
        input=Fixture();input.completedRelationshipIds.Clear();input.completedRelationshipIds.Add("REL-knife-consistent-wound");input.confirmedFindingIds.Clear();input.resolvedEndingId=Rules.prematureEndingId;
        Check("Unsupported relationship earns no credit",Near(Score(input).relationshipComponent,0));
        input=Fixture();input.boardConnections.Add(new(){sourceId="EVD-014",targetId="statement_peter_timeline"});r=Score(input);
        Check("Unmatched current board connection reduces relationship quality",r.unsupportedRelationships==1 && Near(r.relationshipComponent,25f*5/6));
        input.boardConnections.RemoveAt(input.boardConnections.Count-1);
        Check("Removed/corrected connection leaves no lasting deduction penalty",Near(Score(input).relationshipComponent,25));
        input=Fixture();input.completedRelationshipIds.AddRange(input.completedRelationshipIds.ToArray());input.boardConnections.AddRange(input.boardConnections.ToArray());
        Check("Duplicate IDs/pairs cannot exceed 100",Near(Score(input).totalScore,100));
        input=Fixture();input.completedRelationshipIds.Remove("REL-paul-timeline-contradiction");
        Check("Supported ending without authored full chain requires review",Score(input).status=="requires_review");
        input.resolvedEndingId=Rules.prematureEndingId;
        Check("Premature Paul earns 3.75 conclusion points",Near(Score(input).conclusionComponent,3.75f));
        input=Fixture();input.selectedConclusionId=Rules.inconclusiveConclusionId;input.resolvedEndingId=Rules.inconclusiveEndingId;
        Check("Inconclusive despite full supported chain earns lower credit",Near(Score(input).conclusionComponent,3.75f));
        input.completedRelationshipIds.Clear();input.boardConnections.Clear();
        Check("Inconclusive with genuinely insufficient developed chain earns 15",Near(Score(input).conclusionComponent,15));
        Check("Incomplete scene/session emits no final score",Score(Fixture(),false).status=="incomplete" && !Score(Fixture(),false).hasFinalScore);
        input=Fixture();input.atConclusion=false;input.selectedConclusionId="";input.resolvedEndingId="";
        Check("Absent conclusion emits no final score",!Score(input).hasFinalScore);
        Check("Missing snapshot is safe and requires review if completion claimed",FinalDeductionAssessment.Score(null,true,Rules).status=="requires_review");
        var invalidRules=JsonUtility.FromJson<DeductionScoringDefinition>(JsonUtility.ToJson(Rules));invalidRules.conclusionWeight=30;
        Check("Invalid weights are rejected rather than hidden by a 100 clamp",!FinalDeductionAssessment.Score(Fixture(),true,invalidRules).hasFinalScore);
        input=Fixture();input.checkpoints=null;
        Check("Malformed local checkpoint data cannot crash scoring",Score(input).status=="requires_review");

        input=Fixture();input.relationshipReviewVersion=1;input.boardConnections.Clear();input.completedRelationshipIds.Clear();
        input.relationshipResponses=Rules.relationships.Select((q,n)=>new RelationshipInterpretationResponse {
            relationshipId=q.id,responseId=q.correctInterpretationId,responseOrder=n+1,answeredSeconds=n+10}).ToList();
        r=Score(input);
        Check("Review choices preserve exactly 60/25/15 and total 100 without board connections",r.hasFinalScore && Near(r.reasoningComponent,60)
            && Near(r.relationshipComponent,25) && Near(r.conclusionComponent,15) && Near(r.totalScore,100));
        var timeline=Rules.relationships.Find(q=>q.id==VendorFollowupInterview.RelationshipId);
        input.relationshipResponses.Find(a=>a.relationshipId==timeline.id).responseId=timeline.interpretations.First(c=>c.id!=timeline.correctInterpretationId).id;
        input.resolvedEndingId=Rules.prematureEndingId;r=Score(input);
        Check("Wrong interpretation uses existing unsupported penalty and cannot support Paul ending",Near(r.relationshipComponent,25f*4/6)
            && Near(r.conclusionComponent,3.75f) && r.unsupportedRelationships==1);
        input.completedRelationshipIds=Rules.relationships.Select(q=>q.id).ToList();
        Check("Manual completed IDs cannot override locked review answers",Near(Score(input).relationshipComponent,r.relationshipComponent));
        input.relationshipResponses.Add(input.relationshipResponses[0]);
        Check("Duplicate review records are rejected",Score(input).status=="requires_review");
        input=Fixture();input.relationshipReviewVersion=1;
        Check("Final grade cannot bypass the required available relationship review",Score(input).status=="requires_review");

        // Real session state and payload, independent of procedural result ratios.
        var manager=ReasoningCheckpointManager.Instance;
        manager.Evaluate();
        if(manager.IsEligible("RC-01-PETER") && !manager.IsAnswered("RC-01-PETER")) manager.RecordResponse("RC-01-PETER","RC-01-PETER-C");
        var before=Score(FinalDeductionAssessment.CaptureLive(Rules.inconclusiveConclusionId,Rules.inconclusiveEndingId));
        SessionLogger.Instance.LogEvent(SessionEventType.ProcedureOutOfOrder,"phase4-focused",new Dictionary<string,string>{{"procedureId","final-evidence-photograph"}});
        var after=Score(FinalDeductionAssessment.CaptureLive(Rules.inconclusiveConclusionId,Rules.inconclusiveEndingId));
        Check("ProcedureOutOfOrder remains logged and does not change numerical components",SessionLogger.Instance.EventCounts[SessionEventType.ProcedureOutOfOrder]>0
            && Near(before.reasoningComponent,after.reasoningComponent) && Near(before.relationshipComponent,after.relationshipComponent) && Near(before.conclusionComponent,after.conclusionComponent));
        RelationshipReviewTestUtil.ConfirmAvailable();
        var evidenceBefore=string.Join("|",EvidenceStateManager.Instance.AllRecords.Select(e=>e.definition.evidenceId+":"+e.status));
        var relBefore=string.Join("|",RelationshipStateManager.Instance.AllRecords.Select(e=>e.definition.relationshipId+":"+e.state));
        FinalDeductionAssessment.CaptureConclusion(Rules.inconclusiveConclusionId,Rules.inconclusiveEndingId);
        FinalDeductionAssessment.CaptureConclusion(Rules.peterConclusionId,Rules.peterEndingId);
        Check("Conclusion assessment snapshot locks once without altering conclusion logic",SessionLogger.Instance.EventCounts[SessionEventType.DeductionAssessmentSnapshot]==1 && EndingManager.Instance.AcceptedEnding==null);
        Check("Scoring/capture does not mutate evidence or relationships",evidenceBefore==string.Join("|",EvidenceStateManager.Instance.AllRecords.Select(e=>e.definition.evidenceId+":"+e.status))
            && relBefore==string.Join("|",RelationshipStateManager.Instance.AllRecords.Select(e=>e.definition.relationshipId+":"+e.state)));
        var config=ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();config.uploadRawEventLogEnabled=false;config.maxEventsPerRequest=1;
        var scenario=UnityEngine.Resources.FindObjectsOfTypeAll<ScenarioDefinition>().First(s=>s.scenarioId==Rules.scenarioId);
        SessionLogger.Instance.Flush();
        var dto=AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,Rules.inconclusiveConclusionId,Rules.inconclusiveEndingId);
        Check("Current payload keeps protocol version and required checkpoint records without legacy analytics",dto.payloadVersion==1 && dto.scoringRulesVersion=="1.7.0"
            && dto.orderedEvents.Any(e=>e.eventType=="ReasoningCheckpointSubmitted") && dto.orderedEvents.Any(e=>e.eventType=="DeductionAssessmentSnapshot") && dto.evidenceResults.Count==0);
        Check("Incomplete payload distinguishes new categories and withholds final grade",dto.hasDeductionAssessment && !dto.deductionAssessment.hasFinalScore && dto.deductionAssessment.checkpointMaximum==24);
        config.uploadRawEventLogEnabled=true;
        dto=AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,Rules.inconclusiveConclusionId,Rules.inconclusiveEndingId);
        Check("Raw-event cap cannot truncate the minimal educational record",!dto.includesRawEventLog && dto.orderedEvents.Any(e=>e.eventType=="DeductionAssessmentSnapshot"));
        // Diagnostic completion milestones only; no full procedural playthrough.
        SessionLogger.Instance.LogEvent(SessionEventType.InvestigationCompleted);SessionLogger.Instance.EndSession();
        dto=AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,Rules.inconclusiveConclusionId,Rules.inconclusiveEndingId);
        Check("Completed payload carries computed 1.7.0 categories and chronology",dto.deductionAssessment.hasFinalScore
            && dto.orderedEvents.Any(e=>e.eventType=="SessionEnded") && dto.orderedEvents.Any(e=>e.eventType=="InvestigationCompleted"));
        System.IO.Directory.CreateDirectory("docs/evidence/session-simplification");
        // This scoring fixture bypasses the real conclusion picker; the separate
        // EducationalSessionRecordTest captures the real offered question/options.
        System.IO.File.WriteAllText("docs/evidence/session-simplification/scoring-fixture-wire.json",JsonUtility.ToJson(dto,true));
        Destroy(config);
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path);yield return null;
        Check("Fresh reset clears checkpoints and cannot inherit frozen final assessment",ReasoningCheckpointManager.Instance.Records.Count==0 && !FinalDeductionAssessment.FinalResult(true).hasFinalScore);
        report.AppendLine($"=== {(failures==0?"PASS":"FAIL")}: {checks} focused checks, {failures} failures ===");Report=report.ToString();IsDone=true;
    }
}
