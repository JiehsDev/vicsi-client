// Assets/_Project/Scripts/Testing/EvidenceTentPickupTest.cs
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Per-placement pickup policy: unused tents return immediately, successful markers
/// stay until recording, and a locked nearby marker cannot hide an unused tent.
/// Places tents with EvidenceTentTool's real placement (aimed like
/// ChalkProcedureTest) and presses B via EvidenceTentPickup.TryReclaim.
///
/// Run from the editor via:
///     FindFirstObjectByType&lt;EvidenceTentPickupTest&gt;().RunAndLog()
/// </summary>
public class EvidenceTentPickupTest : MonoBehaviour
{
    private const string Bottle = "EVD-018";

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private StringBuilder report;
    private int failures;
    private EvidenceTentTool tents;
    private OVRCameraRig rig;

    public void RunAndLog() => StartCoroutine(Run());

    private void Check(string label, bool ok)
    {
        report.AppendLine($"  {(ok ? "PASS" : "FAIL")}: {label}");
        if (!ok) failures++;
    }

    private IEnumerator Run()
    {
        IsDone = false;
        failures = 0;
        report = new StringBuilder("=== EvidenceTentPickupTest ===\n");
        RelationshipReviewTestUtil.BeginCaseFixture();
        yield return null;

        var esm = EvidenceStateManager.Instance;
        tents = FindFirstObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        rig = FindFirstObjectByType<OVRCameraRig>();
        if (esm == null || tents == null || rig == null)
        {
            Finish("FAIL: EvidenceStateManager, EvidenceTentTool or camera rig missing.");
            yield break;
        }
        if (SceneTurnover.IsPending) SceneTurnover.Controller.CompleteWithoutDialogue();
        Check("scene photography route completed (markers need it)", ScenePhotographyTestUtil.CompleteRoute());
        if (PlayerToolRegistry.VirtuallyEquippedRole != ToolType.EvidenceMarker)
        {
            HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.EvidenceMarker, rig.rightHandAnchor);
        }
        yield return null;

        // A bare-floor spot in the yard, well over 1.5 m from every evidence item.
        Vector3 floor = new Vector3(-0.5f, 0f, 3.0f);
        Check("test floor spot is outside every evidence item's tent radius", EvidenceProp.FindNearestWithinRadius(floor) == null);

        // --- 1. Tent on non-evidence comes back and frees its number.
        Check("spare pool starts at 4 and reserves roster markers", tents.SelectPlacementIndex(null,null) == 3);
        var junk = PlaceAt(floor);
        Check("tent placed on bare floor (pickup attached, not on evidence)", junk != null && string.IsNullOrEmpty(junk.MarkedEvidenceId));
        Check($"spare 4 now in use [next={tents.NextTentNumber}]", tents.NextTentNumber == 5);

