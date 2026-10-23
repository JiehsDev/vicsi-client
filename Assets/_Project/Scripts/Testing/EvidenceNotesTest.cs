// Assets/_Project/Scripts/Testing/EvidenceNotesTest.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Editor diagnostic for the progressive evidence-note contract. Run in a fresh Play
/// session because it advances real EvidenceStateManager records through their public
/// APIs; leaving Play mode restores the scene state.
/// </summary>
public class EvidenceNotesTest : MonoBehaviour
{
    private static readonly string[] EvidenceIds =
    {
        "EVD-014", "EVD-015", "EVD-018", "BODY-A"
    };

    public string RunAllTests()
    {
        var failures = new List<string>();
        var report = new StringBuilder("=== EvidenceNotesTest ===\n");
        var manager = EvidenceStateManager.Instance;
        if (manager == null)
        {
            return report.AppendLine("FAIL: EvidenceStateManager.Instance is missing.").ToString();
        }

        // Evidence work is gated on the overall scene photography - route first.
        Check(failures, report, "overall scene photography completed first", ScenePhotographyTestUtil.CompleteRoute());

        foreach (string id in EvidenceIds)
        {
            Check(failures, report, $"{id} is absent before discovery",
                string.IsNullOrEmpty(EvidenceNoteComposer.Compose(manager.GetRecord(id))));
        }

        const string progressiveId = "EVD-014";
        const string woundComparison = "consistent with the sharp-edged wound";
        Apply(failures, report, "discover knife", manager.MarkFound(progressiveId, ToolType.IOC));
        string discovered = EvidenceNoteComposer.Compose(manager.GetRecord(progressiveId));
        Check(failures, report, "discovery reveals basic description/location/condition",
            ContainsAll(discovered, manager.GetDisplayName(progressiveId), "Description", "Found location", "Observed condition") && !discovered.Contains(progressiveId, StringComparison.Ordinal));
        Check(failures, report, "discovery does not reveal swab result", !discovered.Contains("A sample was collected", StringComparison.Ordinal));
        Check(failures, report, "discovery does not reveal fingerprint result", !discovered.Contains("usable latent", StringComparison.Ordinal));
        Check(failures, report, "discovery does not reveal wound comparison", !discovered.Contains(woundComparison, StringComparison.Ordinal));

        Apply(failures, report, "raw photograph", manager.MarkPhotographedRaw(progressiveId, ToolType.Photographer));
        Apply(failures, report, "chalk outline", manager.MarkChalkMarked(progressiveId, ToolType.EvidenceMarker));
        Apply(failures, report, "evidence marker", manager.MarkTented(progressiveId, ToolType.EvidenceMarker, 14));
        TraceMarkerTestUtil.MarkTraceIfRequired(progressiveId);
        Apply(failures, report, "final photograph", manager.MarkPhotographedFinal(progressiveId, ToolType.Photographer));
        MeasurementTestUtil.CompleteSketchFor(progressiveId);
        Apply(failures, report, "swab blade", manager.MarkSwabbed(progressiveId, ToolType.Swab));

        string swabbed = EvidenceNoteComposer.Compose(manager.GetRecord(progressiveId));
        Check(failures, report, "swab finding appears only after swabbing", swabbed.Contains("A sample was collected", StringComparison.Ordinal));
        Check(failures, report, "fingerprint finding remains hidden after swabbing", !swabbed.Contains("usable latent", StringComparison.Ordinal));
        Check(failures, report, "wound comparison remains hidden before report finding", !swabbed.Contains(woundComparison, StringComparison.Ordinal));

        Apply(failures, report, "fingerprint handle", manager.MarkFingerprinted(progressiveId, ToolType.FingerprintKit));
        string fingerprinted = EvidenceNoteComposer.Compose(manager.GetRecord(progressiveId));
        Check(failures, report, "fingerprint finding appears after fingerprint processing", fingerprinted.Contains("usable latent", StringComparison.Ordinal));
        Check(failures, report, "fingerprint identity is not exposed", !ContainsAny(fingerprinted, "wife", "brother", "suspect"));

        manager.RecordReportLinesSelected(progressiveId, new[] { "knife-wound-match" });
        string reported = EvidenceNoteComposer.Compose(manager.GetRecord(progressiveId));
        Check(failures, report, "wound comparison appears after its report finding", reported.Contains(woundComparison, StringComparison.Ordinal));

        foreach (string id in EvidenceIds)
        {
            if (id != progressiveId)
            {
                Apply(failures, report, $"discover {id}", manager.MarkFound(id, ToolType.IOC));
            }

            string note = EvidenceNoteComposer.Compose(manager.GetRecord(id));
            Check(failures, report, $"{id} has readable core sections",
                note.Length >= 180 && ContainsAll(note, manager.GetDisplayName(id), "Description", "Found location", "Observed condition", "Procedures / status", "Investigative context") && !note.Contains(id, StringComparison.Ordinal));
            Check(failures, report, $"{id} note does not reveal suspect/conclusion",
                !ContainsAny(note, "the wife", "the brother", "committed the crime", "committed the murder", "correct suspect", "correct conclusion"));
        }

        report.AppendLine(failures.Count == 0 ? $"=== PASS: {EvidenceIds.Length} evidence notes validated ===" : $"=== FAIL: {failures.Count} check(s) failed ===");
        return report.ToString();
    }

    private static void Apply(List<string> failures, StringBuilder report, string label, TransitionResult result)
    {
        Check(failures, report, label, result == TransitionResult.Applied);
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool passed)
    {
        report.AppendLine($"{(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed) failures.Add(label);
    }

    private static bool ContainsAll(string text, params string[] values)
    {
        foreach (string value in values)
        {
            if (!text.Contains(value, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        foreach (string value in values)
        {
            if (text.Contains(value, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
