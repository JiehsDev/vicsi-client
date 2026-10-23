#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Physical custody can finish before deduction, without advancing or scoring a conclusion.</summary>
public sealed class EarlyCustodyPassTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== EarlyCustodyPassTest ===\n");
    int failures;
    void Check(string label, bool value) { report.AppendLine((value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(EarlyCustodyPassTest));
        int previous = SessionState.GetInt(SceneTestStart.EditorSelectionKey, -1);
        bool diagnostic = SessionState.GetBool("ViCSI.SceneTestStart.Diagnostic", false);
        SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic", true);
        SessionState.SetInt(SceneTestStart.EditorSelectionKey, (int)SceneTestStartingPoint.AllEvidenceProceduresDone);
        try
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 10; i++) yield return null;
            RelationshipReviewTestUtil.BeginCaseFixture();
            Check("Physical-completion preset is ready", SceneTestStart.Instance.IsReady && SceneTestStart.Instance.Failure == null);
            var box = EvidenceBoxDepositZone.Instance;
            var inventory = EvidenceInventory.Instance;
            var closeout = SOCOCloseout.Instance;
            Check("Conclusion feedback is still pending", SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation && !EndingManager.Instance.FeedbackAccepted);
            Check("All physical evidence is ready independently of deduction", closeout.AllAccountedFor && box.ReadyForTransfer && inventory.AllEntryIds.Count() == 6);
            var before = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
            FindAnyObjectByType<OVRCameraRig>().enabled = false;
            var head = Camera.main.transform;
            head.position = box.GetComponent<Collider>().bounds.center + Vector3.up * 1.6f;
            head.LookAt(box.transform.position + Vector3.up);
            STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip();
            Check("Player can open custody review before conclusion feedback", box.PlayerInRange && box.RequestCustodyPass());
            yield return new WaitForSecondsRealtime(.3f);
            Check("Explicit confirmation transfers all six packaged items", box.ConfirmCustodyPass() && box.PassComplete && box.Contents.ItemCount == 6 && inventory.AllCustodyDeposited);
            Check("Box custody still satisfies physical completion", closeout.AllAccountedFor);
            Check("Physical transfer does not advance the deduction or closeout phase", SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation && !EndingManager.Instance.FeedbackAccepted
                && !SceneTurnover.TryAdvance(ScenarioPhase.SOCOCloseout) && !SceneTurnover.TryAdvance(ScenarioPhase.FinalSceneSurvey));
            var after = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
            Check("The 60/25/15 assessment values are unchanged by custody", before.totalScore == after.totalScore && before.reasoningComponent == after.reasoningComponent
                && before.relationshipComponent == after.relationshipComponent && before.conclusionComponent == after.conclusionComponent);
            var guide = box.GetComponent<EvidenceBoxWaypoint>(); guide.Refresh();
            Check("Custody waypoint retires after transfer", !guide.DestinationActive && !guide.IsVisible);
        }
        finally
        {
            SessionState.SetInt(SceneTestStart.EditorSelectionKey, previous);
            SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic", diagnostic);
        }
        report.AppendLine(failures == 0 ? "=== PASS ===" : "=== FAIL ==="); Report = report.ToString(); IsDone = true;
    }
}
#endif
