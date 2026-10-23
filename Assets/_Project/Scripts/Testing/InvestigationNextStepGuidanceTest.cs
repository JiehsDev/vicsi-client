#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class InvestigationNextStepGuidanceTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== InvestigationNextStepGuidanceTest ===\n");
    private int failures;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Check(string name, bool value) { report.AppendLine((value ? "PASS: " : "FAIL: ") + name); if (!value) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(InvestigationNextStepGuidanceTest));
        int original = SessionState.GetInt(SceneTestStart.EditorSelectionKey, -1);
        bool diagnostic = SessionState.GetBool("ViCSI.SceneTestStart.Diagnostic", false);
        SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic", true);
        SessionState.SetInt(SceneTestStart.EditorSelectionKey, (int)SceneTestStartingPoint.AllEvidenceProceduresDone);
        try
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 12; i++) yield return null;
            var flow = SOCOCloseout.Instance;
            var review = EvidenceRelationshipsReview.Instance;
            var state = EvidenceStateManager.Instance;
            var inventory = EvidenceInventory.Instance;
            ProceduralTrainingGuidance.Instance.enabled = false;
            STCSNotificationUI.Instance.HideImmediately();
            NotificationManager.Instance.UI.ClearTooltip(); NotificationManager.Instance.UI.HidePrompt();
            Check("Real completed-evidence fixture is ready", SceneTestStart.Instance.IsReady && SceneTestStart.Instance.Failure == null
                && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation && flow.AllAccountedFor);
            var rig = FindAnyObjectByType<OVRCameraRig>(); rig.enabled = false;
            var camera = Camera.main;
            camera.transform.position = EvidenceBoxDepositZone.Instance.transform.position + new Vector3(0, 1.6f, -4);
            camera.transform.rotation = Quaternion.Euler(0, 0, 0);
            flow.RefreshObjective();
            Check("Completed collection offers evidence linking before conclusion", SOCOCloseout.SuggestedInvestigationMenuEntry == "Relationships"
                && flow.IsObjectiveVisible && flow.ObjectiveGuidance.Contains("Next: Link evidence"));
            Check("Hint names the actual wheel label and controller actions", flow.ObjectiveGuidance.Contains("Evidence Links")
                && flow.ObjectiveGuidance.Contains("Hold [X]") && flow.ObjectiveGuidance.Contains("right stick") && flow.ObjectiveGuidance.Contains("release [X]"));
            Check("Guidance does not auto-open review or record an answer", !review.IsShowing && review.Responses.Count == 0 && !PlayerUIGate.IsBlocked);
            var waypoint = EvidenceBoxDepositZone.Instance.GetComponent<EvidenceBoxWaypoint>(); waypoint.Refresh();
            Check("Custody waypoint does not compete with investigation guidance", !waypoint.DestinationActive && !waypoint.IsVisible);
            var knife = state.GetRecord("EVD-014"); knife.swabSampleSealed = false; flow.RefreshObjective();
            Check("Incomplete evidence suppresses the handoff", SOCOCloseout.SuggestedInvestigationMenuEntry == null && !flow.IsObjectiveVisible);
            knife.swabSampleSealed = true; flow.RefreshObjective();
            Check("Completing evidence restores the persistent hint", flow.IsObjectiveVisible);
            PlayerUIGate.Enter(this); flow.RefreshObjective(); Check("Open panels suppress the hint", !flow.IsObjectiveVisible); PlayerUIGate.Exit(this);
            NotificationManager.Instance.UI.ShowTooltip("Store the item first.", 10); flow.RefreshObjective();
            Check("Immediate action feedback takes priority", !flow.IsObjectiveVisible);
            NotificationManager.Instance.UI.ClearTooltip(); flow.RefreshObjective();
            yield return null; yield return null;
            var objective = (GameObject)typeof(SOCOCloseout).GetField("objectivePanel", Private).GetValue(flow);
            var label = objective.GetComponentInChildren<TMPro.TMP_Text>(); label.ForceMeshUpdate();
            Check("Persistent instruction fits its backed panel", !label.isTextOverflowing && objective.GetComponentInChildren<UnityEngine.UI.Image>() != null);
            Capture(camera, "link-evidence-hint.png");

            var menu = FindAnyObjectByType<UtilityMenuController>();
            typeof(UtilityMenuController).GetMethod("OpenMenu", Private).Invoke(menu, null);
            flow.RefreshObjective();
            Check("Menu hides the persistent instruction", menu.IsCanvasActive && !flow.IsObjectiveVisible);
            var nextLabel = menu.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t.text.Contains("NEXT") && t.text.Contains("Links"));
            nextLabel?.ForceMeshUpdate();
            Check("Evidence Links is visibly marked NEXT without clipping", nextLabel != null && !nextLabel.isTextOverflowing);
            yield return null; yield return null;
            Capture(camera, "evidence-links-menu.png");
            var segments = (System.Collections.IList)typeof(UtilityMenuController).GetField("segmentViews", Private).GetValue(menu);
            int index = Enumerable.Range(0, segments.Count).First(i => ((UtilityMenuController.MenuEntry)segments[i].GetType().GetField("Entry").GetValue(segments[i])).displayName == "Relationships");
            typeof(UtilityMenuController).GetField("hoveredIndex", Private).SetValue(menu, index);
            typeof(UtilityMenuController).GetMethod("CloseMenu", Private).Invoke(menu, null);
            yield return null;
            Check("The highlighted entry opens the real relationship review", review.IsShowing && review.Responses.Count == 0);
            flow.RefreshObjective(); Check("Review suppresses the hint without advancing progress", !flow.IsObjectiveVisible);
            review.Hide(); flow.RefreshObjective();
            Check("Closing unanswered review restores the hint", flow.IsObjectiveVisible && SOCOCloseout.SuggestedInvestigationMenuEntry == "Relationships");
            foreach (var question in review.EligibleQuestions())
                Check("Review response recorded: " + question.id, review.ConfirmResponse(question.id, question.correctInterpretationId));
            flow.RefreshObjective();
            Check("Completed relationships lead to the unfinished sketch", review.IsReadyForConclusion && SOCOCloseout.SuggestedInvestigationMenuEntry == "Scene Sketch"
                && flow.ObjectiveGuidance.Contains("Scene Sketch"));
            MeasurementTestUtil.CompleteSceneSketch();
            STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip(); flow.RefreshObjective();
            Check("Submitted sketch leads to the actual conclusion menu label", SOCOCloseout.SuggestedInvestigationMenuEntry == "Submit Conclusion"
                && flow.IsObjectiveVisible && flow.ObjectiveGuidance.Contains("Conclude"));
            Check("Guidance preserves evidence and requires an explicit conclusion", inventory.AllEntryIds.Count() == 6 && flow.AllAccountedFor
                && EndingManager.Instance.AcceptedEnding == null && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation);
        }
        finally
        {
            PlayerUIGate.Exit(this);
            SessionState.SetInt(SceneTestStart.EditorSelectionKey, original);
            SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic", diagnostic);
        }
        report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failures ===");
        Report = report.ToString(); IsDone = true;
    }
    private void Capture(Camera camera, string filename)
    {
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1200, 900, 24); var texture = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); texture.Apply();
            const string folder = "docs/evidence/investigation-next-step-2026-10-23";
            System.IO.Directory.CreateDirectory(folder); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, filename), texture.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Destroy(texture); }
    }
}
#endif
