// Assets/_Project/Scripts/Testing/StoryContentTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Validates the authored CSI_Environment story content (NarrativeEvents_CSI_Environment,
/// Objectives_CSI_Environment, the 7 RelationshipDefinitions, Endings_CSI_Environment) end
/// to end, against the REAL scene singletons (EvidenceStateManager, RelationshipStateManager,
/// NarrativeEventManager, ObjectiveStateManager, EndingManager) - unlike this project's other
/// test scripts, this one deliberately DOES advance real evidence/relationship/objective/
/// ending state, the same way GreyboxFlowTest does, because the thing under test IS that
/// real content and its real wiring, not an isolated throwaway rig.
///
/// Runs GreyboxFlowTest.RunFullFlow() first for the physical/procedural lifecycle (proven
/// elsewhere - not re-verified here), then directly calls EvidenceStateManager.
/// RecordReportLinesSelected for the specific narratively-critical report lines
/// GreyboxFlowTest's generic "first N options" selection does not happen to reach (this is
/// the same public API RecorderTool itself calls - not a fabricated shortcut), then drives
/// the deduction-board relationships EvidenceBoardController would (MarkSelectedForPair +
/// CompleteAllSelected) since no board UI exists in this scene yet to do it via drag-connect.
///
/// BrotherPathTest separately covers incomplete-Paul and unsupported-Peter outcomes;
/// this full-flow diagnostic verifies the supported case and picker suppression behaviour.
/// </summary>
public class StoryContentTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    public void RunAllTests()
    {
        StartCoroutine(RunAllTestsRoutine());
    }

    private IEnumerator RunAllTestsRoutine()
    {
        var failures = new List<string>();
        var report = new StringBuilder();
        report.AppendLine("=== StoryContentTest ===");
        AutomatedTestSandbox.Activate(nameof(StoryContentTest));   // completes a session
        RelationshipReviewTestUtil.BeginCaseFixture();

        var flow = FindFirstObjectByType<GreyboxFlowTest>();
        if (flow == null)
        {
            failures.Add("No GreyboxFlowTest in scene - can't drive the evidence lifecycle.");
            Report = FinishReport(report, failures);
            IsDone = true;
            yield break;
        }

        // This suite submits after the body findings and board relationships are ready.
        // Paul is selectable earlier now, so the procedural setup must not end the session.
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" }, skipSubmit: true);
        string flowReport = flow.RunFullFlow();
        report.AppendLine("--- GreyboxFlowTest.RunFullFlow() ---");
        report.AppendLine(flowReport.Contains("=== PASS") ? "  (physical/procedural lifecycle: PASS)" : "  FAIL: GreyboxFlowTest itself did not pass - see full flow report.");
        if (!flowReport.Contains("=== PASS"))
        {
            failures.Add("GreyboxFlowTest.RunFullFlow() did not pass - story content cannot be meaningfully validated on top of a broken lifecycle.");
            report.AppendLine(flowReport);
        }

        var esm = EvidenceStateManager.Instance;

        // BODY-A is NOT in GreyboxFlowTest's default evidenceIds array - RunFullFlow
        // never touches it, so its status is still NotFound at this point. Drive it
        // through the real sequence directly (same public Mark* methods GreyboxFlowTest
        // itself calls) rather than only selecting a report line against an item that
        // was never actually found - caught as a real gap in this test's own first draft,
        // where RecordReportLinesSelected succeeded even though BODY-A had never been
        // marked Found, silently passing every check gated on that line while
        // EvidenceDiscovered(BODY-A) correctly still read false.
        esm.MarkFound("BODY-A", ToolType.None);
        esm.MarkPhotographedRaw("BODY-A", ToolType.None);
        esm.MarkChalkMarked("BODY-A", ToolType.ChalkMark); // outline first: chalk is required before its marker
        esm.MarkTentedWithLetter("BODY-A", ToolType.LetterTentMarker, "A");
        esm.MarkPhotographedFinal("BODY-A", ToolType.None);
        // Evidence records wait for the discovered evidence's measurements (fixed references + pairs among discovered items).
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        MeasurementTestUtil.CompleteSketchFor("BODY-A");
        esm.RecordReportLinesSelected("BODY-A", new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" });
        esm.MarkLogged("BODY-A", ToolType.None);

        // Retroactively select the specific narratively-critical lines GreyboxFlowTest's
        // generic "first N options" selection does not happen to reach - the same public
        // API RecorderTool itself calls (EvidenceStateManager.RecordReportLinesSelected),
        // not a fabricated shortcut.
        esm.RecordReportLinesSelected("EVD-014", new[] { "knife-wound-match" });
        esm.RecordReportLinesSelected("EVD-015", new[] { "blood-spatter-mechanism-only" });

        // Every authored beat uses playAllLinesInSequence with real per-line delays
        // (lineDisplaySeconds) - a 7-line beat genuinely takes ~18 real seconds to
        // finish playing, and by now every beat's trigger is simultaneously satisfied,
        // so all 9 are queued back-to-back (~50+ seconds total). Sped up here, for this
        // test run only, via reflection on the real NarrativeEventManager instance -
        // never written back to the asset, and restored in the finally block below, so
        // real gameplay pacing is unaffected.
        var narManager = NarrativeEventManager.Instance;
        var lineDisplayField = typeof(NarrativeEventManager).GetField("lineDisplaySeconds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float originalLineDisplaySeconds = (float)lineDisplayField.GetValue(narManager);
        lineDisplayField.SetValue(narManager, 0.02f);

        yield return WaitUntilOrTimeout(() =>
        {
            string[] allBeats = { "beat-1-initial-briefing","beat-2-body-doubt","beat-3-knife-wound-match","beat-4-brother-fingerprint","beat-5-bottle-supports-brother","beat-7-spatter-reconstruction" };
            foreach (var b in allBeats)
            {
                if (!narManager.IsCompleted(b)) return false;
            }
            return true;
        }, 15f);

        lineDisplayField.SetValue(narManager, originalLineDisplaySeconds);

        Check(failures, report, "beat-1-initial-briefing fired (always-eligible)", NarrativeEventManager.Instance.IsCompleted("beat-1-initial-briefing"));
        Check(failures, report, "beat-2-body-doubt fired", NarrativeEventManager.Instance.IsCompleted("beat-2-body-doubt"));
        Check(failures, report, "beat-3-knife-wound-match fired", NarrativeEventManager.Instance.IsCompleted("beat-3-knife-wound-match"));
        Check(failures, report, "beat-4-brother-fingerprint fired", NarrativeEventManager.Instance.IsCompleted("beat-4-brother-fingerprint"));
        Check(failures, report, "beat-5-bottle-supports-brother fired", NarrativeEventManager.Instance.IsCompleted("beat-5-bottle-supports-brother"));
        Check(failures, report, "beat-7-spatter-reconstruction fired", NarrativeEventManager.Instance.IsCompleted("beat-7-spatter-reconstruction"));

        var objectives = ObjectiveStateManager.Instance;
        Check(failures, report, "obj-document-body Completed (not Failed)", objectives.GetRecord("obj-document-body")?.state == ObjectiveState.Completed);
        Check(failures, report, "obj-identify-poi Completed (not Failed)", objectives.GetRecord("obj-identify-poi")?.state == ObjectiveState.Completed);
        Check(failures, report, "obj-identify-murder-weapon Completed", objectives.GetRecord("obj-identify-murder-weapon")?.state == ObjectiveState.Completed);
        Check(failures, report, "obj-verify-fingerprint-timing Completed", objectives.GetRecord("obj-verify-fingerprint-timing")?.state == ObjectiveState.Completed);
        Check(failures, report, "obj-reconstruct-crime Completed", objectives.GetRecord("obj-reconstruct-crime")?.state == ObjectiveState.Completed);

        var relationships = RelationshipStateManager.Instance;
        // The existing vendor lead requires a locked fingerprint reasoning response,
        // as well as the processed print. This is a story fixture, not a bypass of that gate.
        var reasoning = ReasoningCheckpointManager.Instance;
        reasoning?.Evaluate();
        Check(failures, report, "Fingerprint reasoning response is locked before the physical vendor interview",
            reasoning != null && (reasoning.IsAnswered("RC-03-FINGERPRINT")
                || reasoning.RecordResponse("RC-03-FINGERPRINT", "RC-03-FINGERPRINT-C")));
        // Full evidence processing can leave HYP-03 open. Finish that real modal
        // before interviewing, just as the player must; do not bypass the UI gate.
        var interviewCheckpoint = FindFirstObjectByType<HypothesisCheckpointUI>();
        float interviewGateWait = 0f;
        while (interviewCheckpoint != null && interviewCheckpoint.IsShowing && interviewGateWait < 3f)
        {
            interviewCheckpoint.AnswerForTest();
            interviewGateWait += Time.unscaledDeltaTime;
            yield return null;
        }
        Check(failures, report, "Checkpoint releases the UI gate before the vendor interview", !PlayerUIGate.IsBlocked);
        var interview = VendorFollowupInterview.Instance;
        bool vendorOpened = VendorFollowupTest.OpenAtVendor();
        yield return null;
        Check(failures, report, "Vendor follow-up starts through the physical vendor question action",
            vendorOpened && interview != null && interview.AskAnyoneElse());
        if (interview != null) yield return VendorFollowupTest.DrainDialogue(interview);
        Check(failures, report, "Vendor follow-up discovers the independent timeline observation",
            interview != null && interview.IsDiscovered);
        string[] scenarioRelIds = {
            "REL-knife-consistent-wound", "REL-bottle-supports-brother", "REL-spatter-consistent-reconstruction", "REL-paul-print-handled-knife", "REL-peter-departure-supported", "REL-paul-timeline-contradiction"
        };
        foreach (var relId in scenarioRelIds)
        {
            Check(failures, report, $"{relId} reached Available (unlockRequirement satisfied)", relationships.GetRecord(relId)?.state >= RelationshipState.Available);
        }

        // Confirm earned interpretations without any manual node/link operations.
        RelationshipReviewTestUtil.ConfirmAvailable();
        // The real Submit Theory action: completes every Selected relationship and
        // records that a theory was submitted, which Submit Conclusion is gated on.
        var board = FindFirstObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        if (board != null)
        {
            RelationshipReviewTestUtil.ConfirmAvailable();
            board.SubmitTheory();
        }
        else
        {
            failures.Add("No EvidenceBoardController in scene - can't submit a theory.");
            relationships.CompleteAllSelected();
        }

        foreach (var relId in scenarioRelIds)
        {
            Check(failures, report, $"{relId} reached Completed after board connection + Submit Conclusion finalize", relationships.GetRecord(relId)?.state == RelationshipState.Completed);
        }

        // --- Ending resolution, against this fully-played-through state ---
        var endingManager = EndingManager.Instance;

        Check(failures, report, "No wife ending remains in the EndingSet",
            endingManager.ResolveEnding("CONCLUSION-WIFE-INVOLVEMENT").Ending?.endingId?.StartsWith("ending-wife") != true);

        var brotherResult = endingManager.ResolveEnding("conclusion-brother");
        Check(failures, report, $"conclusion-brother resolves to ending-brother-accused (got {brotherResult.Ending?.endingId})",
            brotherResult.Ending?.endingId == "ending-brother-accused");

        var inconclusiveResult = endingManager.ResolveEnding("CONCLUSION-INCONCLUSIVE");
        Check(failures, report, $"CONCLUSION-INCONCLUSIVE resolves to ending-inconclusive (got {inconclusiveResult.Ending?.endingId})",
            inconclusiveResult.Ending?.endingId == "ending-inconclusive");

        var peterResult = endingManager.ResolveEnding("conclusion-peter");
        Check(failures, report, $"conclusion-peter resolves to ending-peter-unsupported (got {peterResult.Ending?.endingId})",
            peterResult.Ending?.endingId == "ending-peter-unsupported");

        var noSelectionResult = endingManager.ResolveEnding(null);
        Check(failures, report, $"No conclusion selected also resolves to ending-inconclusive (got {noSelectionResult.Ending?.endingId})",
            noSelectionResult.Ending?.endingId == "ending-inconclusive");

        // --- Full pipeline through the real picker + CompletionScreenUI, once ---
        // HYP-03 now fires at the processed-evidence threshold, before submission.
        // Answer any open checkpoint through its real UI before attempting Submit Conclusion.
        var checkpointUI = FindFirstObjectByType<HypothesisCheckpointUI>();
        float checkpointWait = 0f;
        while (checkpointUI != null && checkpointUI.IsShowing && checkpointWait < 3f)
        {
            checkpointUI.AnswerForTest();
            checkpointWait += Time.deltaTime;
            yield return null;
        }
        Check(failures, report, "Checkpoint UI releases the submission gate before the picker", !PlayerUIGate.IsBlocked);
        RelationshipReviewTestUtil.RecordEarnedReasoning();
        MeasurementTestUtil.CompleteSceneSketch();
        STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        endingManager.TrySubmitConclusion();

        // Submit opens any SubmissionInitiated checkpoint (HYP-03) first; the picker only
        // appears once it has been answered.
        yield return null;
        if (checkpointUI != null && checkpointUI.IsShowing)
        {
            checkpointUI.AnswerForTest();
        }

        // Full investigation: a supported Case conclusion (brother) is available, so the
        // picker withholds Inconclusive and refuses a confirm of it (see
        // EndingManager.GetSelectableCaseConclusions). Wait for the picker first.
        yield return WaitUntilOrTimeout(() => endingManager.GetPickerOptionIdsForTest().Count > 0, 3f);
        var pickerIds = endingManager.GetPickerOptionIdsForTest();
        Check(failures, report, $"Picker offers Paul and Peter but not Inconclusive (got {string.Join(",", pickerIds)})",
            pickerIds.Contains("conclusion-brother") && pickerIds.Contains("conclusion-peter") && !pickerIds.Contains("CONCLUSION-INCONCLUSIVE"));
        Check(failures, report, "Confirming hidden Inconclusive is refused", !endingManager.SelectConclusionForTest("CONCLUSION-INCONCLUSIVE"));

        RelationshipReviewTestUtil.EnableAssessmentCapture();
        bool selected = false;
        yield return WaitUntilOrTimeout(() => endingManager.SelectConclusionForTest("conclusion-brother") && (selected = true), 3f);
        Check(failures, report, "SelectConclusionForTest(conclusion-brother) succeeds while the real picker is showing", selected);

        var completionScreen = FindFirstObjectByType<CompletionScreenUI>();
        float elapsed = 0f;
        while (completionScreen != null && !completionScreen.IsShowing && elapsed < 3f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        Check(failures, report, "Full Submit Conclusion pipeline shows the real CompletionScreenUI", completionScreen != null && completionScreen.IsShowing);
        float completedSeconds = SceneTurnover.InvestigationSeconds;
        yield return new WaitForSecondsRealtime(.05f);
        Check(failures, report, "Accepted conclusion feedback preserves the investigation and keeps its SOCO clock running",
            SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation && EndingManager.Instance.AcceptedEnding != null
            && SceneTurnover.InvestigationSeconds > completedSeconds);

        if (completionScreen != null && completionScreen.IsShowing)
        {
            completionScreen.DismissForTest();
        }

        Report = FinishReport(report, failures);
        IsDone = true;
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static string FinishReport(StringBuilder report, List<string> failures)
    {
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        return report.ToString();
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool condition)
    {
        if (condition)
        {
            report.AppendLine($"  PASS: {label}");
        }
        else
        {
            failures.Add(label);
            report.AppendLine($"  FAIL: {label}");
        }
    }
}
