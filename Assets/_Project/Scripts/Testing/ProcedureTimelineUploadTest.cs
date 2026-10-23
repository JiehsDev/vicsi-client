using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Tests the current minimal-upload filter with source-specific completion records.</summary>
public sealed class ProcedureTimelineUploadTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== ProcedureTimelineUploadTest ===\n");
    private int failures;
    private void Check(string label, bool value) { report.AppendLine((value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    public void RunAllTests()
    {
        var types = new[] { "SceneLogSigned", "PPEEquipped", "UsedGlovesDisposed", "FreshGlovesEquipped", "FreshSwabLoaded",
            "PhotographicScaleCaptured", "SampleDryingCompleted", "SamplePackaged", "FingerprintPracticeCompleted" };
        var events = types.Select((type, i) => new SessionEvent { sequenceNumber = i * 3 + 2, timestampMs = 500L + i * 1000,
            eventType = type, targetId = "EVD-015", payload = new List<PayloadEntry>() }).ToList();
        foreach (var type in new[] { "SampleDryingCompleted", "SamplePackaged" })
            events.Add(new SessionEvent { sequenceNumber = events.Last().sequenceNumber + 3, timestampMs = events.Last().timestampMs + 1000,
                eventType = type, targetId = "EVD-014", payload = new List<PayloadEntry>() });
        var completed = events.ToArray();
        foreach (var type in new[] { "SampleDryingStarted", "EvidenceTransitionBlocked", "ToolEquipped", "FingerprintPracticeOpened" })
            events.Add(new SessionEvent { sequenceNumber = events.Last().sequenceNumber + 3, timestampMs = events.Last().timestampMs + 1000,
                eventType = type, targetId = "EVD-014", payload = new List<PayloadEntry>() });
        var dto = new AssessmentSessionUploadDto { scoringRulesVersion = "1.7.0" };
        EducationalSessionRecord.BuildMinimalUpload(dto, events);
        foreach (var type in types) Check(type + " retained without raw logs", dto.orderedEvents.Any(e => e.eventType == type));
        Check("Both blood sources retain their own preparation records", new[] { "SampleDryingCompleted", "SamplePackaged" }
            .All(type => dto.orderedEvents.Where(e => e.eventType == type).Select(e => e.targetId).SequenceEqual(new[] { "EVD-015", "EVD-014" })));
        Check("Original sequence numbers, timestamps and targets preserved", dto.orderedEvents.Count == completed.Length
            && dto.orderedEvents.Zip(completed, (a, b) => a.sequenceNumber == b.sequenceNumber && a.timestampMs == b.timestampMs && a.targetId == b.targetId).All(v => v));
        Check("Started, blocked and tool-open events remain outside completion timeline", dto.orderedEvents.All(e => completed.Any(c => c.eventType == e.eventType)));
        Check("Raw logs and duplicate essential section remain disabled", !dto.includesRawEventLog && dto.deductionAssessmentEvents.Count == 0);
        var json = EducationalSessionRecord.Serialize(dto, true);
        var decoded = JsonUtility.FromJson<AssessmentSessionUploadDto>(json);
        Check("All completion records survive actual wire serialization", decoded.orderedEvents.Count == completed.Length
            && decoded.orderedEvents.Select(e => e.eventType).SequenceEqual(completed.Select(e => e.eventType)));
        Directory.CreateDirectory("docs/evidence/dashboard-workflow-refresh-2026-10-20");
        File.WriteAllText("docs/evidence/dashboard-workflow-refresh-2026-10-20/procedure-events-wire.json", json);
        report.AppendLine(failures == 0 ? "=== PASS ===" : "=== FAIL ==="); Report = report.ToString(); IsDone = true;
    }
}
