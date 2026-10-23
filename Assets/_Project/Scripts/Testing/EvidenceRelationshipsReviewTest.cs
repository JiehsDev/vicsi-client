using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EvidenceRelationshipsReviewTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== EvidenceRelationshipsReviewTest ===\n");
    private int failures;
    private void Check(string label, bool ok) { report.AppendLine((ok?"PASS: ":"FAIL: ")+label); if(!ok)failures++; }
    public void RunAllTests() { AutomatedTestSandbox.Activate(nameof(EvidenceRelationshipsReviewTest)); DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private IEnumerator Run()
    {
        yield return null;
        ReasoningCheckpointManager.Instance.enabled = false;
        HypothesisCheckpointManager.Instance.enabled = false;
        ProceduralTrainingGuidance.Instance.enabled = false;
        ScenePhotographyTestUtil.CompleteRoute();
        var review = EvidenceRelationshipsReview.Instance;
        var board = FindAnyObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        var state = EvidenceStateManager.Instance;
        Check("Old board canvas remains inactive and creates no connections", !board.gameObject.activeSelf && board.GetAssessmentConnections().Count==0);
        Check("Timeline interpretation cannot be answered before real vendor discovery", !review.ConfirmResponse(VendorFollowupInterview.RelationshipId,
            FinalDeductionAssessment.Rules.relationships.Find(r=>r.id==VendorFollowupInterview.RelationshipId).correctInterpretationId));
        foreach(var id in new[]{"BODY-A","EVD-014","EVD-015","EVD-018"}) state.MarkFound(id,ToolType.None);
        state.RecordReportLinesSelected("BODY-A",new[]{"body-a-wound-sharp-object"});
        state.RecordReportLinesSelected("EVD-014",new[]{"knife-wound-match"});
        state.RecordReportLinesSelected("EVD-015",new[]{"blood-spatter-mechanism-only"});
        state.GetRecord("EVD-014").fingerprintLabStatus=FingerprintLabStatus.Matched;
        FindingStateManager.Instance.RefreshFindings();
        var originalReasoningCount=ReasoningCheckpointManager.Instance.Records.Count;
        var questions=review.EligibleQuestions();
        Check("Earned physical/context questions available; timeline still withheld", questions.Count==5 && questions.All(r=>r.id!=VendorFollowupInterview.RelationshipId));
        Check("All six existing IDs have four unique interpretations and one authored key", FinalDeductionAssessment.Rules.relationships.Count==6
            && FinalDeductionAssessment.Rules.relationships.All(r=>r.interpretations.Count==4 && r.interpretations.Select(c=>c.id).Distinct().Count()==4
                && r.interpretations.Count(c=>c.id==r.correctInterpretationId)==1 && r.interpretations.All(c=>!string.IsNullOrWhiteSpace(c.text))));
        STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip();
        HeldItemCoordinator.Instance.HolsterCurrentTool();
        board.Show(); yield return null;
        Check("Existing menu adapter opens a compact review instead of the old board", review.IsShowing && !board.gameObject.activeSelf
            && review.QuestionUI != null && review.QuestionUI.DisplayedPrompt.Contains("EVIDENCE RELATIONSHIPS REVIEW"));
        if (!review.IsShowing) { Report=report.AppendLine("=== FAIL: review did not open ===").ToString(); IsDone=true; yield break; }
        var ui=review.QuestionUI;
        ui.ConfirmSelection();
        Check("First confirmation only selects; it cannot record an answer", review.Responses.Count==0 && review.IsShowing);
        review.Hide();
        Check("Closing an unconfirmed question releases UI ownership without recording", !review.IsShowing && !PlayerUIGate.IsBlocked && review.Responses.Count==0);
        review.Show(); yield return null;
        Check("Reopening resumes the unanswered question",review.IsShowing && review.ActiveRelationshipId==questions[0].id);
        foreach(var question in questions)
        {
            ui=review.QuestionUI;
            Check(question.id+": visible four-choice question", review.ActiveRelationshipId==question.id && ui.DisplayedOptions.Count==4);
            Canvas.ForceUpdateCanvases(); ui.ResponsiveLayout.Refresh();
            Check(question.id+": readable padding and measured answer heights",ui.DisplayedOptions.All(label=>{
                var fit=label.GetComponentInParent<ResponsiveTextOption>();
                return fit!=null && fit.HorizontalPadding>=20 && fit.VerticalPadding>=16 && fit.RequiredHeight>=64
                    && label.GetPreferredValues(label.text,label.rectTransform.rect.width-label.margin.x-label.margin.z,Mathf.Infinity).y
                        <= label.rectTransform.rect.height + 1;
            }));
            Check(question.id+": no correctness feedback before submission", !ui.DisplayedHint.ToLowerInvariant().Contains("correct")
                && !ui.DisplayedPrompt.Contains("score") && ui.DisplayedOptions.All(label=>!label.text.Contains("Correct answer")));
            bool chooseUnsupported = question.id == "REL-peter-departure-supported";
            ui.SelectResponse(question.interpretations.FindIndex(c=>chooseUnsupported ? c.id!=question.correctInterpretationId : c.id==question.correctInterpretationId));
            ui.ConfirmSelection();
            Check(question.id+": confirmed response locked", review.HasResponse(question.id)
                && !review.ConfirmResponse(question.id,question.interpretations.First(c=>c.id!=question.correctInterpretationId).id));
            if (chooseUnsupported)
            {
                RelationshipStateManager.Instance.CompleteAllSelected();
                Check("Incorrect confirmed interpretation is locked without manufacturing conclusion support",
                    RelationshipStateManager.Instance.GetRecord(question.id).state==RelationshipState.Selected);
            }
            yield return null;
        }
        Check("Review completes without node manipulation and without new checkpoint answers", review.IsReadyForConclusion
            && EvidenceBoardController.HasEverSubmittedTheory && board.GetAssessmentConnections().Count==0
            && ReasoningCheckpointManager.Instance.Records.Count==originalReasoningCount);
        review.Show(); Check("Reopening cannot change locked responses",!review.IsShowing && review.Responses.Count==5);
        var snapshot=FinalDeductionAssessment.CaptureLive();
        var exposed=review.Responses[0]; var lockedId=exposed.responseId;exposed.responseId="tampered";
        Check("Read-only response views cannot mutate the locked session record",review.Responses[0].responseId==lockedId);
        Check("Session snapshot contains locked IDs, order and review version", snapshot.relationshipReviewVersion==1 && snapshot.relationshipResponses.Count==5
            && snapshot.relationshipResponses.Select(r=>r.responseOrder).SequenceEqual(Enumerable.Range(1,5)));
        var config=ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();config.uploadRawEventLogEnabled=false;
        var scenario=Resources.FindObjectsOfTypeAll<ScenarioDefinition>().First(s=>s.scenarioId==FinalDeductionAssessment.Rules.scenarioId);
        SessionLogger.Instance.Flush();
        var dto=AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,"","");
        Check("Raw-log-disabled upload preserves every locked answer as essential events", !dto.includesRawEventLog
            && dto.orderedEvents.Count(e=>e.eventType=="RelationshipInterpretationSubmitted")==5
            && dto.deductionAssessmentEvents.Count==0);
        System.IO.Directory.CreateDirectory("docs/evidence/relationship-review");
        System.IO.File.WriteAllText("docs/evidence/relationship-review/upload-wire.json",JsonUtility.ToJson(dto,true)); Destroy(config);

        STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip();
        var ending=EndingManager.Instance;
        MeasurementTestUtil.CompleteSceneSketch();
        STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        ending.TrySubmitConclusion(); yield return null; yield return null;
        var choices=ending.GetPickerOptionIdsForTest();
        Check("Completed available review opens the real conclusion picker",choices.Count>0);
        if(choices.Count>0)
        {
            // Simulate independent information arriving after the picker opened.
            // Actual RC-03/vendor access and discovery are covered by VendorFollowupTest.
            typeof(VendorFollowupInterview).GetField("<IsDiscovered>k__BackingField",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(VendorFollowupInterview.Instance,true);
            Check("Late statement creates a newly required relationship question",!review.IsReadyForConclusion);
            Check("Final confirmation refuses to skip a newly earned review",!ending.SelectConclusionForTest(choices[0]));
            yield return null; yield return null;
            Check("Picker releases ownership to the new relationship question",review.IsShowing && review.ActiveRelationshipId==VendorFollowupInterview.RelationshipId);
            if(review.IsShowing)
            {
                var timeline=FinalDeductionAssessment.Rules.relationships.Find(r=>r.id==VendorFollowupInterview.RelationshipId);
                ui=review.QuestionUI; Canvas.ForceUpdateCanvases();ui.ResponsiveLayout.Refresh();
                Check("Timeline choices also resize without text overflow",ui.DisplayedOptions.Count==4 && ui.DisplayedOptions.All(label=>
                    label.GetPreferredValues(label.text,label.rectTransform.rect.width-label.margin.x-label.margin.z,Mathf.Infinity).y<=label.rectTransform.rect.height+1));
                ui.SelectResponse(timeline.interpretations.FindIndex(c=>c.id==timeline.correctInterpretationId));ui.ConfirmSelection();
                Check("New question locks before conclusion can be resubmitted",review.IsReadyForConclusion && review.HasResponse(timeline.id)
                    && ending.AcceptedEnding==null && !review.IsShowing);
            }
        }
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path); yield return null;
        Check("Restart clears answers, review completion, UI and conclusion eligibility", EvidenceRelationshipsReview.Instance.Responses.Count==0
            && !EvidenceRelationshipsReview.Instance.IsShowing && !EvidenceBoardController.HasEverSubmittedTheory
            && !EvidenceRelationshipsReview.Instance.IsReadyForConclusion && !PlayerUIGate.IsBlocked);
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failure(s) ===");
        Report=report.ToString();IsDone=true;
    }
}
