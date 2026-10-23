// Assets/_Project/Scripts/Testing/EvidenceTraceMarkerTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// The kitchen knife takes TWO numbered tents - one for the knife, one for the blood on its
/// blade (EvidenceDefinition.requiresTraceMarker, EvidenceTraceMarkerTarget) - one evidence
/// record, two tents. Drives the real EvidenceTentTool placement (aimed like
/// ChalkProcedureTest) and EvidenceTentPickup:
///   - the first tent at the blade marks the knife itself;
///   - the knife's final photograph is refused until the blood trace has its own tent;
///   - the second tent marks the trace (logged EvidenceTraceMarked);
///   - a further tent near the knife ties to nothing, so picking it up never reverts the knife;
///   - both successful markers stay until recording, then return without erasing their record.
///
/// Run from the editor via:
///     FindFirstObjectByType&lt;EvidenceTraceMarkerTest&gt;().RunAndLog()
/// </summary>
public class EvidenceTraceMarkerTest : MonoBehaviour
{
    private const string Knife = "EVD-014";

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private StringBuilder report;
    private int failures;
    private EvidenceTentTool tents;

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
        report = new StringBuilder("=== EvidenceTraceMarkerTest ===\n");
        RelationshipReviewTestUtil.BeginCaseFixture();
        yield return null;

