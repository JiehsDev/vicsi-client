using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>Real briefing/controller input, entry enforcement, tool independence and fresh-session reset.</summary>
public sealed class ArrivalPPETest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== ArrivalPPETest ===\n");
    int failures;
    InputDevice controller;
    void Check(string name, bool ok) { report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); if (!ok) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    static int Count(SessionEventType type) => SessionLogger.Instance.EventCounts.TryGetValue(type, out int n) ? n : 0;
    static void Clear() { STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip(); }
    IEnumerator Press(string button, bool down)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)controller[button], down ? 1f : 0f);
        yield return null; yield return null;
    }
    IEnumerator Run()
    {
        yield return new WaitForSeconds(1);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pre = FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null) { pre.enabled = false; PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken", flags).GetValue(pre)); }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        if (ProceduralTrainingGuidance.Instance != null) ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        Clear();
        var cordon = FindAnyObjectByType<SceneCordon>(); var station = cordon.PPEStation;
        Check("Exactly one clean-glove station is installed", station != null && FindObjectsByType<ArrivalPPEStation>(FindObjectsSortMode.None).Length == 1);
        if (station == null) { Finish(); yield break; }
        var original = Camera.main; string oldTag = original.tag; original.tag = "Untagged";
        var rig = original.GetComponentInParent<OVRCameraRig>(); bool rigEnabled = rig.enabled; rig.enabled = false;
        var cameraObject = new GameObject("Arrival PPE validation head", typeof(Camera));
        var cam = cameraObject.GetComponent<Camera>(); cam.tag = "MainCamera";
        cameraObject.transform.SetParent(rig.transform, false); cameraObject.transform.localPosition = Vector3.up * 1.75f;
        var fixtureLocomotor = FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
        fixtureLocomotor.InjectPlayerEyes(cam.transform);
        var home = cameraObject.transform.position;
        var protectedArea = (Bounds)typeof(SceneCordon).GetField("protectedArea", flags).GetValue(cordon);
        Vector3 inside = cordon.transform.TransformPoint(protectedArea.center);
        inside.y = home.y;
        var requested = new List<string>(); System.Action<DialogueRequest> record = r => requested.Add(r.id);
        GameDialogue.OnRequested += record;
        InputSystem.RegisterLayout("{\"name\":\"ArrivalPPEController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        controller = InputSystem.AddDevice("ArrivalPPEController"); InputSystem.SetDeviceUsage(controller, CommonUsages.RightHand);
        try
        {
            yield return null; yield return null;
            Check("Fresh session requires briefing and gloves", SceneTurnover.IsPending && !station.GlovesEquipped && !cordon.CanEnter);
            Check("Station is outside the controlled boundary", !cordon.RequiresBriefingAt(station.transform.position + Vector3.up * 1.6f));
            Check("Station uses the authored clean glove meshes", station.transform.Find("Clean glove pair").GetComponentsInChildren<MeshRenderer>(true).Length == 8);
            Check("Gloves cannot be picked up before briefing", !station.Interact() && !station.PairPickedUp);
            Preview(cam, station);
            int returns = cordon.EntryReturns;
            rig.transform.position += inside - cam.transform.position;
            yield return null; yield return null;
            Check("Pre-briefing crossing returns the head outside", cordon.EntryReturns > returns && !cordon.RequiresBriefingAt(cam.transform.position));
            Check("Pre-briefing crossing requests the briefing instruction", requested.Contains("fr-early-entry-warning"));
            var locomotor = FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
            var capsule = FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
            locomotor.HandleLocomotionEvent(new Oculus.Interaction.Locomotion.LocomotionEvent(-30001,
                new Pose(new Vector3(inside.x, 1.2f, inside.z), Quaternion.identity),
                Oculus.Interaction.Locomotion.LocomotionEvent.TranslationType.Absolute,
                Oculus.Interaction.Locomotion.LocomotionEvent.RotationType.None));
            yield return null; yield return null; yield return null;
            Check("Actual SDK teleport cannot bypass the gate or separate capsule/head", !cordon.RequiresBriefingAt(cam.transform.position)
                && !cordon.RequiresBriefingAt(capsule.Pose.position)
                && Vector2.Distance(new Vector2(cam.transform.position.x, cam.transform.position.z), new Vector2(capsule.Pose.position.x, capsule.Pose.position.z)) < .15f);
            Clear();
            Check("Actual responder interaction starts", SceneTurnover.Controller.Interact());
            Check("PPE explanation is inserted once before Form 1 handover", SceneTurnover.Controller.DialogueLineIds.Count(id => id == "FR_PPE_01") == 1
                && System.Array.IndexOf(SceneTurnover.Controller.DialogueLineIds, "FR_PPE_02") < System.Array.IndexOf(SceneTurnover.Controller.DialogueLineIds, "FR_TURNOVER_04"));
            double deadline = Time.realtimeSinceStartupAsDouble + 40;
            var seen = new HashSet<string>();
            while (SceneTurnover.IsPending && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var panel = STCSNotificationUI.Instance;
                if (panel.IsShowing)
                {
                    foreach (var id in SceneTurnover.Controller.DialogueLineIds) if (panel.CurrentText == GameDialogue.Text(id)) seen.Add(id);
                    if (panel.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f) panel.Skip();
                }
                yield return null;
            }
            Check("All briefing lines including security render before IOC assessment", seen.Count == SceneTurnover.Controller.DialogueLineIds.Length && SceneTurnover.Phase == ScenarioPhase.IOCAssessment);
            Check("Both PPE teaching lines are recorded in history", new[] { "FR_PPE_01", "FR_PPE_02" }.All(id => DialogueHistoryManager.Instance.Entries.Any(e => e.text == GameDialogue.Text(id))));
            Check("PPE teaches contamination, glove changes and limits", GameDialogue.Text("FR_PPE_01").Contains("not complete PPE")
                && GameDialogue.Text("FR_PPE_02").Contains("Change gloves between unrelated evidence"));
            Check("Briefing alone cannot unlock entry or observations", !cordon.CanEnter && !SceneTurnover.RecordIOCObservation("ioc-victim"));
            var assessmentGuide = FindAnyObjectByType<IOCAssessmentInteraction>().GetComponent<IOCAssessmentGuide>();
            Clear(); assessmentGuide.Refresh();
            Check("Interior direction guide waits for gloves", !assessmentGuide.IsVisible);
            Check("Next instruction points to gloves, not into the kitchen", requested.Contains("ppe-arrival-next") && !requested.Contains("ioc-assessment-guidance"));
            Clear(); returns = cordon.EntryReturns;
            rig.transform.position += inside - cam.transform.position;
            yield return null; yield return null;
            Check("Post-briefing bare-handed crossing also returns outside", cordon.EntryReturns > returns && !cordon.RequiresBriefingAt(cam.transform.position) && requested.Contains("ppe-entry-required"));
            home=station.transform.position-station.transform.forward*.85f+Vector3.up*(cam.transform.position.y-station.transform.position.y);
            rig.transform.position+=home-cam.transform.position;
            yield return null;yield return null;
            Check("Student reaches the glove table's front-yard service position",station.IsInRange);
            Clear();
            var token = new object(); PlayerUIGate.Enter(token);
            Check("Other modal UI prevents pickup", !station.Interact()); PlayerUIGate.Exit(token);
            // Release before pressing: held-button protection must not confirm two stages at once.
            yield return Press("primaryButton", false); yield return Press("primaryButton", true);
            Check("Actual A picks up the pair without equipping it", station.PairPickedUp && !station.GlovesEquipped && PlayerUIGate.IsBlocked);
            Check("Picked-up gloves follow the right hand", rig.rightHandAnchor.Find("Clean gloves awaiting wear") != null);
            yield return null; yield return null;
            Check("Holding A never auto-confirms wearing", station.PairPickedUp && !station.GlovesEquipped);
            yield return Press("primaryButton", false); yield return Press("secondaryButton", true);
            Check("Actual B returns the pair and releases the UI gate", !station.PairPickedUp && !station.GlovesEquipped && !PlayerUIGate.IsBlocked && station.transform.Find("Clean glove pair").gameObject.activeSelf);
            yield return Press("secondaryButton", false);
            // Revalidate range after picking up, without accepting a remote confirmation.
            Check("Pair can be picked up again", station.Interact());
            rig.transform.position += Vector3.right * 4;
            yield return null; yield return null;
            Check("Leaving the station cancels pickup", !station.PairPickedUp && !station.GlovesEquipped && !station.Interact());
            rig.transform.position += home - cam.transform.position;
            yield return null; yield return null;
            var heldBefore = HeldItemCoordinator.Instance.CurrentCategory;
            var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == "l_handMeshNode" || r.name == "r_handMeshNode" || r.name == "LeftHand" || r.name == "RightHand").ToDictionary(r => r, r => r.sharedMaterials);
            yield return Press("primaryButton", true); yield return Press("primaryButton", false);
            int beforeEvents = Count(SessionEventType.PPEEquipped);
            yield return Press("primaryButton", true); yield return Press("primaryButton", false);
            Check("Second deliberate A equips the gloves and frees the gate", station.GlovesEquipped && !station.PairPickedUp && !PlayerUIGate.IsBlocked);
            Check("Equipping records exactly one non-scored PPE event", Count(SessionEventType.PPEEquipped) == beforeEvents + 1);
            Check("Both animated hand meshes receive glove materials", skin.Any(p => p.Key.name == "l_handMeshNode") && skin.Any(p => p.Key.name == "r_handMeshNode")
                && skin.All(p => p.Key.sharedMaterials.All(m => m != null && m.name == "Session clean glove")));
            Check("Gloves occupy no tool or evidence slot", HeldItemCoordinator.Instance.CurrentCategory == heldBefore && PlayerToolRegistry.CurrentRole == ToolType.None);
            HeldItemCoordinator.Instance.HolsterCurrentTool();
            Check("Holstering preserves gloves and worn-hand materials", station.GlovesEquipped && skin.All(p => p.Key.sharedMaterials.All(m => m.name == "Session clean glove")));
            Check("Repeated equip cannot log twice", !station.Interact() && Count(SessionEventType.PPEEquipped) == beforeEvents + 1);
            Check("Gloves still require check-in, with cordon closed and visible", !cordon.CanEnter && cordon.CordonVisible && !cordon.EntranceIsOpen);
            Clear();
            var registerApproach=cordon.AccessRegister.UI.transform.position-cordon.AccessRegister.UI.transform.forward*.7f;
            registerApproach.y=cam.transform.position.y;rig.transform.position+=registerApproach-cam.transform.position;
            yield return null;yield return null;
            Check("Register opens after gloves", cordon.AccessRegister.UI.Open());
            yield return new WaitForSecondsRealtime(.3f);
            Check("Confirmed check-in unlocks entry", cordon.AccessRegister.UI.ConfirmCheckIn() && cordon.CanEnter);
            Clear(); returns = cordon.EntryReturns; rig.transform.position += inside - cam.transform.position;
            yield return null; yield return null;
            Check("Equipped student can cross without forced return", cordon.EntryReturns == returns && cordon.RequiresBriefingAt(cam.transform.position));
            Check("Equipped student can record the existing IOC observation", SceneTurnover.RecordIOCObservation("ioc-victim"));
            Check("No forensic processing or discovery was seeded", ToolInputLock.IsLocked && ScenePhotographyManager.Instance.CurrentPoint == null
                && EvidenceStateManager.Instance.AllRecords.All(r => r.status == EvidenceStatus.NotFound));
            Check("Survey guidance follows glove confirmation", requested.IndexOf("ioc-assessment-guidance") > requested.IndexOf("ppe-gloves-equipped"));
            Clear();
            // Continue through existing phase APIs to exercise an actual tool, without seeding evidence.
            foreach (var observation in FindAnyObjectByType<IOCAssessmentInteraction>().Definition.observations)
                SceneTurnover.RecordIOCObservation(observation.observationId);
            SceneTurnover.CompleteIOCAssessment(); SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested);
            SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover); SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation);
            Clear();
            Check("Actual Recorder equip preserves gloves", HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Recorder, rig.rightHandAnchor)
                && PlayerToolRegistry.CurrentRole == ToolType.Recorder && station.GlovesEquipped
                && skin.All(p => p.Key.sharedMaterials.All(m => m.name == "Session clean glove")));
            HeldItemCoordinator.Instance.HolsterCurrentTool();
            Check("Actual Recorder holster leaves gloves on both hands", PlayerToolRegistry.CurrentRole == ToolType.None && station.GlovesEquipped
                && skin.All(p => p.Key.sharedMaterials.All(m => m.name == "Session clean glove")));
        }
        finally
        {
            GameDialogue.OnRequested -= record;
            if (controller != null) InputSystem.RemoveDevice(controller);
            fixtureLocomotor.InjectPlayerEyes(original.transform);
            original.tag = oldTag; rig.enabled = rigEnabled; Destroy(cameraObject);
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        yield return null; yield return null;
        var fresh = FindAnyObjectByType<SceneCordon>();
        Check("Restart restores briefing, glove pair and locked entry", SceneTurnover.IsPending && fresh.PPEStation != null && !fresh.PPEStation.GlovesEquipped && !fresh.CanEnter);
        Check("Restart leaves one station and no carried pair", FindObjectsByType<ArrivalPPEStation>(FindObjectsSortMode.None).Length == 1
            && FindObjectsByType<Transform>(FindObjectsSortMode.None).All(t => t.name != "Clean gloves awaiting wear"));
        Finish();
    }
    void Finish() { report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ==="); Report = report.ToString(); IsDone = true; }
    void Preview(Camera cam, ArrivalPPEStation station)
    {
        var pair = station.transform.Find("Clean glove pair").gameObject;
        var target = RenderTexture.GetTemporary(1120, 800, 24);
        var oldTarget = cam.targetTexture; var oldActive = RenderTexture.active; var oldRotation = cam.transform.rotation;
        var texture = new Texture2D(1120, 800, TextureFormat.RGB24, false);
        try
        {
            cam.transform.rotation = Quaternion.LookRotation(station.transform.position + Vector3.up * 1.02f - cam.transform.position);
            cam.targetTexture = target; cam.fieldOfView = 60;
            Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1120, 800), 0, 0); texture.Apply(); var shown = texture.GetPixels32();
            System.IO.File.WriteAllBytes("docs/evidence/arrival-ppe/station-preview.png", texture.EncodeToPNG());
            pair.SetActive(false); cam.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1120, 800), 0, 0); texture.Apply(); var hidden = texture.GetPixels32();
            int changed = 0;
            for (int i = 0; i < shown.Length; i++) if (Mathf.Abs(shown[i].r - hidden[i].r) + Mathf.Abs(shown[i].g - hidden[i].g) + Mathf.Abs(shown[i].b - hidden[i].b) > 40) changed++;
            Check("Actual Direct3D camera renders the glove pair [changed pixels=" + changed + "]", changed > 100);
        }
        finally
        {
            pair.SetActive(true); cam.targetTexture = oldTarget; RenderTexture.active = oldActive; cam.transform.rotation = oldRotation;
            RenderTexture.ReleaseTemporary(target); Destroy(texture);
        }
    }
}
