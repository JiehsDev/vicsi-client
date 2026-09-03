// Assets/_Project/Scripts/Testing/GreyboxFlowTest.cs
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Re-runnable end-to-end check of the evidence lifecycle, Scene Entry through
/// Hypothesis Checkpoint. Exists because the previous version of this walkthrough was
/// ad-hoc code typed into the editor once and then lost - which meant that when the
/// Marked step was inserted and the old flow stopped being valid, there was no script
/// to fail, only a memory of one. A test that isn't checked in can't regress; it can
/// only be forgotten.
///
/// This drives the STATE MACHINE directly. It deliberately does not simulate VR input,
/// so it proves the procedural sequence, the gates and the logging - not that a
/// controller trigger is bound correctly. A headset pass is still required for that,
/// and this script is not a substitute for one.
///
/// Run it from the editor via:
///     FindFirstObjectByType&lt;GreyboxFlowTest&gt;().RunFullFlow()
/// or tick runOnStart to have it run automatically on entering Play mode.
/// </summary>
public class GreyboxFlowTest : MonoBehaviour
{
    [Tooltip("Run the full flow automatically when entering Play mode. Leave off for normal play sessions — this drives evidence state directly and will complete the scenario on its own.")]
    [SerializeField] private bool runOnStart;

    [Tooltip("Evidence items to walk through the full lifecycle, in order.")]
    [SerializeField]
    private string[] evidenceIds = { "EVD-014", "EVD-015", "EVD-016", "EVD-017", "EVD-018" };

    private void Start()
    {
        if (runOnStart)
        {
            Debug.Log(RunFullFlow());
        }
    }

    /// <summary>
    /// Walks every configured item from NotFound to Processed in the canonical order,
    /// asserting at each step. Returns a human-readable report; every line that starts
    /// with FAIL is a real regression.
    /// </summary>
    public string RunFullFlow()
    {
        var report = new StringBuilder();
        var failures = new List<string>();

        var esm = EvidenceStateManager.Instance;
        var gate = ProceduralGateValidator.Instance;

        if (esm == null || gate == null)
        {
            return "FAIL: EvidenceStateManager or ProceduralGateValidator missing from scene.";
        }

        report.AppendLine("=== Greybox flow test ===");

        foreach (var id in evidenceIds)
        {
            var record = esm.GetRecord(id);
            if (record == null)
            {
                Fail(failures, report, id + ": not registered in EvidenceStateManager.");
                continue;
            }

            report.AppendLine("--- " + id + " (" + record.definition.displayName
                + ", relevance=" + record.definition.relevance
                + ", requiresFingerprinting=" + record.definition.requiresFingerprinting + ")");

            Step(esm, gate, failures, report, id, EvidenceStatus.Found);

            // The step this test previously did not have. Photograph must be refused
            // until it happens - that is the whole point of inserting Marked.
            if (gate.CanTransition(id, EvidenceStatus.Photographed))
            {
                Fail(failures, report, id + ": Photographed was allowed straight from Found - Marked is not being required.");
            }
            else
            {
                report.AppendLine("    gate correctly refuses Photographed: " + gate.GetBlockReason(id, EvidenceStatus.Photographed));
            }

            Step(esm, gate, failures, report, id, EvidenceStatus.Marked);
            Step(esm, gate, failures, report, id, EvidenceStatus.Photographed);
            Step(esm, gate, failures, report, id, EvidenceStatus.Sketched);
            Step(esm, gate, failures, report, id, EvidenceStatus.Logged);

            // MarkLogged auto-advances to ReadyForCollection, so this is already there.
            if (record.status != EvidenceStatus.ReadyForCollection)
            {
                Fail(failures, report, id + ": expected ReadyForCollection after Logged, got " + record.status);
            }

            Step(esm, gate, failures, report, id, EvidenceStatus.Collected);

            if (record.definition.requiresFingerprinting)
            {
                if (gate.CanTransition(id, EvidenceStatus.Processed))
                {
                    Fail(failures, report, id + ": Processed allowed without fingerprinting despite requiresFingerprinting.");
                }
                else
                {
                    report.AppendLine("    gate correctly refuses Processed: " + gate.GetBlockReason(id, EvidenceStatus.Processed));
                }

                if (esm.MarkFingerprinted(id, ToolType.IOC) != TransitionResult.Applied)
                {
                    Fail(failures, report, id + ": MarkFingerprinted did not apply while Collected.");
                }
                else
                {
                    report.AppendLine("    fingerprinted");
                }
            }

            Step(esm, gate, failures, report, id, EvidenceStatus.Processed);
        }

        report.AppendLine("--- totals ---");
        report.AppendLine("at/above Found:     " + esm.CountAtOrAbove(EvidenceStatus.Found));
        report.AppendLine("at/above Marked:    " + esm.CountAtOrAbove(EvidenceStatus.Marked));
        report.AppendLine("at/above Collected: " + esm.CountAtOrAbove(EvidenceStatus.Collected));
        report.AppendLine("at/above Processed: " + esm.CountAtOrAbove(EvidenceStatus.Processed));

        report.AppendLine(failures.Count == 0
            ? "=== PASS: " + evidenceIds.Length + " items completed the full lifecycle ==="
            : "=== FAILED with " + failures.Count + " problem(s) ===");

        return report.ToString();
    }

    private static void Step(EvidenceStateManager esm, ProceduralGateValidator gate,
        List<string> failures, StringBuilder report, string id, EvidenceStatus target)
    {
        if (!gate.CanTransition(id, target))
        {
            Fail(failures, report, id + ": gate refused " + target + " - " + gate.GetBlockReason(id, target));
            return;
        }

        TransitionResult result;
        switch (target)
        {
            case EvidenceStatus.Found: result = esm.MarkFound(id, ToolType.None); break;
            case EvidenceStatus.Marked: result = esm.MarkTented(id, ToolType.EvidenceMarker); break;
            case EvidenceStatus.Photographed: result = esm.MarkPhotographed(id, ToolType.Photographer); break;
            case EvidenceStatus.Sketched: result = esm.MarkSketched(id, ToolType.Sketcher); break;
            case EvidenceStatus.Logged: result = esm.MarkLogged(id, ToolType.Recorder); break;
            case EvidenceStatus.Collected: result = esm.MarkCollected(id, ToolType.EvidenceCollector); break;
            case EvidenceStatus.Processed: result = esm.MarkProcessed(id, ToolType.IOC); break;
            default:
                Fail(failures, report, id + ": no transition method for " + target);
                return;
        }

        if (result != TransitionResult.Applied)
        {
            Fail(failures, report, id + ": " + target + " returned " + result + " instead of Applied.");
            return;
        }

        report.AppendLine("    -> " + target);
    }

    private static void Fail(List<string> failures, StringBuilder report, string message)
    {
        failures.Add(message);
        report.AppendLine("    FAIL: " + message);
    }
}