        var esm = EvidenceStateManager.Instance;
        var gate = ProceduralGateValidator.Instance;
        tents = FindFirstObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        var rig = FindFirstObjectByType<OVRCameraRig>();
        var trace = FindFirstObjectByType<EvidenceTraceMarkerTarget>();
        if (esm == null || gate == null || tents == null || rig == null || trace == null)
        {
            Finish($"FAIL: missing pieces (esm={esm != null}, gate={gate != null}, tents={tents != null}, rig={rig != null}, trace target={trace != null}).");
            yield break;
        }
        if (SceneTurnover.IsPending) SceneTurnover.Controller.CompleteWithoutDialogue();
        Check("scene photography route completed (markers need it)", ScenePhotographyTestUtil.CompleteRoute());
        if (PlayerToolRegistry.VirtuallyEquippedRole != ToolType.EvidenceMarker)
        {
            HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.EvidenceMarker, rig.rightHandAnchor);
        }
        yield return null;

        var record = esm.GetRecord(Knife);
        Check("knife requires a second tent for its blood trace (data)", record.definition.requiresTraceMarker && trace.evidenceId == Knife);
        Vector3 blade = trace.transform.position;
        esm.MarkFound(Knife, ToolType.None);
        esm.MarkPhotographedRaw(Knife, ToolType.Photographer);
        if (record.definition.requiresChalk) esm.MarkChalkMarked(Knife, ToolType.ChalkMark);

        // 1. First tent - even aimed at the blade - marks the knife itself.
        Check("before the knife is marked, the blade takes no trace tent", !trace.AcceptsTent);
        var knifeTent = PlaceAt(blade);
        Check($"first tent at the blade marks the KNIFE [status={record.status}, tent={record.tentNumber}, trace={record.traceTentNumber}]",
            knifeTent != null && knifeTent.MarkedEvidenceId == Knife && !knifeTent.MarksTrace
            && record.status == EvidenceStatus.Marked && record.tentNumber == 1 && !record.traceTentNumber.HasValue);

        // 2. Final photo refused until the trace has its own tent.
        string reason = gate.GetBlockReason(Knife, EvidenceStatus.PhotographedFinal);
        Check($"knife's final photo refused, naming the blood trace tent ['{reason}']",
            !gate.CanTransition(Knife, EvidenceStatus.PhotographedFinal) && reason == GameDialogue.Text("evidence-trace-marker-first"));
        Check("a direct final-photo call is refused too (state machine backstop)",
            esm.MarkPhotographedFinal(Knife, ToolType.Photographer) == TransitionResult.Violation && record.status == EvidenceStatus.Marked);

        // 3. Second tent on the blade marks the trace.
        int traceEvents = Count(SessionEventType.EvidenceTraceMarked);
        Check("now the blade accepts its trace tent", trace.AcceptsTent);
        var traceTent = PlaceAt(blade);
        Check($"second tent marks the blood trace [trace tent={record.traceTentNumber}, knife still {record.status}]",
            traceTent != null && traceTent.MarksTrace && traceTent.MarkedEvidenceId == Knife
            && record.traceTentNumber == 4 && record.status == EvidenceStatus.Marked);
        Check("EvidenceTraceMarked logged once", Count(SessionEventType.EvidenceTraceMarked) == traceEvents + 1);
        Check("Both successful knife markers wait for recording", !knifeTent.IsPickable() && !traceTent.IsPickable());

        // 4. A further tent beside the knife ties to nothing - picking it up never reverts the knife.
        var extra = PlaceAt(blade + new Vector3(0.1f, 0f, 0.05f));
        Check($"extra tent near the already-marked knife is not tied to it [{(extra != null ? extra.MarkedEvidenceId ?? "none" : "not placed")}]",
            extra != null && string.IsNullOrEmpty(extra.MarkedEvidenceId) && !extra.MarksTrace && extra.IsPickable());
        extra.TryReclaim();
        yield return null;
        Check($"picking up the extra tent leaves the knife's marks intact [{record.status}, trace={record.traceTentNumber}]",
            record.status == EvidenceStatus.Marked && record.tentNumber == 1 && record.traceTentNumber == 4);

        // 5. Trace pickup cannot undo a successful mark before recording.
        traceTent.TryReclaim();
        yield return null;
        Check("Trace tent stays in place before recording and preserves both marks",
            traceTent != null && record.traceTentNumber == 4 && record.status == EvidenceStatus.Marked);

        // 6. Final photo now allowed; afterwards the trace tent is documented and stays.
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        Check("knife's final photo allowed once both tents are down", gate.CanTransition(Knife, EvidenceStatus.PhotographedFinal));
        esm.MarkPhotographedFinal(Knife, ToolType.Photographer);
        Check($"knife final-photographed [{record.status}]", record.status == EvidenceStatus.PhotographedFinal);
        int blocked = Count(SessionEventType.MarkerReclaimBlocked);
        traceTent.TryReclaim();
        yield return null;
        Check("after the final photo the trace tent is documented: refused, stays, trace kept",
            traceTent != null && record.traceTentNumber.HasValue && Count(SessionEventType.MarkerReclaimBlocked) == blocked + 1);

        MeasurementTestUtil.CompleteSketchFor(Knife);
        esm.MarkSwabbed(Knife, ToolType.Swab);
        Check("Sampling alone does not unlock either successful marker", record.swabbingDone && !knifeTent.IsPickable() && !traceTent.IsPickable());
        Check("Required knife fingerprint processing completes before recording", esm.MarkFingerprinted(Knife,ToolType.FingerprintKit)==TransitionResult.Applied);
        Check("Fingerprint processing alone keeps both markers locked", !knifeTent.IsPickable() && !traceTent.IsPickable());
        esm.RecordReportLinesSelected(Knife, new[] { "knife-location", "knife-wound-match" });
        esm.MarkLogged(Knife, ToolType.Recorder);
        Check("Recording unlocks item and trace markers together", knifeTent.IsPickable() && traceTent.IsPickable());
        var recorded = record.status;
        knifeTent.TryReclaim(); traceTent.TryReclaim(); yield return null;
        Check("Both markers return without erasing recorded status, item or trace numbers", knifeTent == null && traceTent == null
            && record.status == recorded && record.tentNumber == 1 && record.traceTentNumber == 4);

        Finish(failures == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures} failure(s) ===");
    }

    private EvidenceTentPickup PlaceAt(Vector3 point)
    {
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        var origin = typeof(EvidenceTentTool).GetField("placementOrigin", bf).GetValue(tents) as Transform;
        var t = tents.transform;
        t.rotation = Quaternion.FromToRotation(origin.forward, Vector3.down) * t.rotation;
        t.position += point + Vector3.up * 0.6f - origin.position;
        var before = new HashSet<EvidenceTentPickup>(FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None));
        typeof(EvidenceTentTool).GetMethod("PlaceTent", bf).Invoke(tents, null);
        foreach (var p in FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None))
        {
            if (!before.Contains(p)) return p;
        }
        return null;
    }

    private static int Count(SessionEventType type) =>
        SessionLogger.Instance != null && SessionLogger.Instance.EventCounts.TryGetValue(type, out int n) ? n : 0;

    private void Finish(string last)
    {
        report.AppendLine(last);
        Report = report.ToString();
        Debug.Log(Report);
        IsDone = true;
    }
}
