// Assets/_Project/Scripts/Testing/InconclusivePathTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode check that Inconclusive still works for an incomplete investigation: every
/// core item is sketched, but the spatter examination and record remain unfinished.
/// The spatter reconstruction finding is therefore not reached. The
/// picker must offer Inconclusive, and submitting it must resolve ending-inconclusive.
/// Run once per fresh Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class InconclusivePathTest : MonoBehaviour
{
    private const string Inconclusive = "CONCLUSION-INCONCLUSIVE";

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var failures = new List<string>();
        var report = new StringBuilder("=== InconclusivePathTest ===\n");
        // Completes a session: isolate the upload queue / owners / network from real data first.
        AutomatedTestSandbox.Activate(nameof(InconclusivePathTest));
        RelationshipReviewTestUtil.BeginCaseFixture();
        int realQueueBefore = AutomatedTestSandbox.RealQueueEntryCount();
        int realOwnersBefore = AutomatedTestSandbox.RealOwnerRecordCount();
        var esm = EvidenceStateManager.Instance;
        var flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        var board = FindFirstObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        var endings = EndingManager.Instance;
        if (esm == null || flow == null || board == null || endings == null)
        {
            Report = "FAIL: required objects missing.";
            IsDone = true;
            yield break;
        }

        // Process the knife and bottle; leave the spatter examination/record unfinished.
        // Recorder completion now unlocks baseline findings independently of selected accuracy.
        // The full geometric sketch is still completed before final conclusion submission.
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-018" }, 0, true);
        string flowReport = flow.RunFullFlow();
        Check(failures, report, "evidence lifecycle", flowReport.Contains("=== PASS"));
        if (!flowReport.Contains("=== PASS")) report.AppendLine(flowReport);

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
        yield return null;

        STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        Check(failures, report, "confirmed knife match requires the physical vendor follow-up", VendorFollowupInterview.Instance.RequiresFollowup
            && VendorFollowupTest.OpenAtVendor());
        yield return null;
        Check(failures, report, "required vendor question starts", VendorFollowupInterview.Instance.AskAnyoneElse());
        yield return VendorFollowupTest.DrainDialogue(VendorFollowupInterview.Instance);
        Check(failures, report, "delivered vendor answer clears completion gate", VendorFollowupInterview.CompletionBlockReason == null);

        var relationships = RelationshipStateManager.Instance;
        foreach (var id in new[] { "REL-knife-consistent-wound", "REL-spatter-consistent-reconstruction", "REL-bottle-supports-brother" })
        {
            var record = relationships.GetRecord(id);
            if (record != null && record.state == RelationshipState.Available)
            {
                relationships.MarkSelectedForPair(record.definition.sourceId, record.definition.targetId);
            }
        }
        RelationshipReviewTestUtil.RecordEarnedReasoning();
        RelationshipReviewTestUtil.ConfirmAvailable();
        board.SubmitTheory();
        yield return null;

        var conclusions = ConclusionStateManager.Instance;
        Check(failures, report, "spatter reconstruction finding NOT reached", !FindingStateManager.Instance.IsConfirmed("FIND-SPATTER-RECONSTRUCTION"));
        Check(failures, report, "Paul and Peter remain selectable while the case is incomplete",
            conclusions.IsAvailable("conclusion-brother") && conclusions.IsAvailable("conclusion-peter"));
        Check(failures, report, "Inconclusive available", conclusions.IsAvailable(Inconclusive));

        Check(failures, report, "complete sketch remains possible before spatter examination", MeasurementTestUtil.CompleteSceneSketch());
        Check(failures, report, "spatter processing remains unfinished in the inconclusive fixture", !esm.GetRecord("EVD-015").swabbingDone
            && !esm.IsAtOrPastStatus("EVD-015",EvidenceStatus.Logged));
        STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        endings.TrySubmitConclusion();
        float t = 0f;
        while (endings.GetPickerOptionIdsForTest().Count == 0 && t < 3f)
        {
            var ui = FindFirstObjectByType<HypothesisCheckpointUI>();
            if (ui != null && ui.IsShowing)
            {
                ui.AnswerForTest();
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        var ids = endings.GetPickerOptionIdsForTest();
        Check(failures, report, $"picker shows Inconclusive with Paul and Peter (got {string.Join(",", ids)})",
            ids.Contains(Inconclusive) && ids.Contains("conclusion-brother") && ids.Contains("conclusion-peter"));
        RelationshipReviewTestUtil.EnableAssessmentCapture();
        Check(failures, report, "Inconclusive confirm accepted", endings.SelectConclusionForTest(Inconclusive));

        var completion = FindFirstObjectByType<CompletionScreenUI>();
        t = 0f;
        while (completion != null && !completion.IsShowing && t < 3f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Check(failures, report, "completion screen shown", completion != null && completion.IsShowing);
        Check(failures, report, "Inconclusive resolves ending-inconclusive", endings.ResolveEnding(Inconclusive).Ending?.endingId == "ending-inconclusive");

        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" });
        if (completion != null && completion.IsShowing)
        {
            completion.DismissForTest();
        }

        Check(failures, report, "[closeout] conclusion feedback acknowledgement accepted", endings.AcceptConclusionFeedback());

        // Final upload follows closeout, not the initial conclusion screen.
        string thisSession = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
        Check(failures, report, "[isolation] accepted conclusion waits for closeout before uploading",
            SceneTurnover.Phase == ScenarioPhase.SOCOCloseout && AutomatedTestSandbox.Queue != null
            && !AutomatedTestSandbox.Queue.LoadIndex().entries.Exists(e => e.sessionId == thisSession));
        Check(failures, report, "[isolation] real upload queue and real session owners unchanged",
            AutomatedTestSandbox.RealQueueEntryCount() == realQueueBefore && AutomatedTestSandbox.RealOwnerRecordCount() == realOwnersBefore);

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool passed)
    {
        report.AppendLine($"  {(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed)
        {
            failures.Add(label);
        }
    }
}
