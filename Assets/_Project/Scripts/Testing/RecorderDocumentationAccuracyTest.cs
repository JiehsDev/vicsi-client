using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>Real four-subject Recorder confirmation, separate points and upload preservation.</summary>
public sealed class RecorderDocumentationAccuracyTest:MonoBehaviour
{
    public bool IsDone{get;private set;}
    public string Report{get;private set;}
    readonly StringBuilder report=new("=== RecorderDocumentationAccuracyTest ===\n");int failures;
    void Check(string name,bool ok){report.AppendLine((ok?"PASS: ":"FAIL: ")+name);if(!ok)failures++;}
    public void RunAllTests(){AutomatedTestSandbox.Activate(nameof(RecorderDocumentationAccuracyTest));StartCoroutine(Run());}
    IEnumerator Run()
    {
        yield return null;
        var state=EvidenceStateManager.Instance;var ui=RecorderReportUI.Instance;
        ReasoningCheckpointManager.Instance.enabled=false;HypothesisCheckpointManager.Instance.enabled=false;
        ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null)NarrativeEventManager.Instance.enabled=false;
        Check("Only the four current Recorder subjects are assessed",RecorderDocumentation.Catalog.subjects.Select(s=>s.id)
            .SequenceEqual(new[]{"BODY-A","EVD-014","EVD-015","EVD-018"})&&!RecorderDocumentation.AppliesTo("witness")
            &&!RecorderDocumentation.AppliesTo("EVD-019")&&!RecorderDocumentation.AppliesTo("FINAL-CONCLUSION"));
        Check("Catalog maximum is separate 24 points",RecorderDocumentation.Catalog.maximumTotal==24&&RecorderDocumentation.Catalog.maximumPerSubject==6);
        Check("Existing deduction weights remain 60/25/15",FinalDeductionAssessment.Rules.checkpointWeight==60
            &&FinalDeductionAssessment.Rules.relationshipWeight==25&&FinalDeductionAssessment.Rules.conclusionWeight==15);
        int perfect=0;
        foreach(var subject in RecorderDocumentation.Catalog.subjects)
        {
            Check(subject.id+" has six stable distinct statements",subject.statements.Count==6&&subject.statements.Select(s=>s.id).Distinct().Count()==6);
            Check(subject.id+" has three 2-point and three 0-point statements",subject.statements.Count(s=>s.weight==2)==3&&subject.statements.Count(s=>s.weight==0)==3);
            var correct=subject.statements.Where(s=>s.weight==2).Select(s=>s.id).ToArray();
            var wrong=subject.statements.Where(s=>s.weight==0).Select(s=>s.id).ToArray();
            perfect+=RecorderDocumentation.Capture(subject.id,correct).earnedPoints;
            Check(subject.id+" earns 6 for supported and 0 for unsupported selections",RecorderDocumentation.Capture(subject.id,correct).earnedPoints==6
                &&RecorderDocumentation.Capture(subject.id,wrong).earnedPoints==0);
            Check(subject.id+" rejects duplicates, foreign IDs and incorrect counts",!RecorderDocumentation.ValidSelection(subject.id,new[]{correct[0],correct[0],correct[1]})
                &&!RecorderDocumentation.ValidSelection(subject.id,new[]{correct[0],correct[1],"witness"})&&!RecorderDocumentation.ValidSelection(subject.id,correct.Take(2)));
        }
        Check("Four perfect entries total 24",perfect==24);
        ScenePhotographyTestUtil.CompleteRoute();
        foreach(var r in state.AllRecords)
        {
            r.status=EvidenceStatus.PhotographedFinal;r.swabbingDone=r.swabSampleSealed=r.definition.requiresSwabbing;
            r.fingerprintingDone=r.fingerprintLifted=r.definition.requiresFingerprinting;r.tentLetter=r.definition.evidenceId=="BODY-A"?"A":null;
        }
        foreach(var subject in RecorderDocumentation.Catalog.subjects){MeasurementTestUtil.RecordExact(subject.id,1);MeasurementTestUtil.RecordExact(subject.id,2);MeasurementTestUtil.RecordAvailablePairsFor(subject.id);}
        Check("Body is sketched before Recorder confirmation",MeasurementTestUtil.CompleteSketchFor("BODY-A"));
        foreach(var subject in RecorderDocumentation.Catalog.subjects)
        {
            STCSNotificationUI.Instance.HideImmediately();NotificationManager.Instance.UI.ClearTooltip();
            Check(subject.id+" real Recorder opens",RecorderTool.Instance.TryOpenReport(EvidenceProp.FindById(subject.id))&&ui.IsShowing);
            var chosen=subject.statements.Where(s=>s.weight==(subject.id=="BODY-A"?0:2)).Select(s=>s.id).ToArray();
            foreach(var id in chosen)Check(subject.id+" selects "+id,ui.SelectLineForTest(id));
            Check(subject.id+" three choices confirm and lock",ui.Confirm()==TransitionResult.Applied&&!ui.IsShowing&&!RecorderTool.Instance.TryOpenReport(EvidenceProp.FindById(subject.id)));
            var snapshot=state.GetRecord(subject.id).recorderDocumentation;
            Check(subject.id+" freezes six statements and exactly three selections",snapshot!=null&&snapshot.statements.Count==6&&snapshot.statements.Count(s=>s.selected)==3
                &&snapshot.maximumPossiblePoints==6&&snapshot.earnedPoints==(subject.id=="BODY-A"?0:6));
            Check(subject.id+" stores only actual choices",state.GetRecord(subject.id).selectedReportLineIds.SetEquals(chosen));
            state.RecordReportLinesSelected(subject.id,chosen.Concat(new[]{"foreign-statement"}));
            Check(subject.id+" rejects changes to confirmed choices",state.GetRecord(subject.id).selectedReportLineIds.SetEquals(chosen));
            yield return null;
        }
        Check("Zero-point body documentation does not block body removal",state.GetBodyRemovalBlockReason("BODY-A")==null);
        Check("Baseline body finding remains independent of Recorder points",FindingStateManager.Instance.IsConfirmed("FIND-BODY-SHARP-WOUND"));
        Check("No scoring feedback is displayed in Recorder UI",ui.GetComponentsInChildren<TMP_Text>(true).All(t=>!new[]{"correct","incorrect","weight","points","score"}.Any(word=>t.text.ToLowerInvariant().Contains(word))));
        var input=FinalDeductionAssessment.CaptureLive();
        Check("Deduction snapshot excludes Recorder points",!JsonUtility.ToJson(input).Contains("recorderDocumentation"));
        SessionLogger.Instance.Flush();
        var config=ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();config.uploadRawEventLogEnabled=false;
        var scenario=Resources.FindObjectsOfTypeAll<ScenarioDefinition>().First(s=>s.scenarioId==FinalDeductionAssessment.Rules.scenarioId);
        var dto=AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,"","");
        var recordings=dto.orderedEvents.Where(e=>e.eventType=="ReportLinesSelected").ToArray();
        Check("Minimal upload preserves four versioned Recorder snapshots",recordings.Length==4&&recordings.All(e=>e.payload.Any(p=>p.key=="documentationVersion"&&p.value==RecorderDocumentation.Catalog.version.ToString())));
        Check("Uploaded Recorder total is 18/24 without a combined grade",recordings.Sum(e=>JsonUtility.FromJson<RecorderSubjectResult>(e.payload.Find(p=>p.key=="documentation").value).earnedPoints)==18);
        var json=EducationalSessionRecord.Serialize(dto,true);
        Check("Wire upload retains selected text, weights and points",json.Contains("documentationVersion")&&json.Contains("earnedPoints")&&json.Contains("rec-body-fall-v1"));
        Directory.CreateDirectory("docs/evidence/recorder-documentation-2026-10-21");
        File.WriteAllText("docs/evidence/recorder-documentation-2026-10-21/current-unity-upload.json",json);
        Object.Destroy(config);report.AppendLine(failures==0?"=== PASS ===":"=== FAIL ===");Report=report.ToString();IsDone=true;
    }
}
