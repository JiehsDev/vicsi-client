using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

public sealed class SceneAccessRegisterTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== SceneAccessRegisterTest ===\n");
    int failures;
    InputDevice device;
    Oculus.Interaction.Locomotion.FirstPersonLocomotor locomotor;
    void Check(string label, bool ok) { report.AppendLine((ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    static void Clear() { STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip(); }
    static int Count(SessionEventType type) => SessionLogger.Instance.EventCounts.TryGetValue(type, out int n) ? n : 0;
    IEnumerator Press(string button, bool down)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)device[button], down ? 1f : 0f); yield return null; yield return null;
    }
    IEnumerator Move(Vector3 feet, float settling = .4f)
    {
        locomotor.HandleLocomotionEvent(new Oculus.Interaction.Locomotion.LocomotionEvent(-31000, new Pose(feet, Quaternion.identity),
            Oculus.Interaction.Locomotion.LocomotionEvent.TranslationType.Absolute, Oculus.Interaction.Locomotion.LocomotionEvent.RotationType.None));
        yield return new WaitForSecondsRealtime(settling);
    }
    IEnumerator Run()
    {
        yield return null;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        // Missing ownership alone must never authorize a lobby session. Only the
        // controller's explicit direct-start decision may permit an unassigned run.
        var flowCheck = new GameObject("Direct-start authorization scope");
        flowCheck.SetActive(false);
        var directFlow = flowCheck.AddComponent<PreSessionFlowController>();
        Check("Fresh identity flow rejects ownerless check-in", !directFlow.AllowsUnassignedCheckIn("direct-session"));
        typeof(PreSessionFlowController).GetField("sessionUnlocked", flags).SetValue(directFlow, true);
        Check("Unlocked lobby flow still rejects ownerless check-in", !directFlow.AllowsUnassignedCheckIn("direct-session"));
        typeof(PreSessionFlowController).GetField("unassignedDirectSessionId", flags).SetValue(directFlow, "direct-session");
        Check("Explicit ownerless direct run permits its own check-in", directFlow.AllowsUnassignedCheckIn("direct-session"));
        Check("Direct-run permission cannot carry into a different session", !directFlow.AllowsUnassignedCheckIn("next-session")
            && !directFlow.AllowsUnassignedCheckIn(null) && !directFlow.AllowsUnassignedCheckIn(string.Empty));
        typeof(PreSessionFlowController).GetField("sessionUnlocked", flags).SetValue(directFlow, false);
        Check("Locked direct run cannot check in", !directFlow.AllowsUnassignedCheckIn("direct-session"));
        Destroy(flowCheck);
        var pre = FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null) { pre.enabled = false; PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken", flags).GetValue(pre)); }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        if (ProceduralTrainingGuidance.Instance != null) ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        Clear();
        var cordon = FindAnyObjectByType<SceneCordon>(); var model = cordon.AccessRegister; var ui = model.UI;
        Check("One runtime access register is installed outside the cordon", FindObjectsByType<SceneAccessRegister>().Length == 1 && ui != null
            && !cordon.IsWithinSecurityFootprint(ui.transform.position));
        var original = Camera.main; var originalTag = original.tag; original.tag = "Untagged";
        var rig = original.GetComponentInParent<OVRCameraRig>(); bool rigEnabled = rig.enabled; rig.enabled = false;
        var go = new GameObject("Access register validation head", typeof(Camera)); var cam = go.GetComponent<Camera>(); cam.tag = "MainCamera";
        go.transform.SetParent(rig.transform, false); go.transform.localPosition = Vector3.up * 1.75f;
        locomotor = FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(); locomotor.InjectPlayerEyes(cam.transform);
#if UNITY_EDITOR
        BatchLocomotionFramePump.Activate();
#endif
        var capsule = FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        var owner = new IdentifiedStudent { StudentId = "scene-access-test-student", DisplayName = "Alexandra Maria de los Santos - Student Investigator With A Long Display Name", IsTestSession = true };
        // Bind the isolated test session, never a real student's owner store.
        var currentOwner = SessionOwnership.Store.Find(SessionLogger.Instance.SessionId);
        if (currentOwner == null) SessionOwnership.BindCurrentSession(owner);
        string expectedId = SessionOwnership.Store.Find(SessionLogger.Instance.SessionId).studentId;
        string expectedName = SessionOwnership.Store.Find(SessionLogger.Instance.SessionId).displayNameSnapshot;
        InputSystem.RegisterLayout("{\"name\":\"SceneAccessController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        device = InputSystem.AddDevice("SceneAccessController"); InputSystem.SetDeviceUsage(device, CommonUsages.RightHand);
        try
        {
            yield return null; yield return null;
            var home = capsule.Pose.position - Vector3.up * capsule.Height * .5f;
            // The register is beside the house entrance, no longer beside the spawn.
            var registerApproach=ui.transform.position-ui.transform.forward*.7f;
            registerApproach.y=home.y;home=registerApproach;
            yield return Move(home);
            var entry = (cordon.transform.Find("EntrancePost_L").position + cordon.transform.Find("EntrancePost_R").position) * .5f;
            float houseFloor=entry.y-.5f;
            var inside = new Vector3(entry.x, houseFloor, entry.z + .7f);
            Check("Fresh session has no authorization or invented NPC visits", !model.IsAuthorized && model.Visits.Count == 0 && !cordon.CanEnter);
            Check("Register cannot open before briefing", !ui.Open());
            Check("Security briefing names keeper and explains access purpose", GameDialogue.Text("FR_SECURITY_01").Contains("I will maintain")
                && GameDialogue.Text("FR_SECURITY_01").Contains("disturbance and contamination")
                && SceneTurnover.Controller.DialogueLineIds.Count(id => id == "FR_SECURITY_01") == 1);
            SceneTurnover.Controller.CompleteWithoutDialogue(); Clear();
            Check("Briefing alone does not open register without gloves", !ui.Open());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            cordon.PPEStation.PrepareDevelopmentGloves();
#endif
            Check("Gloves alone do not authorize entry or IOC observation", !cordon.CanEnter && !SceneTurnover.RecordIOCObservation("ioc-victim"));
            int returns = cordon.EntryReturns;
            yield return Move(inside);
            Check("Denied SDK crossing returns both head and capsule with no visit", cordon.EntryReturns > returns
                && !cordon.IsWithinSecurityFootprint(cam.transform.position) && !cordon.IsWithinSecurityFootprint(capsule.Pose.position) && model.Visits.Count == 0);
            var secured=(Bounds)typeof(SceneCordon).GetField("protectedArea",flags).GetValue(cordon);
            var securedMin=cordon.transform.TransformPoint(secured.min);var securedMax=cordon.transform.TransformPoint(secured.max);
            var boundaryProbes=new[]{new Vector3(securedMin.x+.4f,houseFloor,securedMin.z+.7f),
                new Vector3(securedMax.x-.4f,houseFloor,securedMin.z+.7f),new Vector3((securedMin.x+securedMax.x)*.5f,houseFloor,securedMax.z-.4f)};
            foreach(var probe in boundaryProbes)
            {
                Clear();int previousReturns=cordon.EntryReturns;yield return Move(probe);
                Check("Unauthorised house crossing returns head and capsule at "+probe.ToString("F2"),cordon.EntryReturns>previousReturns
                    &&!cordon.IsWithinSecurityFootprint(cam.transform.position)&&!cordon.IsWithinSecurityFootprint(capsule.Pose.position)&&model.Visits.Count==0);
            }
            Clear();
            var token = new object(); PlayerUIGate.Enter(token); Check("Other modal UI prevents registration", !ui.Open()); PlayerUIGate.Exit(token);
            yield return Press("primaryButton", false); yield return Press("primaryButton", true);
            Check("Actual A opens review without signing", ui.IsShowing && !model.IsAuthorized);
            yield return new WaitForSecondsRealtime(.35f);
            Check("Holding opening A cannot confirm", ui.IsShowing && !model.IsAuthorized);
            yield return Press("primaryButton", false); yield return Press("secondaryButton", true);
            Check("Actual B cancels with no authorization or signature event", !ui.IsShowing && !model.IsAuthorized && Count(SessionEventType.SceneLogSigned) == 0 && !PlayerUIGate.IsBlocked);
            yield return Press("secondaryButton", false);
            Check("Review can reopen", ui.Open());
            yield return Move(home + Vector3.left * 4);
            Check("Walking away cancels without signing", !ui.IsShowing && !model.IsAuthorized && !ui.ConfirmCheckIn());
            yield return Move(home); Clear();
            yield return Press("primaryButton", true); yield return Press("primaryButton", false);
            yield return new WaitForSecondsRealtime(.3f);
            yield return Press("primaryButton", true); yield return Press("primaryButton", false);
            Check("Separate actual A confirms check-in", model.IsAuthorized && !ui.IsShowing && cordon.CanEnter && Count(SessionEventType.SceneLogSigned) == 1);
            Check("Confirmation freezes existing session identity", model.StudentId == expectedId && model.StudentName == expectedName);
            Check("Signing means awaiting entry, not an invented visit", model.Visits.Count == 0 && Count(SessionEventType.SceneAccessEntered) == 0);
            Check("Registration sidecar exists beside the isolated session log", model.LocalPath.StartsWith(AutomatedTestSandbox.Root) && File.Exists(model.LocalPath));
            Clear(); Check("Confirmed register can be reviewed", ui.Open());
            yield return new WaitForSecondsRealtime(.3f);
            Check("Repeated confirmation cannot sign twice", !ui.ConfirmCheckIn() && Count(SessionEventType.SceneLogSigned) == 1); ui.Hide();
            yield return Move(inside);
            Check("First observed entry creates exactly one open visit", model.Visits.Count == 1 && model.Visits[0].timeOutMs < 0 && model.IsInside && Count(SessionEventType.SceneAccessEntered) == 1);
            yield return new WaitForSecondsRealtime(.5f);
            Check("Remaining inside never duplicates visits", model.Visits.Count == 1 && Count(SessionEventType.SceneAccessEntered) == 1);
            // The approach and protected-area boxes share an internal join at protectedArea.min.z.
            var protectedBounds = (Bounds)typeof(SceneCordon).GetField("protectedArea", flags).GetValue(cordon);
            float join = cordon.transform.TransformPoint(protectedBounds.min).z;
            yield return Move(new Vector3(entry.x, houseFloor, join - .03f));
            yield return Move(new Vector3(entry.x, houseFloor, join + .03f));
            Check("House doorway-to-interior boundary join is not a physical exit", model.Visits.Count == 1 && model.Visits[0].timeOutMs < 0 && Count(SessionEventType.SceneAccessExited) == 0);
            yield return Move(home);
            Check("Actual exit closes the visit with ordered elapsed times", model.Visits.Count == 1 && model.Visits[0].timeOutMs >= model.Visits[0].timeInMs && Count(SessionEventType.SceneAccessExited) == 1);
            for (int i = 0; i < 8; i++) yield return Move(new Vector3(entry.x, houseFloor, entry.z + (i % 2 == 0 ? .01f : -.01f)), .05f);
            Check("Small boundary jitter creates no additional visit", model.Visits.Count == 1 && Count(SessionEventType.SceneAccessEntered) == 1);
            yield return Move(inside);
            Check("Re-entry opens a new visit without another check-in", model.Visits.Count == 2 && Count(SessionEventType.SceneLogSigned) == 1);
            foreach (var observation in FindAnyObjectByType<IOCAssessmentInteraction>().Definition.observations) SceneTurnover.RecordIOCObservation(observation.observationId);
            SceneTurnover.CompleteIOCAssessment(); SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested); Clear();
            int exits = Count(SessionEventType.SceneAccessExited);
            Check("Original scripted SOCO arrival remains available", FindAnyObjectByType<SOCOWorkflowInteraction>().AcknowledgeArrival());
            while (FindAnyObjectByType<SOCOWorkflowInteraction>().IsArriving) yield return null;
            yield return new WaitForSecondsRealtime(.4f); Clear();
            Check("Scripted reposition records no invented physical exit", model.Visits.Count == 2 && model.Visits[1].timeOutMs < 0
                && model.Visits[1].simulatedRepositionMs >= 0 && Count(SessionEventType.SceneAccessExited) == exits);
            SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation); Clear();
            Check("IOC/SOCO handover retains one identity and authorization", model.StudentId == expectedId && model.IsAuthorized
                && Count(SessionEventType.SceneAccessRoleChanged) == 1 && model.Transitions.Count == 2 && Count(SessionEventType.SceneLogSigned) == 1);
            // Current scene requires the outdoor photographs before SOCO re-entry.
            // Complete those actual shutter actions before testing vendor exit/return.
            var photos = ScenePhotographyManager.Instance;
            foreach (var phase in new[] { ScenePhotoPhase.Exterior, ScenePhotoPhase.Entrance })
            {
                var point = photos.Points.First(p => p.phase == phase);
                Vector3 oldHead = cam.transform.position; Quaternion oldFacing = cam.transform.rotation;
                cam.transform.position = point.transform.position + Vector3.up * 1.6f;
                cam.transform.rotation = point.cameraTarget != null ? Quaternion.LookRotation(point.cameraTarget.position - cam.transform.position) : point.transform.rotation;
                typeof(ScenePhotographyManager).GetMethod("HandleShutter", flags).Invoke(photos,
                    new object[] { FindAnyObjectByType<PhotographTool>(FindObjectsInactive.Include), false });
                cam.transform.SetPositionAndRotation(oldHead, oldFacing);
            }
            Check("Outdoor photography opens the current SOCO entry gate", photos.IsPhaseCompleted(ScenePhotoPhase.Exterior)
                && photos.IsPhaseCompleted(ScenePhotoPhase.Entrance) && cordon.CanEnter);
            yield return Move(inside);
            var vendor = FindAnyObjectByType<VendorWitnessInteraction>();
            Check("Vendor destination lies outside the secured footprint", vendor != null && !cordon.IsWithinSecurityFootprint(vendor.transform.position));
            if (vendor != null) yield return Move(new Vector3(vendor.transform.position.x, home.y, vendor.transform.position.z));
            Check("Vendor trip records a genuine exit", model.Visits.Count == 3 && model.Visits[2].roleAtEntry == "SOCO" && model.Visits[2].timeOutMs >= 0);
            yield return Move(inside);
            Check("Return from vendor records a fourth visit for same student", model.Visits.Count == 4 && model.Visits[3].timeOutMs < 0 && model.StudentId == expectedId);
            yield return Move(home); Clear(); Check("Visit review opens outside", ui.Open());
            ui.Refresh(); Canvas.ForceUpdateCanvases();
            Check("Register uses bounded scrolling for visit history", ui.Layout.Body.CanScroll && ui.PanelRect.rect.height <= 820.1f);
            var labels = ui.PanelRect.GetComponentsInChildren<TMP_Text>(true);
            Check("Readable rows show name, role, purpose, times and status without markup interpretation", labels.All(t => !t.richText)
                && labels.Any(t => t.text.Contains("Role at entry:") && t.text.Contains("Time in:") && t.text.Contains("Time out:") && t.text.Contains("Status:")));
            Preview(cam, ui); ui.Hide();
            yield return Move(inside); // A further real visit remains open at scenario end.
            long lastEntry = model.Visits.Last().timeInMs; int visitsAtEnd = model.Visits.Count;
            SessionLogger.Instance.EndSession();
            Check("Session end labels presence without inventing an exit", model.IsFinalized && model.Visits.Last().presentAtScenarioEnd && model.Visits.Last().timeOutMs < 0);
            var saved = JsonUtility.FromJson<SceneAccessRegister.Record>(File.ReadAllText(model.LocalPath));
            Check("Full finalized register survives local JSON round trip", saved.finalized && saved.studentId == expectedId && saved.visits.Count == visitsAtEnd
                && saved.visits.Last().timeInMs == lastEntry && saved.visits.Last().timeOutMs == -1 && saved.transitions.Count == 2);
            File.Copy(model.LocalPath, "docs/evidence/scene-security/register-example.json", true);
            model.ObserveBoundary(home, home, true); yield return new WaitForSecondsRealtime(.3f); model.ObserveBoundary(home, home, true);
            Check("Frozen register ignores later boundary samples", model.Visits.Count == visitsAtEnd && model.Visits.Last().timeOutMs < 0);
            // Traversing the real house can legitimately discover evidence by proximity.
            Check("Cordon stays secured and no evidence is processed by entry logging", cordon.CordonVisible && !cordon.EntranceIsOpen
                && EvidenceStateManager.Instance.AllRecords.All(r => r.status <= EvidenceStatus.Found));
        }
        finally
        {
            ui.Hide(); locomotor.InjectPlayerEyes(original.transform); original.tag = originalTag; rig.enabled = rigEnabled;
            if (device != null) InputSystem.RemoveDevice(device); Destroy(go);
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); yield return null; yield return null;
        var fresh = FindAnyObjectByType<SceneCordon>();
        Check("Fresh investigation resets registration and all visit records", !fresh.AccessRegister.IsAuthorized && fresh.AccessRegister.Visits.Count == 0
            && !fresh.CanEnter && !fresh.PPEReady && SceneTurnover.IsPending);
        report.AppendLine(failures == 0 ? "=== PASS ===" : "=== FAIL: " + failures + " failure(s) ==="); Report = report.ToString(); IsDone = true;
    }
    private void Preview(Camera cam, SceneAccessRegisterUI ui)
    {
        var target = RenderTexture.GetTemporary(1120, 900, 24); var oldTarget = cam.targetTexture; var oldActive = RenderTexture.active;
        var texture = new Texture2D(1120, 900, TextureFormat.RGB24, false);
        try
        {
            cam.targetTexture = target; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1120, 900), 0, 0); texture.Apply();
            File.WriteAllBytes("docs/evidence/scene-security/register-preview.png", texture.EncodeToPNG());
            Check("Native register review is rendered with active world-space panel", ui.IsShowing && texture.GetPixels32().Count(c => c.r > 150 && c.g > 150 && c.b > 150) > 100);
        }
        finally { cam.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(target); Destroy(texture); }
    }
}
