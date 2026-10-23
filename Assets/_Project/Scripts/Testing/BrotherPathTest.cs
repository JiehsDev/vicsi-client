// Assets/_Project/Scripts/Testing/BrotherPathTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode check of the brother-only deduction path, driven through the real public
/// state APIs (EvidenceStateManager Mark*/RecordReportLinesSelected, the real board
/// SubmitTheory, the real hypothesis checkpoint UI and conclusion picker). Conclusion
/// availability is one-way, so the scenarios run in increasing order of support in one
/// session: nothing -> fingerprint only -> knife path without the spatter -> full path.
/// Run once per fresh Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class BrotherPathTest : MonoBehaviour
{
    private const string BrotherConclusion = "conclusion-brother"; // Stable persisted id for Paul.
    private const string PeterConclusion = "conclusion-peter";
    private const string InconclusiveConclusion = "CONCLUSION-INCONCLUSIVE";

    private static readonly HashSet<string> RetiredIds = new()
    {
        "EVD-017", "EVD-019", "CONCLUSION-WIFE-INVOLVEMENT", "conclusion-wife", "ending-wife-convicted",
        "FIND-PHONE-AFFAIR", "FIND-GLOVES-PRINT-WIFE", "hyp03-gloves-wife-match", "hyp03-phone-overclaim"
    };

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly Dictionary<string, string> answeredReasoning = new();

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var failures = new List<string>();
        var report = new StringBuilder("=== BrotherPathTest ===\n");
        // Completes a session: isolate the upload queue / owners / network from real data first.
        AutomatedTestSandbox.Activate(nameof(BrotherPathTest));
        RelationshipReviewTestUtil.BeginCaseFixture();
        int realQueueBefore = AutomatedTestSandbox.RealQueueEntryCount();
        int realOwnersBefore = AutomatedTestSandbox.RealOwnerRecordCount();
        var esm = EvidenceStateManager.Instance;
        var conclusions = ConclusionStateManager.Instance;
        var relationships = RelationshipStateManager.Instance;
        var board = FindFirstObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        if (esm == null || conclusions == null || relationships == null || board == null || EndingManager.Instance == null)
        {
            Report = "FAIL: required managers missing.";
            IsDone = true;
            yield break;
        }

        // --- Stage 0: nothing investigated ---
        yield return Settle();
        Check(failures, report, "[start] brother conclusion unavailable", !conclusions.IsAvailable(BrotherConclusion));
        Check(failures, report, "[start] Inconclusive unavailable before any meaningful finding", !conclusions.IsAvailable(InconclusiveConclusion));

        // --- Stage A: fingerprint only (knife through the real GreyboxFlowTest lifecycle) ---
        var flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        if (flow == null)
        {
            Report = "FAIL: no GreyboxFlowTest in scene.";
            IsDone = true;
            yield break;
        }
        GreyboxFlowTest.CheckpointAnswererForTest = _ => AnswerOpenCheckpointCorrectly(null);
        flow.SetEvidenceIdsForTest(new[] { "EVD-014" }, 0, true);
        RunFlow(flow, failures, report, "knife lifecycle (found -> swab -> print -> match -> processed)");
        yield return Settle();
        Check(failures, report, "[fingerprint only] FIND-KNIFE-PRINT-BROTHER confirmed", FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER"));
        Check(failures, report, "[fingerprint only] brother conclusion UNAVAILABLE", !conclusions.IsAvailable(BrotherConclusion));
        Check(failures, report, "[fingerprint only] Inconclusive available (meaningful investigation)", conclusions.IsAvailable(InconclusiveConclusion));
        CheckSelectable(failures, report, "[fingerprint only] picker rule offers exactly Inconclusive", InconclusiveConclusion);

        // --- Stage B: wound + weapon match + print, connected - but no spatter reconstruction ---
        esm.MarkFound("BODY-A", ToolType.None);
        esm.MarkPhotographedRaw("BODY-A", ToolType.None);
        esm.MarkChalkMarked("BODY-A", ToolType.ChalkMark); // outline first: chalk is required before its marker
        esm.MarkTentedWithLetter("BODY-A", ToolType.LetterTentMarker, "A");
        esm.MarkPhotographedFinal("BODY-A", ToolType.None);
        MeasurementTestUtil.CompleteSketchFor("BODY-A");
        esm.RecordReportLinesSelected("BODY-A", new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" });
        MeasurementTestUtil.CompleteDiscoveredMeasurements(); // records wait for the discovered evidence's measurements
        Apply(failures, report, "body logged", esm.MarkLogged("BODY-A", ToolType.None));
        esm.RecordReportLinesSelected("EVD-014", new[] { "knife-wound-match" });

        // Bottle through the real lifecycle - supporting context only, so it must not
        // make the brother conclusion available on its own (checked below). Its
        // collection is also what reaches HYP-02's "2 items Collected" trigger.
        flow.SetEvidenceIdsForTest(new[] { "EVD-018" }, 1, true);
        RunFlow(flow, failures, report, "bottle lifecycle");
        yield return Settle();
        ConnectAvailable(relationships);
        board.SubmitTheory();
        yield return Settle();
        Check(failures, report, "[no spatter] REL-knife-consistent-wound Completed", relationships.GetRecord("REL-knife-consistent-wound")?.state == RelationshipState.Completed);
        Check(failures, report, "[no spatter] FIND-SPATTER-RECONSTRUCTION not confirmed", !FindingStateManager.Instance.IsConfirmed("FIND-SPATTER-RECONSTRUCTION"));
        Check(failures, report, "[no spatter] Paul and Peter are selectable after meaningful investigation", conclusions.IsAvailable(BrotherConclusion) && conclusions.IsAvailable(PeterConclusion));
        Check(failures, report, "[no spatter] Inconclusive available", conclusions.IsAvailable(InconclusiveConclusion));
        Check(failures, report, "[no spatter] Paul resolves to the insufficiently-supported outcome",
            EndingManager.Instance.ResolveEnding(BrotherConclusion).Ending?.endingId == "ending-premature-submission");
        Check(failures, report, "[no spatter] Peter resolves to the unsupported-accusation outcome",
            EndingManager.Instance.ResolveEnding(PeterConclusion).Ending?.endingId == "ending-peter-unsupported");
        Check(failures, report, "[no spatter] Inconclusive remains a defensible outcome",
            EndingManager.Instance.ResolveEnding(InconclusiveConclusion).Ending?.endingId == "ending-inconclusive");
        CheckSelectableSet(failures, report, "[no spatter] picker offers all incomplete-case options", BrotherConclusion, PeterConclusion, InconclusiveConclusion);

        // Obtain the independently observed timeline fact while the player can still
        // interview; discovery without the reconstruction remains insufficient.
        var interview = VendorFollowupInterview.Instance;
        bool vendorOpened = VendorFollowupTest.OpenAtVendor();
        yield return null;
        if (interview == null || !vendorOpened || !interview.AskAnyoneElse()) failures.Add("Physical vendor follow-up could not start.");
        if (interview != null) yield return VendorFollowupTest.DrainDialogue(interview);

        // --- Stage C: open the REAL picker during partial investigation ---
        RelationshipReviewTestUtil.ConfirmAvailable(); // the newly discovered timeline must be reviewed first
        MeasurementTestUtil.CompleteSceneSketch();
        STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        EndingManager.Instance.TrySubmitConclusion();
        yield return Settle(); // settle any currently triggered checkpoint before the picker
        float t = 0f;
        while (EndingManager.Instance.GetPickerOptionIdsForTest().Count == 0 && t < 3f)
        {
            AnswerOpenCheckpointCorrectly(null);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        var openedWith = EndingManager.Instance.GetPickerOptionIdsForTest();
        Check(failures, report, $"[partial picker] offers Paul, Peter, and Inconclusive (got {string.Join(",", openedWith)})",
            HasExactly(openedWith, BrotherConclusion, PeterConclusion, InconclusiveConclusion));

        // --- Stage D: brother becomes supported WHILE that picker is still open ---
        // Direct state calls (the picker holds the UI gate, so the Recorder UI can't open);
        // same public APIs the tools use. The bottle is deliberately skipped - it's
        // supporting context, never a gate.
        esm.MarkFound("EVD-015", ToolType.None);
        yield return Settle(); // HYP-01's three discovered collectible items are now present
        esm.MarkPhotographedRaw("EVD-015", ToolType.None);
        esm.MarkChalkMarked("EVD-015", ToolType.ChalkMark); // outline first: chalk is required before its marker
        esm.MarkTented("EVD-015", ToolType.EvidenceMarker, 3);
        esm.MarkPhotographedFinal("EVD-015", ToolType.None);
        // Blood sample before the record (EVD-015 requiresSwabbing): swab, then seal it.
        MeasurementTestUtil.CompleteSketchFor("EVD-015");
        Apply(failures, report, "spatter sampled", esm.MarkSwabbed("EVD-015", ToolType.Swab));
        Apply(failures, report, "spatter sample sealed", esm.MarkSwabSampleSealed("EVD-015", ToolType.Swab));
        esm.RecordReportLinesSelected("EVD-015", new[] { "blood-spatter-location", "blood-spatter-droplet-pattern", "blood-spatter-mechanism-only" });
        MeasurementTestUtil.CompleteDiscoveredMeasurements(); // records wait for the discovered evidence's measurements
        Apply(failures, report, "spatter logged", esm.MarkLogged("EVD-015", ToolType.None));
        yield return Settle(); // HYP-03 triggers after processing, not on earlier submission
        ConnectAvailable(relationships);
        relationships.CompleteAllSelected();
        yield return null;
        Check(failures, report, "[full] REL-spatter-consistent-reconstruction Completed", relationships.GetRecord("REL-spatter-consistent-reconstruction")?.state == RelationshipState.Completed);
        Check(failures, report, "[full] REL-paul-print-handled-knife Completed", relationships.GetRecord("REL-paul-print-handled-knife")?.state == RelationshipState.Completed);
        Check(failures, report, "[full] discovered vendor statement and timeline contradiction Completed",
            interview != null && interview.IsDiscovered
            && relationships.GetRecord(VendorFollowupInterview.RelationshipId)?.state == RelationshipState.Completed);
        Check(failures, report, "[full] brother conclusion AVAILABLE", conclusions.IsAvailable(BrotherConclusion));
        Check(failures, report, "[full] Inconclusive still internally available (monotonic state untouched)", conclusions.IsAvailable(InconclusiveConclusion));
        CheckSelectableSet(failures, report, "[full] picker hides Inconclusive but retains Paul and Peter", BrotherConclusion, PeterConclusion);
        Check(failures, report, "[full] conclusion-brother resolves ending-brother-accused",
            EndingManager.Instance.ResolveEnding(BrotherConclusion).Ending?.endingId == "ending-brother-accused");

        // Stale selection: Inconclusive was on screen when the picker opened.
        bool staleAccepted = EndingManager.Instance.SelectConclusionForTest(InconclusiveConclusion);
        Check(failures, report, "[stale] confirming Inconclusive after brother became supported is refused", !staleAccepted);
        var refreshed = EndingManager.Instance.GetPickerOptionIdsForTest();
        Check(failures, report, $"[stale] picker stays open and refreshes without Inconclusive (got {string.Join(",", refreshed)})",
            HasExactly(refreshed, BrotherConclusion, PeterConclusion));
        var completionCheck = FindFirstObjectByType<CompletionScreenUI>();
        Check(failures, report, "[stale] nothing submitted - no completion screen", completionCheck == null || !completionCheck.IsShowing);

        // --- Stage E: submit the brother conclusion through the refreshed picker ---
        RelationshipReviewTestUtil.EnableAssessmentCapture();
        bool selected = EndingManager.Instance.SelectConclusionForTest(BrotherConclusion);
        Check(failures, report, "[full] brother conclusion selectable in the refreshed picker", selected);

        var completion = FindFirstObjectByType<CompletionScreenUI>();
        t = 0f;
        while (completion != null && !completion.IsShowing && t < 3f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Check(failures, report, "[full] completion screen shown", completion != null && completion.IsShowing);

        // --- HYP answers + logged ids ---
        Check(failures, report, "All six earned contextual checkpoint responses remain independent assessment records",
            ReasoningCheckpointManager.Instance.Records.Count == 6);
        Check(failures, report, "RC-03 fingerprint response is locked before the vendor lead",
            ReasoningCheckpointManager.Instance.Records.Any(r => r.CheckpointId=="RC-03-FINGERPRINT" && r.ResponseId=="RC-03-FINGERPRINT-C"));
        Check(failures, report, "All six relationship interpretations recorded without board connections",
            EvidenceRelationshipsReview.Instance.Responses.Count==6 && board.GetAssessmentConnections().Count==0);
        Check(failures, report, "HYP-01, HYP-02 and HYP-03 each define exactly one correct option", ExactlyOneCorrect("HYP-01") && ExactlyOneCorrect("HYP-02") && ExactlyOneCorrect("HYP-03"));

        GreyboxFlowTest.CheckpointAnswererForTest = null;
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" });

        // SessionLogger buffers (every 10 events / 30 s) - flush before reading.
        SessionLogger.Instance?.Flush();
        string log = ReadSessionLog();
        Check(failures, report, "session log records the RC-03 interpretation without a fabricated legacy justification", log.Contains("RC-03-FINGERPRINT-C")
            && !log.Contains("hyp02-print-needs-reconstruction"));
        Check(failures, report, "session log records separate locked relationship interpretations", log.Contains("RelationshipInterpretationSubmitted"));
        Check(failures, report, "EndingSelected logged ending-brother-accused", log.Contains("\"EndingSelected\"") && log.Contains("ending-brother-accused"));
        Check(failures, report, "stale Inconclusive confirm logged as ConclusionSubmissionBlocked", log.Contains("conclusion no longer selectable"));
        Check(failures, report, "ConclusionSelected never logged for Inconclusive", !log.Contains("\"eventType\":\"ConclusionSelected\",\"targetId\":\"" + InconclusiveConclusion + "\""));
        foreach (var id in RetiredIds)
        {
            Check(failures, report, $"session log contains no retired id {id}", !log.Contains("\"" + id + "\""));
        }

        if (completion != null && completion.IsShowing)
        {
            completion.DismissForTest();
        }

        Check(failures, report, "[closeout] conclusion feedback acknowledgement accepted", EndingManager.Instance.AcceptConclusionFeedback());

        // Final upload waits for the real closeout workflow, not just the conclusion.
        string thisSession = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
        Check(failures, report, "[isolation] accepted conclusion waits for closeout before uploading",
            SceneTurnover.Phase == ScenarioPhase.SOCOCloseout && AutomatedTestSandbox.Queue != null
            && !AutomatedTestSandbox.Queue.LoadIndex().entries.Exists(e => e.sessionId == thisSession));
        var uploadConfig = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>(); uploadConfig.uploadRawEventLogEnabled = false;
        var scenario = Resources.FindObjectsOfTypeAll<ScenarioDefinition>().First(s => s.scenarioId == FinalDeductionAssessment.Rules.scenarioId);
        var queued = AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath, uploadConfig, scenario, BrotherConclusion, "ending-brother-accused");
        Destroy(uploadConfig);
        Check(failures, report, "[persistence] session payload retains the confirmed timeline interpretation in the existing schema",
            queued != null && queued.payloadVersion == 1 && queued.orderedEvents.Exists(e =>
                e.eventType == "RelationshipInterpretationSubmitted" && e.targetId == VendorFollowupInterview.RelationshipId));
        Check(failures, report, "[persistence] vendor question/discovery logged once before session completion",
            log.Contains(VendorFollowupInterview.StatementId)
            && SessionLogger.Instance.EventCounts.TryGetValue(SessionEventType.WitnessFollowupQuestionSelected, out int questions) && questions == 1
            && SessionLogger.Instance.EventCounts.TryGetValue(SessionEventType.WitnessStatementDiscovered, out int statements) && statements == 1);
        Check(failures, report, "[isolation] real upload queue and real session owners unchanged",
            AutomatedTestSandbox.RealQueueEntryCount() == realQueueBefore && AutomatedTestSandbox.RealOwnerRecordCount() == realOwnersBefore);

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // Answers whatever hypothesis checkpoint is open with its correct reasoning option -
    // located among the options actually visible right now, never by a fixed index.
    private IEnumerator Settle()
    {
        RelationshipReviewTestUtil.RecordEarnedReasoning();
        for (int i = 0; i < 3; i++)
        {
            yield return null;
        }
    }

    private void AnswerOpenCheckpointCorrectly(StringBuilder report)
    {
        var ui = FindFirstObjectByType<HypothesisCheckpointUI>();
        if (ui == null || !ui.IsShowing)
        {
            return;
        }

        var current = typeof(HypothesisCheckpointUI).GetField("current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ui) as HypothesisCheckpoint;
        if (current == null)
        {
            return;
        }

        var snapshot = typeof(HypothesisCheckpointUI).GetField("snapshotDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(ui) as ReasoningCheckpointDefinition;
        if (snapshot != null)
        {
            if (!snapshot.id.StartsWith("RC-", System.StringComparison.Ordinal)) return;
            int strongest = snapshot.responses.FindIndex(r => r.weight == snapshot.responses.Max(c => c.weight));
            answeredReasoning[string.IsNullOrEmpty(snapshot.integratedHypothesisId) ? snapshot.id : snapshot.integratedHypothesisId] = snapshot.responses[strongest].id;
            ui.SelectResponse(strongest); ui.ConfirmSelection();
            return;
        }

        string theoryId = current.id == "HYP-01" ? "theory-undetermined" : "theory-homicide";
        int hypothesis = current.options.FindIndex(o => o.id == theoryId);
        if (hypothesis < 0) return;

        // Same filter HypothesisCheckpointUI.EnterReasoningStage applies, so the index
        // is the correct option's position among what the player would actually see.
        var visible = new List<ReasoningOption>();
        if (current.reasoningOptions != null)
        {
            foreach (var option in current.reasoningOptions)
            {
                if (option != null && SelectionPrerequisite.AllSatisfied(option.prerequisites))
                {
                    visible.Add(option);
                }
            }
        }
        int correct = Mathf.Max(0, visible.FindIndex(o => o.isCorrect));
        if (visible.Count > correct)
        {
            answeredReasoning[current.id] = visible[correct].id;
            report?.AppendLine($"  ({current.id}: correct option is #{correct + 1} of {visible.Count} visible)");
        }
        ui.AnswerForTest(hypothesis, correct);
    }

    private static void CheckSelectable(List<string> failures, StringBuilder report, string label, string expectedOnlyId)
    {
        var ids = new List<string>();
        foreach (var def in EndingManager.Instance.GetSelectableCaseConclusions())
        {
            ids.Add(def.conclusionId);
        }
        Check(failures, report, $"{label} (got {string.Join(",", ids)})", ids.Count == 1 && ids[0] == expectedOnlyId);
    }

    private static void CheckSelectableSet(List<string> failures, StringBuilder report, string label, params string[] expectedIds)
    {
        var ids = new List<string>();
        foreach (var def in EndingManager.Instance.GetSelectableCaseConclusions()) ids.Add(def.conclusionId);
        Check(failures, report, $"{label} (got {string.Join(",", ids)})", HasExactly(ids, expectedIds));
    }

    private static bool HasExactly(List<string> actual, params string[] expected)
    {
        return actual.Count == expected.Length && actual.TrueForAll(id => System.Array.IndexOf(expected, id) >= 0);
    }

    private static void ConnectAvailable(RelationshipStateManager relationships)
    {
        RelationshipReviewTestUtil.ConfirmAvailable();
    }

    private static bool ExactlyOneCorrect(string checkpointId)
    {
        var manager = HypothesisCheckpointManager.Instance;
        var set = typeof(HypothesisCheckpointManager).GetField("checkpointSet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(manager) as HypothesisCheckpointSet;
        var cp = set?.checkpoints.Find(c => c.id == checkpointId);
        return cp != null && cp.reasoningOptions.FindAll(o => o.isCorrect).Count == 1;
    }

    private static string ReadSessionLog()
    {
        string path = SessionLogger.Instance != null ? SessionLogger.Instance.ActiveLogFilePath : null;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return string.Empty;
        }
        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
        using var reader = new System.IO.StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static void RunFlow(GreyboxFlowTest flow, List<string> failures, StringBuilder report, string label)
    {
        string flowReport = flow.RunFullFlow();
        bool ok = flowReport.Contains("=== PASS");
        Check(failures, report, label, ok);
        if (!ok)
        {
            report.AppendLine(flowReport);
        }
    }

    private static void Apply(List<string> failures, StringBuilder report, string label, TransitionResult result)
    {
        Check(failures, report, label, result == TransitionResult.Applied);
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
