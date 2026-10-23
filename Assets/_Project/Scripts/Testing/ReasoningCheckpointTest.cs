using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Focused trigger/UI/persistence checks using state fixtures, not a playthrough.</summary>
public sealed class ReasoningCheckpointTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== ReasoningCheckpointTest ===\n");
    private int checks, failures;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private void Check(string label, bool pass)
    { checks++; if (!pass) failures++; report.AppendLine((pass ? "PASS: " : "FAIL: ") + label); }
    private static void ClearGuidance()
    { STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip(); NotificationManager.Instance.UI.HidePrompt(); }
    private static IEnumerator WaitForPanel(HypothesisCheckpointUI ui)
    {
        // Presentation is deliberately deferred, so wait for readiness rather
        // than assuming two frames on a heavily loaded desktop editor.
        double deadline=Time.realtimeSinceStartupAsDouble+8d;
        while (!ui.IsShowing && Time.realtimeSinceStartupAsDouble<deadline)
        { ClearGuidance(); yield return null; }
    }
    private static IEnumerator WaitForTraining(string source)
    {
        double deadline=Time.realtimeSinceStartupAsDouble+8d;
        while (!STCSNotificationUI.Instance.HasLinesFrom(source) && Time.realtimeSinceStartupAsDouble<deadline)
        { ClearGuidance(); yield return null; }
    }
    private IEnumerator Run()
    {
        yield return null;
        var manager = ReasoningCheckpointManager.Instance;
        var ui = FindAnyObjectByType<HypothesisCheckpointUI>();
        var hyp = HypothesisCheckpointManager.Instance;
        hyp.enabled = false;
        ProceduralTrainingGuidance.Instance.enabled = false;
        Check("Bootstrap uses existing choice UI and six authored checkpoints", manager != null && ui != null && manager.Definitions.checkpoints.Count == 6);
        if (manager == null || ui == null) { Finish(); yield break; }
        Check("Authored IDs, categories, response IDs and weights validate", manager.Definitions.IsValid(out _));
        int[][] weights = { new[]{1,3,4,2}, new[]{1,4,1,3}, new[]{1,3,4,2}, new[]{1,4,3,1}, new[]{1,4,3,2}, new[]{1,4,3,2} };
        for (int n = 0; n < 6; n++)
        {
            var d = manager.Definitions.checkpoints[n];
            Check(d.id + " has exact authored A-D stable IDs/weights", d.responses.Select(r=>r.weight).SequenceEqual(weights[n])
                && d.responses.Select(r=>r.id).SequenceEqual("ABCD".Select(c=>d.id+"-"+c)));
        }
        Check("Peter checkpoint not eligible before Form 1", !manager.IsEligible("RC-01-PETER") && !SceneTurnover.Form1Received);
        Check("All later checkpoints initially ineligible", manager.Definitions.checkpoints.Skip(1).All(c=>!manager.IsEligible(c.id)));
        Check("Fingerprint training held even before identification", manager.HoldsTraining("TRAIN-FINGERPRINT"));
        ProceduralTrainingGuidance.Instance.enabled = true;
        ClearGuidance();
        ProceduralTrainingGuidance.Schedule("TRAIN-FINGERPRINT");
        ProceduralTrainingGuidance.Schedule("TRAIN-MARKER");
        yield return null; yield return null;
        Check("Actual training queue withholds fingerprint lesson without starving unrelated marker lesson",
            !STCSNotificationUI.Instance.HasLinesFrom("TRAIN-FINGERPRINT") && STCSNotificationUI.Instance.HasLinesFrom("TRAIN-MARKER"));
        ProceduralTrainingGuidance.Instance.enabled = false; ClearGuidance();
        var history = DialogueHistoryManager.Instance;
        GameDialogue.SayLine("narr-mission-knife-print-result-1", "Professor", "phase3-test-print-lesson");
        Check("Potentially revealing fingerprint narrative is withheld from UI/history", !STCSNotificationUI.Instance.HasLinesFrom("phase3-test-print-lesson")
            && !history.Entries.Any(e=>e.sourceId=="phase3-test-print-lesson"));
        SceneTurnover.Controller.CompleteWithoutDialogue();
        Check("Form 1 makes Peter checkpoint eligible", manager.IsEligible("RC-01-PETER"));
        Check("Scene interpretation still waits for observations", !manager.IsEligible("RC-02-SCENE"));
        ClearGuidance();
        PlayerUIGate.Enter(this);
        yield return new WaitForSecondsRealtime(.8f);
        Check("Eligible checkpoint queues behind existing modal", !ui.IsShowing && manager.IsEligible("RC-01-PETER") && !manager.IsAnswered("RC-01-PETER"));
        PlayerUIGate.Exit(this);
        GameDialogue.SayLine("FR_TURNOVER_01", "First Responder", "phase3-test-active-dialogue");
        yield return new WaitForSecondsRealtime(.1f);
        Check("Active dialogue prevents checkpoint overlap", !ui.IsShowing && !manager.CanPresent());
        ClearGuidance();
        Transform rig = Camera.main.GetComponentInParent<OVRCameraRig>() != null
            ? Camera.main.GetComponentInParent<OVRCameraRig>().transform : Camera.main.transform;
        Vector3 originalPosition = rig.position;
        for (int step=0; step<30; step++) { rig.position += Vector3.right*.02f; yield return new WaitForSecondsRealtime(.03f); }
        Check("Cumulative slow repositioning also postpones the question", !ui.IsShowing && !manager.CanPresent());
        rig.position = originalPosition;
        yield return new WaitForSecondsRealtime(.8f);
        Check("Peter prompt appears once after dialogue clears", ui.IsShowing && ui.DisplayedPrompt.Contains(manager.Definitions.checkpoints[0].prompt));
        Check("Opening/highlight alone does not submit", manager.Records.Count == 0 && ui.DisplayedHint.Contains("Select"));
        string display = ui.DisplayedPrompt + ui.DisplayedHint + string.Join(" ", ui.DisplayedOptions.Select(t=>t.text));
        Check("Player content has no weights, points or correctness feedback", !System.Text.RegularExpressions.Regex.IsMatch(System.Text.RegularExpressions.Regex.Replace(display, @"Reasoning Question \d+", "Reasoning Question"), @"\b(weight|points|correct!|incorrect!|[1-4])\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        ui.ConfirmSelection(); // first A selects, second A confirms
        Check("First A only selects and exposes confirmation", manager.Records.Count == 0 && ui.IsShowing && ui.DisplayedHint.Contains("Confirm Answer"));
        ui.SelectResponse(2); ui.SelectResponse(0); ui.ConfirmSelection();
        Check("Explicit confirmation records the final selected A response once", manager.Records.Count == 1
            && manager.Records[0].ResponseId == "RC-01-PETER-A" && manager.Records[0].Weight == 1 && !ui.IsShowing);
        Check("Submitted response cannot be replaced with higher-weight answer", !manager.RecordResponse("RC-01-PETER", "RC-01-PETER-C") && manager.Records[0].Weight == 1);
        ui.ShowReasoning(manager.Definitions.checkpoints[0], _=>{});
        Check("Same-session reopening cannot replay answered checkpoint", !ui.IsShowing && manager.Records.Count == 1);
        Check("Unknown response ID cannot create a record", !manager.RecordResponse("RC-02-SCENE", "not-authored"));
        manager.enabled = false; // Fixtures below explicitly drive the same eligibility evaluator.
        foreach (string id in new[]{"ioc-victim","ioc-possible-weapon","ioc-main-scene-survey"}) SceneTurnover.RecordIOCObservation(id);
        manager.Evaluate();
        Check("Scene checkpoint requires blood observation as well as victim, knife and survey", !manager.IsEligible("RC-02-SCENE"));
        SceneTurnover.RecordIOCObservation("ioc-bloodstain-area"); manager.Evaluate();
        Check("Actual IOC observation IDs unlock scene checkpoint before forensic findings", manager.IsEligible("RC-02-SCENE")
            && !FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER"));
        string evidenceBefore = EvidenceSnapshot(); string relationshipsBefore = RelationshipSnapshot();
        var board = FindAnyObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        var scorer = FindAnyObjectByType<DeductionScorer>(FindObjectsInactive.Include);
        float scoreBefore = scorer.Score(board).total;
        bool endingBefore = EndingManager.Instance.ResolveEnding("conclusion-brother").Ending?.endingId == "ending-premature-submission";
        ui.ShowReasoning(manager.Definitions.checkpoints[1], id=>manager.RecordResponse("RC-02-SCENE",id));
        ui.SelectResponse(1); ui.ConfirmSelection();
        Check("Scene interpretation records independently of HYP-01", manager.IsAnswered("RC-02-SCENE")
            && !SessionLogger.Instance.EventCounts.ContainsKey(SessionEventType.HypothesisSubmitted));
        Check("Snapshot leaves EvidenceStatus, board relationships, score and conclusion unchanged", evidenceBefore == EvidenceSnapshot()
            && relationshipsBefore == RelationshipSnapshot() && Mathf.Approximately(scoreBefore,scorer.Score(board).total)
            && endingBefore == (EndingManager.Instance.ResolveEnding("conclusion-brother").Ending?.endingId == "ending-premature-submission"));
        Check("Responses/options do not enter Dialogue History or Witness Accounts", !history.Entries.Any(e=>e.sourceId.StartsWith("RC-"))
            && !FindAnyObjectByType<WitnessAccountsUI>().GetKnownAccounts().Any(a=>a.CatalogId.StartsWith("RC-")));
        SceneTurnover.CompleteIOCAssessment(); SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested);
        manager.enabled = true; ClearGuidance();
        Check("SOCO request/turnover blocks presentation", !manager.CanPresent());
        SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover); SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation);
        var esm = EvidenceStateManager.Instance;
        // Arrange only the lab-match prerequisite; finding confirmation uses the production evaluator.
        esm.GetRecord("EVD-014").fingerprintLabStatus = FingerprintLabStatus.SentToLab;
        esm.MarkMatched("EVD-014");
        FindingStateManager.Instance.RefreshFindings(); manager.Evaluate();
        Check("Paul fingerprint checkpoint requires the actual confirmed identity finding", manager.IsEligible("RC-03-FINGERPRINT")
            && FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER"));
        PlayerUIGate.Enter(this); hyp.enabled = true;
        ClearGuidance(); yield return new WaitForSecondsRealtime(.8f);
        Check("Integrated HYP-02 also queues behind a modal without releasing lesson", !ui.IsShowing && manager.HoldsTraining("TRAIN-FINGERPRINT"));
        PlayerUIGate.Exit(this);
        // A physically equipped tool must also postpone the question.
        PlayerToolRegistry.ToggleEquip(ToolType.Photographer, Camera.main.transform);
        yield return new WaitForSecondsRealtime(.15f);
        Check("Held/virtually equipped tool defers eligible checkpoint", !ui.IsShowing && !manager.CanPresent());
        PlayerToolRegistry.HolsterCurrent(); ClearGuidance();
        yield return new WaitForSecondsRealtime(.8f);
        Check("Fingerprint appears as HYP-02's single contextual first question", ui.IsShowing && ui.DisplayedPrompt.Contains(manager.Definitions.checkpoints[2].prompt)
            && ui.DisplayedPrompt.Contains("Reasoning Question 3") && !ui.DisplayedPrompt.Contains("HYP-"));
        ui.SelectResponse(2); ui.ConfirmSelection();
        Check("One confirmed fingerprint answer records the snapshot and closes the panel", manager.IsAnswered("RC-03-FINGERPRINT")
            && !ui.IsShowing && manager.Records.Last().Weight == 4);
        Check("No duplicate legacy justification question appears", !ui.DisplayedPrompt.Contains("What most supports") && !ui.IsShowing);
        Check("Fingerprint training releases only after response is recorded", !manager.HoldsTraining("TRAIN-FINGERPRINT"));
        yield return null;
        Check("HYP-02 completion is emitted exactly once for the single explicit response", SessionLogger.Instance.EventCounts.TryGetValue(SessionEventType.HypothesisSubmitted,out int hyps) && hyps==1);
        SessionLogger.Instance.Flush();
        var fingerprintCompletion = SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath)
            .Where(e=>e.eventType=="HypothesisSubmitted"&&e.targetId=="HYP-02").ToArray();
        Check("Completion links the contextual response without inventing a second answer", fingerprintCompletion.Length==1
            && SessionLogReader.PayloadValue(fingerprintCompletion[0],"reasoningResponseId")=="RC-03-FINGERPRINT-C"
            && string.IsNullOrEmpty(SessionLogReader.PayloadValue(fingerprintCompletion[0],"reasoningOptionId")));
        manager.Evaluate(); yield return null;
        Check("Reevaluation cannot reopen the answered fingerprint question", !ui.IsShowing
            && manager.Records.Count(r=>r.CheckpointId=="RC-03-FINGERPRINT")==1);
        Check("Withheld explanation now reaches existing dialogue channel/history", STCSNotificationUI.Instance.HasLinesFrom("phase3-test-print-lesson")
            || history.Entries.Any(e=>e.sourceId=="phase3-test-print-lesson"));
        hyp.enabled = false; manager.enabled = false; ClearGuidance();
        ProceduralTrainingGuidance.Instance.enabled = true;
        yield return WaitForTraining("TRAIN-FINGERPRINT");
        Check("Held Phase 2 fingerprint lesson is actually presented after submission", STCSNotificationUI.Instance.HasLinesFrom("TRAIN-FINGERPRINT")
            && history.Entries.Count(e=>e.sourceId=="TRAIN-FINGERPRINT")==1);
        ClearGuidance(); ProceduralTrainingGuidance.Schedule("TRAIN-FINGERPRINT"); yield return null;
        Check("Fingerprint teaching remains once per session after releasing the hold", !STCSNotificationUI.Instance.HasLinesFrom("TRAIN-FINGERPRINT")
            && history.Entries.Count(e=>e.sourceId=="TRAIN-FINGERPRINT")==1);
        ProceduralTrainingGuidance.Instance.enabled = false; ClearGuidance();
        esm.RecordReportLinesSelected("BODY-A",new[]{"body-a-wound-sharp-object"});
        esm.RecordReportLinesSelected("EVD-014",new[]{"knife-wound-match"}); manager.Evaluate();
        Check("Knife/wound checkpoint requires authored consistency finding", manager.IsEligible("RC-04-KNIFE-WOUND")
            && FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-WOUND-CONSISTENT"));
        manager.RecordResponse("RC-04-KNIFE-WOUND","RC-04-KNIFE-WOUND-B");
        Check("Catalog presence alone cannot unlock Vendor timeline checkpoint", ViCSIDialogueCatalog.Current.Find("vendor-paul-3am-summary")!=null
            && !manager.IsEligible("RC-05-TIMELINE"));
        ClearGuidance(); bool opened = VendorFollowupTest.OpenAtVendor(); yield return null;
        bool asked = VendorFollowupInterview.Instance.AskAnyoneElse();
        Check("Physical Vendor interaction starts answer before discovery/eligibility", opened && asked && !manager.IsEligible("RC-05-TIMELINE"));
        yield return VendorFollowupTest.DrainDialogue(VendorFollowupInterview.Instance);
        manager.Evaluate();
        Check("Completed physical Vendor statement unlocks timeline checkpoint", VendorFollowupInterview.Instance.HasDiscovered("statement_vendor_paul_3am")
            && manager.IsEligible("RC-05-TIMELINE"));
        manager.RecordResponse("RC-05-TIMELINE","RC-05-TIMELINE-B");
        Check("Strong timeline response does not create deduction-board relationship", RelationshipStateManager.Instance.GetRecord("REL-paul-timeline-contradiction").state != RelationshipState.Completed);
        esm.GetRecord("EVD-015").swabbingDone = true; manager.Evaluate();
        Check("Collecting blood alone cannot unlock reconstruction checkpoint", !manager.IsEligible("RC-06-BLOODSTAIN"));
        esm.RecordReportLinesSelected("EVD-015",new[]{"blood-spatter-mechanism-only"}); manager.Evaluate();
        Check("Actual reconstruction report/finding unlocks bloodstain checkpoint", manager.IsEligible("RC-06-BLOODSTAIN")
            && FindingStateManager.Instance.IsConfirmed("FIND-SPATTER-RECONSTRUCTION"));
        manager.RecordResponse("RC-06-BLOODSTAIN","RC-06-BLOODSTAIN-C");
        Check("Chronology retains early assumption and later stronger reasoning", manager.Records.Count==6 && manager.Records.Select(r=>r.Order).SequenceEqual(Enumerable.Range(1,6))
            && manager.Records.First().Weight==1 && manager.Records.Last().Weight==3
            && manager.Records.All(r=>r.MaximumWeight==4 && r.AnsweredSeconds>=r.EligibleSeconds));
        SessionLogger.Instance.Flush();
        var events = SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath);
        var records = events.Where(e=>e.eventType=="ReasoningCheckpointSubmitted").ToList();
        Check("Eligibility is durably logged once per checkpoint, including its possible weight", events.Count(e=>e.eventType=="ReasoningCheckpointEligible")==6
            && events.Where(e=>e.eventType=="ReasoningCheckpointEligible").All(e=>SessionLogReader.PayloadValue(e,"maximumWeight")=="4"));
        Check("JSONL round-trip preserves six IDs, categories, weights, maxima and timestamps", records.Count==6
            && records.Select(e=>e.targetId).SequenceEqual(manager.Records.Select(r=>r.CheckpointId))
            && records.All(e=>SessionLogReader.PayloadValue(e,"maximumWeight")=="4" && SessionLogReader.PayloadValue(e,"reasoningCategory")!=null
                && SessionLogReader.PayloadValue(e,"responseId")!=null && SessionLogReader.PayloadValue(e,"answeredSeconds")!=null));
        var config = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>(); config.uploadRawEventLogEnabled=true; config.maxEventsPerRequest=5000;
        var scenario = Resources.Load<ScenarioDefinition>("unused");
        var dto = AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,"","");
        Check("Unchanged payload generic orderedEvents can carry separate response records", dto.payloadVersion==1
            && dto.orderedEvents.Count(e=>e.eventType=="ReasoningCheckpointSubmitted")==6);
        Destroy(config);
        var hypSet = (HypothesisCheckpointSet)typeof(HypothesisCheckpointManager)
            .GetField("checkpointSet",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hyp);
        // Arrange the actual authored count trigger, not the reserved submission API.
        // This fixture changes status only to test the existing HYP observer; it does
        // not run or weaken any physical procedure or select a conclusion.
        foreach (string id in new[]{"EVD-018","EVD-014","EVD-015"}) esm.GetRecord(id).status=EvidenceStatus.Processed;
        ClearGuidance(); manager.enabled=true; hyp.enabled=true;
        yield return WaitForPanel(ui);
        Check("HYP-01 still asks its unchanged broad working-theory question", ui.IsShowing
            && ui.DisplayedPrompt.Contains("Reasoning Question 1") && !ui.DisplayedPrompt.Contains("HYP-")
            && ui.DisplayedPrompt.EndsWith(hypSet.checkpoints.Find(c=>c.id=="HYP-01").promptText));
        ui.AnswerForTest();
        yield return WaitForPanel(ui);
        Check("Existing HYP-03 fires at its authored three-Processed threshold", ui.IsShowing
            && ui.DisplayedPrompt.Contains("Reasoning Question 3") && !ui.DisplayedPrompt.Contains("HYP-")
            && ui.DisplayedPrompt.EndsWith(hypSet.checkpoints.Find(c=>c.id=="HYP-03").promptText));
        ui.AnswerForTest();
        Check("HYP-03 still submits original justification without changing checkpoint stream or conclusion", manager.Records.Count==6
            && SessionLogger.Instance.EventCounts[SessionEventType.HypothesisSubmitted]==3 && EndingManager.Instance.AcceptedEnding==null);
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path); yield return null;
        manager = ReasoningCheckpointManager.Instance;
        Check("Fresh scene/session clears answers, chronology, eligibility and weights", manager.Records.Count==0
            && manager.Definitions.checkpoints.All(c=>!manager.IsAnswered(c.id) && !manager.IsEligible(c.id)));
        Check("Fresh session holds fingerprint teaching again", manager.HoldsTraining("TRAIN-FINGERPRINT"));
        SceneTurnover.Controller.CompleteWithoutDialogue();
        Check("New session can trigger Peter snapshot again", manager.IsEligible("RC-01-PETER") && !manager.IsAnswered("RC-01-PETER"));
        Finish();
    }
    private static string EvidenceSnapshot() => string.Join("|",EvidenceStateManager.Instance.AllRecords.OrderBy(r=>r.definition.evidenceId).Select(r=>r.definition.evidenceId+":"+r.status));
    private static string RelationshipSnapshot() => string.Join("|",RelationshipStateManager.Instance.AllRecords.OrderBy(r=>r.definition.relationshipId).Select(r=>r.definition.relationshipId+":"+r.state));
    private void Finish() { report.AppendLine($"=== {(failures==0?"PASS":"FAIL")}: {checks} focused checks, {failures} failures ==="); Report=report.ToString(); IsDone=true; }
}
