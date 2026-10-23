// Assets/_Project/Scripts/Testing/MeasurementToolTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode check of the VR MeasurementTool. Equips it through the tool wheel's own path
/// (HeldItemCoordinator.ToggleEquipTool), places the tape tip where a hand would hold it,
/// and presses the real trigger handler (MeasurementTool.HandleTrigger) - no headset, no
/// shortcut into MeasurementStateManager. Run once per fresh Play session: RunAllTests(),
/// then poll IsDone / Report.
/// </summary>
public class MeasurementToolTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private MeasurementTool tool;
    private MeasurementStateManager manager;

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var failures = new List<string>();
        var report = new StringBuilder("=== MeasurementToolTest ===\n");
        var esm = EvidenceStateManager.Instance;
        manager = MeasurementStateManager.Instance;
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (esm == null || manager == null || rig == null || HeldItemCoordinator.Instance == null)
        {
            Report = "FAIL: required managers/rig missing.";
            IsDone = true;
            yield break;
        }
        yield return null;

        // --- Tool wheel ---
        var wheel = FindFirstObjectByType<ToolWheelController>(FindObjectsInactive.Include);
        var hidden = typeof(ToolWheelController).GetField("hiddenRoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(wheel) as List<ToolType>;
        Check(failures, report, "[wheel] Measurement tool registered and not hidden on the wheel",
            PlayerToolRegistry.GetTool(ToolType.Measurement) != null && hidden != null && !hidden.Contains(ToolType.Measurement));
        Check(failures, report, "[wheel] ToolType.Measurement appended as 13", (int)ToolType.Measurement == 13 && (int)ToolType.LetterTentMarker == 12);

        HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Measurement, rig.rightHandAnchor);
        tool = MeasurementTool.Instance;
        Check(failures, report, "[equip] equipped from the wheel path", tool != null && tool.IsVirtuallyEquipped && PlayerToolRegistry.VirtuallyEquippedRole == ToolType.Measurement);
        yield return null;

        // --- An undiscovered item is not a target (no evidence detection) ---
        AimAt("EVD-015");
        tool.HandleTrigger();
        Check(failures, report, "[leak] undiscovered spatter is not selectable", tool.CurrentPhase == MeasurementTool.Phase.SelectTarget && tool.LastMessage == "Select evidence to measure.");
        PlaceTip(new Vector3(0f, 1.5f, 12.5f), new Vector3(0f, 3f, 12.5f));
        tool.HandleTrigger();
        Check(failures, report, "[select] aiming at nothing -> 'Select evidence to measure.'", tool.LastMessage == "Select evidence to measure.");

        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            esm.MarkFound(id, ToolType.None);
        }
        // HYP-01 opens at 3 items Found and holds the UI gate (which would block the
        // wheel's equip toggle) - answer it the way GreyboxFlowTest does.
        for (int i = 0; i < 5; i++)
        {
            var checkpoint = FindFirstObjectByType<HypothesisCheckpointUI>();
            if (checkpoint != null && checkpoint.IsShowing)
            {
                checkpoint.AnswerForTest();
            }
            yield return null;
        }

        // Measurement follows the marker: a discovered but unmarked item is refused.
        AimAt("EVD-014");
        tool.HandleTrigger();
        Check(failures, report, "[order] unmarked item: 'Place an evidence marker before measuring.'",
            tool.CurrentPhase == MeasurementTool.Phase.SelectTarget && tool.LastMessage == "Place an evidence marker before measuring.");

        // Overall scene photography, then initial photo / chalk (knife only) / marker -
        // through the public lifecycle API, as the other setup steps here.
        Check(failures, report, "[setup] overall scene photography completed", ScenePhotographyTestUtil.CompleteRoute());
        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            esm.MarkPhotographedRaw(id, ToolType.Photographer);
        }
        esm.MarkChalkMarked("EVD-014", ToolType.ChalkMark);
        esm.MarkChalkMarked("EVD-018", ToolType.ChalkMark); // the bottle's position is outlined before its tent too
        esm.MarkChalkMarked("BODY-A", ToolType.ChalkMark);  // as are the body's and the stain's
        esm.MarkChalkMarked("EVD-015", ToolType.ChalkMark);
        Check(failures, report, "[setup] markers placed",
            esm.MarkTentedWithLetter("BODY-A", ToolType.LetterTentMarker, "A") == TransitionResult.Applied
            && esm.MarkTented("EVD-014", ToolType.EvidenceMarker, 1) == TransitionResult.Applied
            && esm.MarkTented("EVD-015", ToolType.EvidenceMarker, 2) == TransitionResult.Applied
            && esm.MarkTented("EVD-018", ToolType.EvidenceMarker, 3) == TransitionResult.Applied);
        yield return null;

        // Each item has its OWN two fixed references (per-evidence rectangular measurement).
        string knife1 = MeasurementTestUtil.Ref("EVD-014", 1), knife2 = MeasurementTestUtil.Ref("EVD-014", 2);
        string body1 = MeasurementTestUtil.Ref("BODY-A", 1), body2 = MeasurementTestUtil.Ref("BODY-A", 2);
        string bottle1 = MeasurementTestUtil.Ref("EVD-018", 1), bottle2 = MeasurementTestUtil.Ref("EVD-018", 2);
        string spatter1 = MeasurementTestUtil.Ref("EVD-015", 1), spatter2 = MeasurementTestUtil.Ref("EVD-015", 2);

        // --- Knife: reference 1, partial, reference 2, complete ---
        AimAt("EVD-014");
        tool.HandleTrigger();
        Check(failures, report, $"[knife] selected; asks for its first fixed reference ('{tool.LastMessage}')",
            tool.SelectedRequirementId == "MEAS-EVD-014" && tool.CurrentPhase == MeasurementTool.Phase.AtReference && tool.CurrentReferenceId == knife1
            && tool.LastMessage == $"Measure from the {Name(knife1)}.");
        yield return null;
        var refHighlight = Guide("referenceHighlight");
        Check(failures, report, "[guides] only the required reference is highlighted, at its anchor",
            refHighlight.activeSelf && Flat(refHighlight.transform.position, SceneReferencePoint.Find(knife1).AnchorPosition) < 0.001f && Guide("targetHighlight").activeSelf);

        HookAt(knife1);
        Check(failures, report, "[knife] tape hooked inside the reference zone", tool.CurrentPhase == MeasurementTool.Phase.Extending);
        var line = tool.GetComponentInChildren<LineRenderer>(true);
        Vector3 fixedHook = SceneReferencePoint.Find(knife1).AnchorPosition;
        Check(failures, report, "[hook] tape snaps to the authored reference point, not the hand contact",
            line.enabled && Vector3.Distance(line.GetPosition(0), fixedHook) < .0005f);
        foreach(var offset in new[]{new Vector3(.25f,0,.2f),new Vector3(-.15f,.45f,-.3f),new Vector3(.1f,-.35f,.15f)})
        {
            PlaceAtTarget("EVD-014",offset);
            typeof(MeasurementTool).GetMethod("RefreshVisuals",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(tool,null);
            Check(failures,report,"[hook] moving the hand horizontally/vertically leaves the reference end fixed: "+offset,
                Vector3.Distance(line.GetPosition(0),fixedHook)<.0005f&&Vector3.Distance(line.GetPosition(1),tool.TapeTip.position)<.0005f);
        }
        PlaceAtTarget("EVD-014", new Vector3(0.03f, 0f, 0f));
        yield return null;
        // Compare against the tape line's own end points from the same frame - without a
        // headset the rig drifts between frames, so the tip a frame later is not the tip
        // the tool drew. The hook is fixed; the reading retains the authored plane/point method.
        var readout = tool.GetComponentInChildren<TMPro.TMP_Text>(true);
        float expectedReading = MeasurementGeometry.DistanceToReference(SceneReferencePoint.Find(knife1),line.GetPosition(1),true);
        Check(failures, report, $"[live] live distance {tool.LiveDistance:0.000} m retains the authored geometry, shown as '{readout.text}'",
            line.enabled && Mathf.Abs(tool.LiveDistance - expectedReading) < 0.0005f
            && Vector3.Distance(line.GetPosition(0),fixedHook) < 0.0005f
            && readout.text == tool.LiveDistance.ToString("0.00") + " m");
        PlaceAtTarget("EVD-014", new Vector3(0.03f, 0f, 0f));
        float expectedLive = MeasurementGeometry.DistanceToReference(SceneReferencePoint.Find(knife1), tool.TapeTip.position, true);
        tool.HandleTrigger();
        var knife = manager.GetRecord("MEAS-EVD-014");
        Check(failures, report, $"[knife] reference 1 recorded at the tape end, not snapped (measured {knife.measuredA:0.0000} vs expected {knife.requirement.expectedDistanceA:0.000})",
            knife.hasA && knife.withinToleranceA && Mathf.Abs(knife.measuredA - expectedLive) < 0.0005f && knife.measuredA != knife.requirement.expectedDistanceA);
        Check(failures, report, $"[knife] partial: reference 1 kept, reference 2 next ('{tool.LastMessage}')",
            knife.progress == MeasurementProgress.Partial && tool.CurrentReferenceId == knife2 && tool.CurrentPhase == MeasurementTool.Phase.AtReference
            && tool.LastMessage.StartsWith($"{Name(knife1)} recorded: ") && tool.LastMessage.EndsWith($"Measure from the {Name(knife2)}."));
        HookAt(knife2);
        PlaceAtTarget("EVD-014", Vector3.zero);
        tool.HandleTrigger();
        Check(failures, report, "[knife] reference 2 completes the requirement", manager.IsMeasurementComplete("MEAS-EVD-014") && tool.LastMessage.EndsWith("Measurement complete.") && tool.CurrentPhase == MeasurementTool.Phase.SelectTarget);
        Check(failures, report, "[knife] measured before disturbance", knife.TakenBeforeDisturbance);
        AimAt("EVD-014");
        tool.HandleTrigger();
        // Fixed references done: with an evidence distance still open the tape moves on to it
        // (Evidence Distance mode); with none, the item is simply complete.
        if (manager.AreAvailablePairMeasurementsComplete("EVD-014"))
        {
            Check(failures, report, "[knife] reselecting a complete item -> 'Measurement complete.'", tool.LastMessage == "Measurement complete." && tool.CurrentPhase == MeasurementTool.Phase.SelectTarget);
        }
        else
        {
            Check(failures, report, $"[knife] reselecting it with an evidence distance open -> Evidence Distance mode ('{tool.LastMessage}')",
                tool.CurrentMode == MeasurementTool.Mode.EvidenceDistance && tool.LastMessage == $"Evidence distance: hold the tape at the {EvidenceStateManager.Instance.GetDisplayName("EVD-014")}.");
        }

        // --- Body: out of tolerance, retry, complete; nothing collected ---
        MeasurementTestUtil.PrepareBodyForMeasurement();
        AimAt("BODY-A");
        tool.HandleTrigger();
        HookAt(body1);
        MeasurementGeometry.TryGetEvidenceAnchor("BODY-A", out var bodyAnchor);
        Vector3 away = Flat3(bodyAnchor - MeasurementGeometry.ReferenceFoot(SceneReferencePoint.Find(body1), bodyAnchor)).normalized;
        PlaceAtTarget("BODY-A", away * 0.3f);
        tool.HandleTrigger();
        var body = manager.GetRecord("MEAS-BODY-A");
        Check(failures, report, $"[tolerance] +0.30 m on reference 1 recorded but invalid (error {body.errorA:0.000})", body.hasA && !body.withinToleranceA && body.progress == MeasurementProgress.Partial);
        Check(failures, report, "[tolerance] side stays required, player told to retry",
            tool.CurrentReferenceId == body1 && tool.CurrentPhase == MeasurementTool.Phase.AtReference && tool.LastMessage == "Measurement is outside the acceptable range. Try again.");
        HookAt(body1);
        PlaceAtTarget("BODY-A", Vector3.zero);
        tool.HandleTrigger();
        HookAt(body2);
        PlaceAtTarget("BODY-A", new Vector3(0f, 0f, -0.04f));
        tool.HandleTrigger();
        Check(failures, report, "[body] completes after the corrected reference 1", manager.IsMeasurementComplete("MEAS-BODY-A"));
        Check(failures, report, "[body] measurements follow final photo without collection (scene custody)",
            esm.GetRecord("BODY-A").status == EvidenceStatus.PhotographedFinal && esm.GetRecord("BODY-A").custodyLocation == EvidenceCustodyLocation.Scene);

        // --- Bottle: another item's reference refused, too far, either order, then complete ---
        esm.MarkPhotographedFinal("EVD-018",ToolType.Photographer);
        AimAt("EVD-018");
        tool.HandleTrigger();
        HookAt(spatter1);
        Check(failures, report, $"[wrong ref] hooking another item's reference is refused ('{tool.LastMessage}'), nothing recorded",
            tool.CurrentPhase == MeasurementTool.Phase.AtReference && tool.LastMessage == "Use a fixed reference assigned to this evidence."
            && manager.GetRecord("MEAS-EVD-018").progress == MeasurementProgress.NotStarted);
        PlaceTip(new Vector3(0f, 1.3f, 12.5f), new Vector3(0f, 3f, 12.5f));
        tool.HandleTrigger();
        Check(failures, report, $"[far] away from every reference -> told which reference to use ('{tool.LastMessage}')",
            tool.LastMessage == $"Hold the tape at the {Name(bottle1)}." && tool.CurrentPhase == MeasurementTool.Phase.AtReference);
        HookAt(bottle2);
        Check(failures, report, "[either order] the item's own second reference is accepted first",
            tool.CurrentPhase == MeasurementTool.Phase.Extending && tool.CurrentReferenceId == bottle2);
        PlaceTip(new Vector3(0f, 1.3f, 12.5f), new Vector3(0f, 1.3f, 14f));
        tool.HandleTrigger();
        Check(failures, report, "[far] tape end away from the item -> not recorded", tool.LastMessage == "Bring the tape end to the evidence." && tool.CurrentPhase == MeasurementTool.Phase.Extending && !manager.GetRecord("MEAS-EVD-018").hasB);
        PlaceAtTarget("EVD-018", Vector3.zero);
        tool.HandleTrigger();
        Check(failures, report, "[either order] reference 2 recorded first; reference 1 now required",
            manager.GetRecord("MEAS-EVD-018").hasB && !manager.GetRecord("MEAS-EVD-018").hasA && tool.CurrentReferenceId == bottle1);
        HookAt(bottle1);
        PlaceAtTarget("EVD-018", new Vector3(0.02f, 0f, 0.02f));
        tool.HandleTrigger();
        Check(failures, report, "[bottle] pair completes", manager.IsMeasurementComplete("MEAS-EVD-018"));

        // --- Spatter: reference 1, unequip / re-equip resumes at reference 2, then disturbance, then late ---
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-015", out var spatterAnchorBefore);
        AimAt("EVD-015");
        tool.HandleTrigger();
        HookAt(spatter1);
        PlaceAtTarget("EVD-015", Vector3.zero);
        tool.HandleTrigger();
        Check(failures, report, "[spatter] reference 1 recorded", manager.GetRecord("MEAS-EVD-015").hasA && tool.CurrentReferenceId == spatter2);

        HookAt(spatter2); // tape out, then put the tool away mid-measurement
        HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Measurement, rig.rightHandAnchor);
        yield return null;
        Check(failures, report, "[unequip] holstered: guides and tape hidden, reference 1 still recorded",
            !tool.IsVirtuallyEquipped && !Guide("referenceHighlight").activeSelf && !Guide("targetHighlight").activeSelf && !line.enabled && manager.GetRecord("MEAS-EVD-015").hasA);
        HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Measurement, rig.rightHandAnchor);
        yield return null;
        Check(failures, report, $"[re-equip] resumes the same item at reference 2 (tape not left extended) ('{tool.LastMessage}')",
            tool.IsVirtuallyEquipped && tool.SelectedRequirementId == "MEAS-EVD-015" && tool.CurrentReferenceId == spatter2 && tool.CurrentPhase == MeasurementTool.Phase.AtReference
            && tool.LastMessage == $"Measure from the {Name(spatter2)}." && Guide("referenceHighlight").activeSelf);

        // Disturb the spatter: sampling it into the swab container is its collection.
        // (State setup through the public lifecycle API - the swab gesture itself is
        // covered by GreyboxFlowTest; the disturbance rule reads only this state.)
        esm.MarkPhotographedFinal("EVD-015", ToolType.None);
        // Sampling the stain is what disturbs it now (its swab is taken on scene, before
        // the record) - the stain itself stays in the scene until its sealed sample is
        // admitted to the inventory after logging.
        // Isolate late-measurement reporting using an already-disturbed fixture.
        // Live sampling is now gated by completed measurement and sketch placement.
        esm.GetRecord("EVD-015").swabbingDone = true;
        Check(failures, report, "[disturbed] already-sampled spatter fixture", esm.GetRecord("EVD-015").swabbingDone);
        Check(failures, report, "[disturbed] sampling counts as disturbance for an item collected by swab",
            MeasurementStateManager.Instance.IsDisturbed(MeasurementStateManager.Instance.RequirementSet.Find("MEAS-EVD-015")));
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-015", out var spatterAnchorAfter);
        Check(failures, report, "[spatter] target anchor stable through sampling", (spatterAnchorAfter - spatterAnchorBefore).sqrMagnitude < 1e-8f);

        HookAt(spatter2);
        PlaceAtTarget("EVD-015", Vector3.zero);
        tool.HandleTrigger();
        var spatter = manager.GetRecord("MEAS-EVD-015");
        Check(failures, report, "[disturbed] late side flagged and the player told",
            spatter.takenAfterDisturbance && tool.LastMessage == "This evidence was moved before its position was measured.");
        Check(failures, report, "[disturbed] Warn requirement: completes, marked late (per requirement enforcement)",
            spatter.IsComplete && !spatter.TakenBeforeDisturbance && spatter.requirement.enforcement == ProcedureEnforcement.Warn);

        // --- Events ---
        SessionLogger.Instance?.Flush();
        string log = ReadSessionLog();
        int started = Count(log, "\"eventType\":\"MeasurementStarted\"");
        int taken = Count(log, "\"eventType\":\"MeasurementTaken\"");
        int completed = Count(log, "\"eventType\":\"MeasurementCompleted\"");
        int outOfTolerance = Count(log, "\"eventType\":\"MeasurementOutOfTolerance\"");
        int late = Count(log, "\"eventType\":\"MeasurementAfterDisturbance\"");
        Check(failures, report, $"[events] Started 4 / Taken 9 / Completed 4 / OutOfTolerance 1 / AfterDisturbance 1 (got {started}/{taken}/{completed}/{outOfTolerance}/{late})",
            started == 4 && taken == 9 && completed == 4 && outOfTolerance == 1 && late == 1);
        Check(failures, report, "[ids] no internal ids in any player message seen", !tool.LastMessage.Contains("REF-") && !tool.LastMessage.Contains("MEAS-") && !tool.LastMessage.Contains("EVD-"));

        HeldItemCoordinator.Instance.HolsterCurrentTool();
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // --- Placement: where a hand would put the tape -------------------------------

    private void PlaceTip(Vector3 tipWorld, Vector3 lookAt)
    {
        var t = tool.transform;
        t.rotation = Quaternion.LookRotation(lookAt - tipWorld);
        t.position += tipWorld - tool.TapeTip.position;
    }

    // Held 1 m above the item's anchor, pointing straight at it.
    private void AimAt(string evidenceId)
    {
        MeasurementGeometry.TryGetEvidenceAnchor(evidenceId, out var anchor);
        PlaceTip(anchor + Vector3.up * 1.0f + Vector3.forward * 0.05f, anchor);
    }

    // Tape held inside the reference's own zone (where a hand would hold it against the wall), then trigger.
    private void HookAt(string referenceId)
    {
        // Exercise forgiving contact away from the exact center of the enlarged receiver.
        var zone = SceneReferencePoint.Find(referenceId).Zone.Box;
        Vector3 tip = zone.transform.TransformPoint(zone.center + new Vector3(.35f,.4f,.15f));
        PlaceTip(tip, tip + Vector3.back);
        tool.HandleTrigger();
    }

    private static string Name(string referenceId) => SceneReferencePoint.Find(referenceId).displayName;

    // Tape end held over the item's anchor (plus an offset on the floor plan).
    private void PlaceAtTarget(string evidenceId, Vector3 offset)
    {
        MeasurementGeometry.TryGetEvidenceAnchor(evidenceId, out var anchor);
        Vector3 tip = new Vector3(anchor.x, 1.0f, anchor.z) + offset;
        PlaceTip(tip, tip + Vector3.down);
    }

    private GameObject Guide(string field)
    {
        return typeof(MeasurementTool).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(tool) as GameObject;
    }

    private static float Flat(Vector3 a, Vector3 b) => MeasurementGeometry.Distance(a, b, true);

    private static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static int Count(string text, string token)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(token, i, System.StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += token.Length;
        }
        return n;
    }

    private static string ReadSessionLog()
    {
        string path = SessionLogger.Instance != null ? SessionLogger.Instance.ActiveLogFilePath : null;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return string.Empty;
        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
        using var reader = new System.IO.StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool passed)
    {
        report.AppendLine($"  {(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed) failures.Add(label);
    }
}
