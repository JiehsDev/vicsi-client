using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Required body sketch, final sheet submission, and authoritative completion gates.</summary>
public sealed class SketchCompletionGateTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== SketchCompletionGateTest ===\n");
    int failures;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    void Check(string label, bool ok) { report.AppendLine((ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures++; }
    public void RunAllTests() { AutomatedTestSandbox.Activate(nameof(SketchCompletionGateTest)); StartCoroutine(Run()); }
    IEnumerator Run()
    {
        RelationshipReviewTestUtil.BeginCaseFixture();
        yield return null;
        var sketch = CrimeSceneSketchManager.Instance;
        var state = EvidenceStateManager.Instance;
        var ending = EndingManager.Instance;
        var closeout = SOCOCloseout.Instance;
        var feedback = typeof(EndingManager).GetField("<FeedbackAccepted>k__BackingField", Private);
        var phase = typeof(SceneTurnover).GetField("<Phase>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var released = typeof(SOCOCloseout).GetField("<ReleaseCompleted>k__BackingField", Private);
        bool priorFeedback = ending.FeedbackAccepted;
        bool priorRelease = closeout.ReleaseCompleted;
        var priorPhase = SceneTurnover.Phase;
        try
        {
            Check("Unsubmitted sketch explains how to finish through the tool wheel", CrimeSceneSketchManager.CompletionBlockReason != null
                && CrimeSceneSketchManager.CompletionBlockReason.Contains("Submit Sketch"));
            ending.TrySubmitConclusion();
            Check("Conclusion cannot open while the sketch is unsubmitted", ending.AcceptedEnding == null && ending.GetPickerOptionIdsForTest().Count == 0);
            Check("Refusal leaves tools and navigation accessible", !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners);
            Clear();
            Check("Actual scene photography and measurements complete", ScenePhotographyTestUtil.CompleteRoute() && MeasurementTestUtil.CompleteSceneMeasurements());
            var body = state.GetRecord("BODY-A");
            var lines = new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" };
            state.RecordReportLinesSelected("BODY-A", lines);
            Check("Report-line metadata alone does not finalize body documentation", !body.bodyFindingsRecorded && body.status == EvidenceStatus.PhotographedFinal);
            Check("Measured body cannot be recorded before sketch placement", !state.CanAdvanceTo("BODY-A", EvidenceStatus.Logged)
                && state.MarkLogged("BODY-A", ToolType.Recorder) == TransitionResult.Violation && !body.bodyFindingsRecorded);
            Check("Body recording refusal points to Scene Sketch", state.GetItemBlockReason("BODY-A", EvidenceStatus.Logged).Contains("Scene Sketch"));
            Check("Body is correctly placed with automatic distances", MeasurementTestUtil.CompleteSketchFor("BODY-A"));
            state.RecordReportLinesSelected("BODY-A", lines);
            Check("Sketch placement unlocks body recording without final sheet submission", state.MarkLogged("BODY-A", ToolType.Recorder) == TransitionResult.Applied && !sketch.IsSubmitted);
            // A legacy/altered state must also be protected at the actual removal APIs.
            sketch.ResetSession();
            Check("Recorded body still refuses preparation if its sketch is missing", state.GetBodyRemovalBlockReason("BODY-A").Contains("Scene Sketch")
                && state.MarkBodyPreparedForRemoval("BODY-A") == TransitionResult.Violation);
            body.bodyRemovalStatus = BodyRemovalStatus.PreparedForTransport;
            Check("Removal cannot bypass the sketch through a prepared-state fixture", state.MarkBodyRemoved("BODY-A") == TransitionResult.Violation
                && body.bodyRemovalStatus == BodyRemovalStatus.PreparedForTransport);
            body.bodyRemovalStatus = BodyRemovalStatus.NotPrepared;
            foreach (var element in sketch.Definition.evidenceElements)
                Check("Required sketch placement: " + element.evidenceId, MeasurementTestUtil.CompleteSketchFor(element.evidenceId));
            Check("All placements and distances are ready but not auto-submitted", sketch.IsReadyForSubmission && !sketch.IsSubmitted);
            Clear(); ending.TrySubmitConclusion(); Clear();
            Check("Complete placements alone still cannot submit the conclusion", ending.AcceptedEnding == null && ending.GetPickerOptionIdsForTest().Count == 0);
            feedback.SetValue(ending, true);
            Check("Even accepted-feedback state cannot bypass unsubmitted sketch", !SceneTurnover.TryAdvance(ScenarioPhase.SOCOCloseout));
            Check("Explicit sketch submission clears its completion gate", sketch.Submit() && CrimeSceneSketchManager.CompletionBlockReason == null);
            Check("Submitted sketch permits the existing closeout transition", SceneTurnover.TryAdvance(ScenarioPhase.SOCOCloseout));
            phase.SetValue(null, ScenarioPhase.SceneRelease); released.SetValue(closeout, true); sketch.ResetSession();
            Check("Final completion also refuses an unsubmitted sketch", !SceneTurnover.TryAdvance(ScenarioPhase.InvestigationComplete));
            Check("Deduction weights and separate Recorder maximum stay unchanged", FinalDeductionAssessment.Rules.checkpointWeight == 60
                && FinalDeductionAssessment.Rules.relationshipWeight == 25 && FinalDeductionAssessment.Rules.conclusionWeight == 15
                && RecorderDocumentation.Catalog.maximumTotal == 24);
        }
        finally { phase.SetValue(null, priorPhase); feedback.SetValue(ending, priorFeedback); released.SetValue(closeout, priorRelease); Clear(); }
        report.AppendLine(failures == 0 ? "=== PASS ===" : "=== FAIL ==="); Report = report.ToString(); IsDone = true;
    }
    static void Clear() { STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip(); }
}