        // --- 2. Only the marker that actually applied the transition waits for recording.
        var record = esm.GetRecord(Bottle);
        esm.MarkFound(Bottle, ToolType.None);
        esm.MarkPhotographedRaw(Bottle, ToolType.Photographer);
        if (record.definition.requiresChalk) esm.MarkChalkMarked(Bottle, ToolType.ChalkMark);
        var onEvidence = PlaceAt(EvidenceProp.FindById(Bottle).transform.position);
        Check($"tent placed on the bottle marks it [{record.status}]", onEvidence != null && onEvidence.MarkedEvidenceId == Bottle && record.status == EvidenceStatus.Marked);
        Check("Unused tent is pickable immediately; successful marker stays until recording",
            junk.IsPickable() && !onEvidence.IsPickable());
        var floorPosition = junk.transform.position;
        junk.transform.position = onEvidence.transform.position + Vector3.right * .15f;
        Check("Nearest locked marker does not block a nearby unused marker",
            EvidenceTentPickupCoordinator.FindNearestInRange(onEvidence.transform.position) == junk);
        var camera = Camera.main.transform; var cameraPosition = camera.position;
        camera.position = onEvidence.transform.position + Vector3.up * 1.6f;
        typeof(EvidenceTentPickup).GetMethod("Update", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(onEvidence,null);
        typeof(EvidenceTentPickup).GetMethod("Update", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(junk,null);
        Check("Only unused marker shows the actual pickup hint before recording",
            !PromptShown(onEvidence) && PromptShown(junk));
        var gate = new object(); PlayerUIGate.Enter(gate);
        typeof(EvidenceTentPickup).GetMethod("Update", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(junk,null);
        Check("Pickup hint defers to UI ownership", !PromptShown(junk));
        PlayerUIGate.Exit(gate);
        camera.position = cameraPosition; junk.transform.position = floorPosition;

        junk.TryReclaim();
        yield return null;
        Check($"floor tent picked up: gone, spare 4 free again [next={tents.NextTentNumber}]", junk == null && tents.SelectPlacementIndex(null,null) == 3);

        // --- 3. Direct pickup cannot bypass the recording gate or revert the mark.
        int beforeBlocked = Count(SessionEventType.MarkerReclaimBlocked);
        onEvidence.TryReclaim();
        yield return null;
        Check("Successful marker cannot be reclaimed at Marked and its record remains intact",
            onEvidence != null && record.status == EvidenceStatus.Marked && record.tentNumber == 3
            && Count(SessionEventType.MarkerReclaimBlocked) == beforeBlocked + 1);

        // --- 4. Documented with the tent in frame: refused, tent stays.
        var documented = onEvidence;
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        esm.MarkPhotographedFinal(Bottle, ToolType.Photographer);
        Check($"bottle re-marked and final-photographed [{record.status}]", record.status == EvidenceStatus.PhotographedFinal);
        int blockedBefore = Count(SessionEventType.MarkerReclaimBlocked);
        documented.TryReclaim();
        yield return null;
        Check("documented tent refused: stays in place, status unchanged, MarkerReclaimBlocked logged",
            documented != null && !documented.IsPickable() && record.status == EvidenceStatus.PhotographedFinal && Count(SessionEventType.MarkerReclaimBlocked) == blockedBefore + 1);

        // --- 5. No soft-lock: use every remaining number on junk, then recover one.
        int max = tents.MaxPlacedTents;
        int placedJunk = 0;
        EvidenceTentPickup lastJunk = null;
        for (int i = 0; i < max; i++)
        {
            var t = PlaceAt(floor + new Vector3(0.35f * i, 0f, 0f));
            if (t != null) { placedJunk++; lastJunk = t; }
        }
        Check($"all spare numbers in use, reserved item markers remain [junk placed={placedJunk}, next={tents.NextTentNumber}]",
            placedJunk == max - 3 && tents.NextTentNumber <= 0 && tents.SelectPlacementIndex(EvidenceProp.FindById("EVD-014"),null) == 0);
        if (lastJunk != null) lastJunk.TryReclaim();
        yield return null;
        Check($"picking one back up makes a number available again (no soft-lock) [next={tents.NextTentNumber}]", tents.NextTentNumber >= 1);

        MeasurementTestUtil.CompleteSketchFor(Bottle);
        Check("Required on-scene fingerprint processing completes before recording", esm.MarkFingerprinted(Bottle,ToolType.FingerprintKit)==TransitionResult.Applied);
        Check("Fingerprint processing alone does not unlock the marker", !documented.IsPickable());
        esm.RecordReportLinesSelected(Bottle, new[] { "bottle-location", "bottle-first-encountered", "bottle-standard-procedure" });
        esm.MarkLogged(Bottle, ToolType.Recorder);
        Check("Successful marker becomes pickable after recording without collection", documented.IsPickable()
            && esm.IsAtOrPastStatus(Bottle, EvidenceStatus.Logged) && !esm.IsAtOrPastStatus(Bottle,EvidenceStatus.Collected));
        var recordedStatus = record.status;
        camera.position = documented.transform.position + Vector3.up * 1.6f;
        typeof(EvidenceTentPickup).GetMethod("Update", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(documented,null);
        Check("Recorded evidence marker now shows its pickup hint", PromptShown(documented));
        camera.position = cameraPosition;
        int reclaims = Count(SessionEventType.MarkerReclaimed);
        documented.TryReclaim(); documented.TryReclaim();
        yield return null;
        Check("Cleanup preserves recorded status and marker number and logs once", documented == null && record.status == recordedStatus
            && record.tentNumber == 3 && Count(SessionEventType.MarkerReclaimed) == reclaims + 1);

        Finish(failures == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures} failure(s) ===");
    }

    // Aims the dispenser's placementOrigin straight down at point and places a tent with the
    // real placement code. Returns the new tent's pickup (null if none was placed).
    private EvidenceTentPickup PlaceAt(Vector3 point)
    {
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        var origin = typeof(EvidenceTentTool).GetField("placementOrigin", bf).GetValue(tents) as Transform;
        var t = tents.transform;
        t.rotation = Quaternion.FromToRotation(origin.forward, Vector3.down) * t.rotation;
        t.position += point + Vector3.up * 0.6f - origin.position;
        var before = new System.Collections.Generic.HashSet<EvidenceTentPickup>(FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None));
        typeof(EvidenceTentTool).GetMethod("PlaceTent", bf).Invoke(tents, null);
        foreach (var p in FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None))
        {
            if (!before.Contains(p)) return p;
        }
        return null;
    }

    private static int Count(SessionEventType type) =>
        SessionLogger.Instance != null && SessionLogger.Instance.EventCounts.TryGetValue(type, out int n) ? n : 0;

    private static bool PromptShown(EvidenceTentPickup tent) => (bool)typeof(EvidenceTentPickup)
        .GetField("promptShown", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(tent);

    private void Finish(string last)
    {
        report.AppendLine(last);
        Report = report.ToString();
        Debug.Log(Report);
        IsDone = true;
    }
}
