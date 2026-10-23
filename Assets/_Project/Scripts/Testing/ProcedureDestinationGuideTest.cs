#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authored continuation waypoints follow live progress without taking input or changing evidence.</summary>
public sealed class ProcedureDestinationGuideTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== ProcedureDestinationGuideTest ===\n");
    int failures;
    void Check(string label, bool value) { report.AppendLine((value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(ProcedureDestinationGuideTest));
        int previous = SessionState.GetInt(SceneTestStart.EditorSelectionKey, -1);
        bool diagnostic = SessionState.GetBool("ViCSI.SceneTestStart.Diagnostic", false);
        SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic", true);
        SessionState.SetInt(SceneTestStart.EditorSelectionKey, (int)SceneTestStartingPoint.NormalPlay);
        GameObject observer = null;
        try
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i=0;i<5;i++) yield return null;
            RelationshipReviewTestUtil.BeginCaseFixture();
            var pre = FindAnyObjectByType<PreSessionFlowController>();
            if (pre != null) { pre.enabled=false; PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pre)); }
            FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
            if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled=false; NarrativeEventManager.Instance.StopAllCoroutines(); }
            var rig = FindAnyObjectByType<OVRCameraRig>(); rig.enabled=false; Camera.main.tag="Untagged";
            observer = new GameObject("Procedure guidance observer",typeof(Camera)); observer.tag="MainCamera";
            var camera = observer.GetComponent<Camera>(); camera.fieldOfView=60; camera.aspect=4f/3; camera.enabled=true;
            camera.transform.position=new Vector3(40,1.7f,40);
            var guide = FindAnyObjectByType<ProcedureDestinationGuide>();
            Check("One editable scene guide and saved canvas exist", guide!=null&&FindObjectsByType<ProcedureDestinationGuide>().Length==1
                &&guide.GuideCanvas!=null&&guide.GuideCanvas.transform.IsChildOf(guide.transform));
            guide.Refresh();Check("FR approach stays with its existing guide",guide.TargetId==null&&!guide.IsVisible);
            SceneTurnover.Controller.CompleteWithoutDialogue();Clear();guide.Refresh();
            Check("Initial gloves remain the first destination",guide.TargetId==null&&ArrivalPPEStation.Instance.GuidanceNeeded);
            ArrivalPPEStation.Instance.PrepareDevelopmentGloves();Clear();guide.Refresh();
            Check("Clean gloves lead to the entry-register waypoint",guide.TargetId=="access-register"&&guide.IsVisible&&guide.GuidanceText.Contains("register"));
            var token=new object();PlayerUIGate.Enter(token);guide.Refresh();Check("Modal panels hide navigation",!guide.IsVisible);PlayerUIGate.Exit(token);guide.Refresh();
            using(ProcedureDestinationGuide.HideForCapture())
            {
                guide.Refresh();Check("Photographs suppress the destination marker",!guide.IsVisible);
                using(ProcedureDestinationGuide.HideForCapture()) { guide.Refresh();Check("Nested capture remains hidden",!guide.IsVisible); }
                guide.Refresh();Check("Outer capture continues suppressing the marker",!guide.IsVisible);
            }
            Check("Navigation returns after capture without owning input",guide.IsVisible&&!PlayerUIGate.IsBlocked&&!ToolInputLock.HasUIOwners);
            guide.enabled=false;Check("Disable hides the authored canvas",!guide.IsVisible);guide.enabled=true;guide.Refresh();
            FindAnyObjectByType<SceneCordon>().AccessRegister.PrepareDevelopmentAuthorization();guide.Refresh();
            Check("Authorization retires register guidance for IOC assessment",guide.TargetId==null);
            foreach(var observation in FindAnyObjectByType<IOCAssessmentInteraction>().Definition.observations) SceneTurnover.RecordIOCObservation(observation.observationId);
            SceneTurnover.CompleteIOCAssessment();SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested);
            SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover);SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation);Clear();guide.Refresh();
            Check("Scene photography keeps its existing guide priority",guide.TargetId==null);
            Check("Actual photograph route completes",ScenePhotographyTestUtil.CompleteRoute());Clear();
            var before=FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(),false,FinalDeductionAssessment.Rules);
            foreach(string id in new[]{"BODY-A","EVD-014","EVD-015","EVD-018"})
            {
                camera.transform.position=EvidenceProp.FindById(id).transform.position+Vector3.up*.6f;
                guide.Refresh();
                Check("Local evidence work has no waypoint: "+EvidenceDisplay.Label(id),!guide.IsVisible&&guide.TargetId==null&&guide.GuidanceText==null);
            }
            var after=FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(),false,FinalDeductionAssessment.Rules);
            Check("Navigation leaves assessment values unchanged",before.totalScore==after.totalScore&&before.reasoningComponent==after.reasoningComponent&&before.relationshipComponent==after.relationshipComponent&&before.conclusionComponent==after.conclusionComponent);
        }
        finally { if(observer!=null)Destroy(observer); SessionState.SetInt(SceneTestStart.EditorSelectionKey,previous);SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic",diagnostic); }
        report.AppendLine(failures==0?"=== PASS ===":"=== FAIL ===");Report=report.ToString();IsDone=true;
    }
    static void Clear() { STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip(); }
}
#endif
