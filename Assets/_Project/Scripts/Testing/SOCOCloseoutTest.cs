using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Focused phase/custody diagnostic. Fixtures isolate closeout from already-tested evidence procedures.</summary>
public sealed class SOCOCloseoutTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private void Check(string label, bool value) { report.AppendLine("  " + (value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static void SetPhase(ScenarioPhase value) => typeof(SceneTurnover).GetField("<Phase>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private static int Count(SessionEventType type) => SessionLogger.Instance.EventCounts.TryGetValue(type, out int count) ? count : 0;
    private IEnumerator DrainDialogue()
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 15d;
        while (STCSNotificationUI.Instance.IsShowing && Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (STCSNotificationUI.Instance.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f) STCSNotificationUI.Instance.Skip();
            yield return null;
        }
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(SOCOCloseoutTest));
        // This test has no tracked HMD. A floor-height camera can legitimately defer
        // wall-safe dialogue forever, so supply a standing-eye fixture for this UI flow.
        var originalCamera = Camera.main;
        originalCamera.tag = "Untagged";
        var cameraFixture = new GameObject("CloseoutStandingEyeFixture");
        var fixtureCamera = cameraFixture.AddComponent<Camera>();
        fixtureCamera.tag = "MainCamera";
        fixtureCamera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 1.6f), Quaternion.identity);
        foreach (var follow in FindObjectsByType<HudFollowCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            typeof(HudFollowCamera).GetField("targetCamera", Private).SetValue(follow, fixtureCamera);
        report.AppendLine("=== SOCOCloseoutTest (phase/custody fixtures, not procedure replay) ===");
        var flow = SOCOCloseout.Instance;
        var ending = EndingManager.Instance;
        var screen = FindFirstObjectByType<CompletionScreenUI>();
        var inventory = EvidenceInventory.Instance;
        var state = EvidenceStateManager.Instance;
        var cordon = FindFirstObjectByType<SceneCordon>();
        Check("Scene has persisted closeout references and begins in SOCO investigation", flow != null && ending != null && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation);
        if (flow == null || ending == null) { Finish(); yield break; }
        Check("Append-safe Complete ordinal and chronological phase query", (int)ScenarioPhase.InvestigationComplete == 6 && !SceneTurnover.HasReachedPhase(ScenarioPhase.InvestigationComplete));
        Check("Cannot skip directly to investigation completion or release", !SceneTurnover.TryAdvance(ScenarioPhase.InvestigationComplete) && !SceneTurnover.TryAdvance(ScenarioPhase.SceneRelease));
        Check("Cannot enter closeout before accepting conclusion feedback", !SceneTurnover.TryAdvance(ScenarioPhase.SOCOCloseout));
        var originalSet = (EndingSet)typeof(EndingManager).GetField("endingSet", Private).GetValue(ending);
        Set(screen, "lineDisplaySeconds", .001f);
        var fixtureSet = ScriptableObject.CreateInstance<EndingSet>();
        Check("Prior scene sketch is submitted before conclusion feedback",MeasurementTestUtil.CompleteSceneSketch());
        // Isolate downstream handoff for each already-authored result; do not modify real requirements.
        foreach (var authored in originalSet.endings)
        {
            screen.DismissForTest(); flow.HidePanel();
            yield return null;
            SetPhase(ScenarioPhase.SOCOInvestigation);
            Set(ending, "<AcceptedEnding>k__BackingField", null); Set(ending, "<FeedbackAccepted>k__BackingField", false);
            fixtureSet.endings = new List<EndingDefinition> { new EndingDefinition { endingId = authored.endingId, title = authored.title,
                endingText = authored.endingText, isFallback = true, priority = 1 } };
            Set(ending, "endingSet", fixtureSet);
            typeof(EndingManager).GetMethod("FinishSubmission", Private).Invoke(ending, new object[] { authored.requiredConclusionId ?? "CONCLUSION-INCONCLUSIVE" });
            Set(ending, "endingSet", originalSet);
            // Cold scene/shader frames can exceed three seconds before the first feedback tick.
            double deadline = Time.realtimeSinceStartupAsDouble + 15d;
            while (!screen.ContinueToCloseout() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Check(authored.endingId + ": feedback leads to closeout and preserves both result IDs", SceneTurnover.Phase == ScenarioPhase.SOCOCloseout
                && ending.ResolvedEndingId == authored.endingId && ending.SelectedConclusionId == (authored.requiredConclusionId ?? "CONCLUSION-INCONCLUSIVE"));
            if (SceneTurnover.Phase != ScenarioPhase.SOCOCloseout) report.AppendLine("    actual phase=" + SceneTurnover.Phase + "; selected=" + ending.SelectedConclusionId + "; ending=" + ending.ResolvedEndingId);
            float seconds = SceneTurnover.InvestigationSeconds;
            yield return new WaitForSecondsRealtime(.05f);
            Check(authored.endingId + ": timer continues and cordon remains secured", SceneTurnover.InvestigationSeconds > seconds && cordon.CordonVisible && !cordon.EntranceIsOpen);
            yield return DrainDialogue();
        }
        Destroy(fixtureSet);
        Check("Evidence processing stays enabled in closeout", !SceneTurnover.IsInvestigationBlocked && !ToolInputLock.IsLocked);
        var recordedScore = EvidenceBoardController.LastScoreResult;
        var board = FindAnyObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        if (board != null) board.SubmitTheory();
        Check("Accepted conclusion cannot be resubmitted or rescored during closeout", board != null && SceneTurnover.IsDeductionBlocked && !EvidenceBoardController.HasEverSubmittedTheory
            && object.Equals(recordedScore, EvidenceBoardController.LastScoreResult));
        Check("Unsecured evidence blocks review with useful feedback", !flow.AllAccountedFor && !flow.TryReviewInventory() && !string.IsNullOrEmpty(flow.MissingFeedback())
            && !SceneTurnover.TryAdvance(ScenarioPhase.EvidenceInventoryReview));
        Check("Pending review text does not claim secured evidence", flow.ReviewText.Contains(GameDialogue.Text("closeout-status-pending")) && !flow.ReviewText.Contains(GameDialogue.Text("closeout-status-stored")));
        yield return DrainDialogue();
        var body = state.GetRecord("BODY-A"); var knife = state.GetRecord("EVD-014"); var spatter = state.GetRecord("EVD-015"); var bottle = state.GetRecord("EVD-018");
        body.status = EvidenceStatus.Logged;
        Check("Body documentation alone is not closeout completion", !flow.EvidenceComplete(body.definition)
            && body.custodyLocation == EvidenceCustodyLocation.Scene);
        body.bodyRemovalStatus = BodyRemovalStatus.Removed; // closeout fixture; real transition tested by BodyRemovalTest
        Check("Body removal completes accountability without ordinary evidence sealing", flow.EvidenceComplete(body.definition));
        knife.status = EvidenceStatus.Processed; knife.fingerprintingDone = true; knife.fingerprintLabStatus = FingerprintLabStatus.Matched;
        spatter.status = EvidenceStatus.Processed;
        bottle.status = EvidenceStatus.Processed; bottle.fingerprintingDone = true; bottle.fingerprintLabStatus = FingerprintLabStatus.Matched;
        Check("Processed status alone does not substitute for physical custody", !flow.EvidenceComplete(knife.definition) && !flow.EvidenceComplete(bottle.definition) && !flow.EvidenceComplete(spatter.definition));
        var samplePrefab = (GameObject)typeof(SOCOCloseout).GetField("completedSamplePrefab", Private).GetValue(flow);
        var hand = FindFirstObjectByType<OVRCameraRig>().rightHandAnchor;
        foreach (var source in new[] { knife, spatter })
        {
            source.swabbingDone = true; source.swabSampleSealed = true;
            string id = HeldItemCoordinator.Instance.HoldContainedSample(source.definition.evidenceId, samplePrefab, hand);
            Check(source.definition.evidenceId + ": contained/sealed but unstored sample is not secured", id != null && !flow.SampleComplete(source.definition));
            Check(source.definition.evidenceId + ": B handler stores the separate completed sample", HeldItemCoordinator.Instance.TryStoreHeldEvidence() && flow.SampleComplete(source.definition));
        }
        Check("Knife and spatter source identities cannot substitute for each other", inventory.GetSampleSource("blood-sample:EVD-014") == "EVD-014" && inventory.GetSampleSource("EVD-015") == "EVD-015");
        foreach (var item in new[] { knife, bottle })
        {
            bool held = HeldItemCoordinator.Instance.HoldBaggedEvidence(item.definition.evidenceId, hand);
            Check(item.definition.evidenceId + ": sealed held bag still requires explicit storage", held && !flow.EvidenceComplete(item.definition) && !inventory.Deposit(item.definition.evidenceId, "diagnostic-box"));
            Check(item.definition.evidenceId + ": B storage completes authored bag custody", HeldItemCoordinator.Instance.TryStoreHeldEvidence() && flow.EvidenceComplete(item.definition));
        }
        bottle.fingerprintingDone = false;
        Check("Bottle's fingerprint work remains required after bag storage", !flow.EvidenceComplete(bottle.definition));
        bottle.fingerprintingDone = true;
        knife.swabSampleSealed = false;
        Check("Knife sample cannot borrow the spatter sample's sealed state", !flow.SampleComplete(knife.definition) && flow.SampleComplete(spatter.definition));
        knife.swabSampleSealed = true;
        yield return null;
        yield return DrainDialogue();
        Check("Closeout requires custody pass before review", !flow.CustodyPassComplete && !flow.ShowInventoryReview() && !SceneTurnover.TryAdvance(ScenarioPhase.FinalSceneSurvey));
        var box=EvidenceBoxDepositZone.Instance;
        fixtureCamera.transform.position=box.transform.position+new Vector3(0,1.1f,.35f);
        fixtureCamera.transform.LookAt(box.transform.position);
        Check("Bulk custody review opens at box", box.RequestCustodyPass());
        yield return new WaitForSecondsRealtime(.3f);
        Check("Bulk custody transfers all stored items with visible contents", box.ConfirmCustodyPass() && flow.CustodyPassComplete && box.Contents.ItemCount==4 && !inventory.AllEntryIds.Any());
        double reviewDeadline = Time.realtimeSinceStartupAsDouble + 30d;
        // Phase-entry teaching may arrive after DrainDialogue returned in its empty-frame gap.
        // Wait through real queued dialogue rather than racing it with a three-second deadline.
        while ((SceneTurnover.Phase == ScenarioPhase.SOCOCloseout || !flow.IsShowing)
            && Time.realtimeSinceStartupAsDouble < reviewDeadline)
        {
            if (STCSNotificationUI.Instance.IsShowing
                && STCSNotificationUI.Instance.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f)
                STCSNotificationUI.Instance.Skip();
            yield return null;
        }
        report.AppendLine("INFO: review phase="+SceneTurnover.Phase+"; owners="+string.Join(",",PlayerUIGate.LockOwnerNames)
            +"; dialogue="+STCSNotificationUI.Instance.CurrentText+"; tools="+ToolInputLock.HasUIOwners+"; missing="+flow.MissingFeedback()
            +"; eye="+Camera.main.transform.position+"; dialogueSuppressed="+STCSNotificationUI.Instance.GetComponent<HudFollowCamera>().IsLayoutSuppressed);
        Check("All accounted evidence automatically offers the live inventory review", SceneTurnover.Phase == ScenarioPhase.EvidenceInventoryReview && flow.IsShowing && flow.AllAccountedFor
            && !flow.ReviewText.Contains(GameDialogue.Text("closeout-status-pending")));
        Check("Review owns modal/tool input and cannot be skipped through phase API", PlayerUIGate.IsBlocked && ToolInputLock.IsLocked && !SceneTurnover.TryAdvance(ScenarioPhase.FinalSceneSurvey));
        CaptureReview(flow);
        flow.HidePanel(); yield return null;
        Check("Review can close/reopen without acknowledging it", !flow.InventoryConfirmed && flow.ShowInventoryReview());
        Check("Explicit review confirmation enters final survey", flow.ConfirmInventory() && SceneTurnover.Phase == ScenarioPhase.FinalSceneSurvey && flow.InventoryConfirmed);
        var destinationGuide=FindAnyObjectByType<ProcedureDestinationGuide>();destinationGuide.Refresh();
        Check("Final survey has an authored destination cue",destinationGuide.TargetId=="final-survey"
            &&destinationGuide.TargetPosition==flow.FinalSurveyPosition);
        Check("[training] inventory and final-survey explanations scheduled at their phase entries",
            ProceduralTrainingGuidance.Instance.HasScheduled("closeout-start") && ProceduralTrainingGuidance.Instance.HasScheduled("closeout-survey-guidance"));
        Check("Confirmed custody cannot be re-equipped during final survey", !HeldItemCoordinator.Instance.EquipEvidence("EVD-014", hand) && inventory.CurrentHeldEvidenceId == null);
        yield return DrainDialogue(); yield return null;
        var head = Camera.main.transform;
        head.position = new Vector3(50, 1.6f, 50);
        Check("Final survey cannot be performed from outside its authored area", !flow.CompleteFinalSurvey());
        var surveyArea = (Bounds)typeof(SOCOCloseout).GetField("finalSurveyArea", Private).GetValue(flow);
        var surveyTarget = (Vector3)typeof(SOCOCloseout).GetField("finalSurveyLookAt", Private).GetValue(flow);
        var surveyEye = surveyArea.center + new Vector3(surveyArea.extents.x*.5f,.4f,-surveyArea.extents.z*.4f);
        head.SetPositionAndRotation(surveyEye,Quaternion.LookRotation(surveyTarget-surveyEye));
        Check("Intentional SOCO survey advances to IOC report without reusing IOC observations", flow.CompleteFinalSurvey() && SceneTurnover.Phase == ScenarioPhase.IOCCompletionReport && flow.FinalSurveyCompleted);
        yield return null; yield return DrainDialogue(); yield return null;
        Check("Completion report still requires explicit confirmation before release", !SceneTurnover.TryAdvance(ScenarioPhase.SceneRelease));
        var ioc = (Transform)typeof(SOCOCloseout).GetField("ioc", Private).GetValue(flow);
        head.position = ioc.position + new Vector3(20, 1.6f, 20);
        int duplicateQuestionRequests = 0;
        System.Action<DialogueRequest> observeQuestion = request => { if (request.id == "closeout-ioc-question") duplicateQuestionRequests++; };
        GameDialogue.OnRequested += observeQuestion;
        try
        {
            Check("Completion report opens directly without approaching or restoring the retired IOC", flow.BeginIOCReport()
                && flow.IsShowing && !ioc.GetComponent<NPCPresentation>().IsVisible);
            var questionBody = (TMPro.TMP_Text)typeof(SOCOCloseout).GetField("body", Private).GetValue(flow);
            Check("Satisfaction question appears once in the confirmation panel, never as preceding dialogue",
                questionBody.text == GameDialogue.Text("closeout-ioc-question") && duplicateQuestionRequests == 0
                && !STCSNotificationUI.Instance.HasLinesFrom("soco-closeout-ioc"));
            Check("Repeated open requests cannot queue another question", !flow.BeginIOCReport() && duplicateQuestionRequests == 0);
            flow.HidePanel();
            yield return null;
            Check("Cancelling keeps the report unconfirmed and permits reopening the same panel", !flow.IOCConfirmed
                && flow.BeginIOCReport() && flow.IsShowing && duplicateQuestionRequests == 0);
        }
        finally { GameDialogue.OnRequested -= observeQuestion; }
        Check("IOC report still needs explicit confirmation", flow.IsShowing && flow.ConfirmIOCReport() && SceneTurnover.Phase == ScenarioPhase.SceneRelease && flow.IOCConfirmed);
        Check("Scene release precedes completion and cordon persists through IOC reply", !flow.ReleaseCompleted && cordon.CordonVisible && !SceneTurnover.TryAdvance(ScenarioPhase.InvestigationComplete));
        yield return null; yield return DrainDialogue(); yield return null;
        Check("Administrative closeout completes investigation with cordon still visible and closed", SceneTurnover.Phase == ScenarioPhase.InvestigationComplete && flow.ReleaseCompleted && cordon.CordonVisible && !cordon.EntranceIsOpen);
        float frozen = SceneTurnover.InvestigationSeconds, loggerFrozen = SessionLogger.Instance.ElapsedSeconds;
        yield return new WaitForSecondsRealtime(.05f);
        Check("Both investigation and finalized session clocks freeze only at completion", Mathf.Abs(frozen - SceneTurnover.InvestigationSeconds) < .001f && Mathf.Abs(loggerFrozen - SessionLogger.Instance.ElapsedSeconds) < .001f);
        Check("Conclusion and all evidence custody remain preserved at completion", ending.AcceptedEnding != null && !string.IsNullOrEmpty(ending.SelectedConclusionId) && flow.AllAccountedFor);
        Check("Each final milestone logs once", Count(SessionEventType.EvidenceInventoryConfirmed) == 1 && Count(SessionEventType.FinalSceneSurveyCompleted) == 1
            && Count(SessionEventType.SceneReleaseConfirmed) == 1 && Count(SessionEventType.InvestigationCompleted) == 1 && Count(SessionEventType.SessionEnded) == 1);
        yield return null; yield return null;
        Check("Final case review opens only after completed closeout",screen.IsInFinalReview&&screen.ReviewPageCount>0
            &&SceneTurnover.Phase==ScenarioPhase.InvestigationComplete);
        if(screen.IsInFinalReview)
        {
            head.SetPositionAndRotation(EvidenceBoxDepositZone.Instance.transform.position+new Vector3(0,1.6f,-4),Quaternion.identity);
            yield return null; yield return null;
            var next=screen.ReviewPointer.Targets.Single(t=>t.id=="review-next");
            var previous=screen.ReviewPointer.Targets.Single(t=>t.id=="review-previous");
            Check("First section disables Previous and shows a clear title",screen.ReviewPageIndex==0&&!previous.enabled&&screen.ReviewTitle=="Case summary");
            yield return new WaitForSecondsRealtime(.3f);
            next.onClick(); int page=screen.ReviewPageIndex; screen.NextReviewPage();
            Check("A single press advances only one section",page==1&&screen.ReviewPageIndex==page);
            yield return new WaitForSecondsRealtime(.3f);
            previous.onClick();
            Check("Previous returns to the first section without exiting",screen.IsInFinalReview&&screen.ReviewPageIndex==0&&PlayerUIGate.IsBlocked);
            var rect=(RectTransform)screen.ReviewLayout.transform;
            float height=rect.rect.height;
            for(int i=0;i<screen.ReviewPageCount;i++)
            {
                screen.ReviewLayout.Refresh(); Canvas.ForceUpdateCanvases();
                Check("Section "+(i+1)+": header and action labels fit",rect.GetComponentsInChildren<TMPro.TMP_Text>()
                    .Where(t=>!t.transform.IsChildOf(screen.ReviewLayout.Body.Content)).All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing&&!t.isTextTruncated;}));
                Check("Section "+(i+1)+": panel size stays stable and starts at the top",Mathf.Abs(rect.rect.height-height)<.01f
                    &&Mathf.Abs(screen.ReviewLayout.Body.Content.anchoredPosition.y)<.01f);
                CaptureFinalReview("section-"+(i+1).ToString("00")+".png");
                if(screen.ReviewLayout.Body.CanScroll)
                {
                    var footer=screen.ReviewLayout.Footer.Root.position;
                    screen.ReviewLayout.ScrollOptions(10000); Canvas.ForceUpdateCanvases();
                    Check("Section "+(i+1)+": long content scrolls without moving controls",screen.ReviewLayout.Body.Content.anchoredPosition.y>0
                        &&Vector3.Distance(footer,screen.ReviewLayout.Footer.Root.position)<.00001f);
                    CaptureFinalReview("section-"+(i+1).ToString("00")+"-scrolled.png");
                }
                if(i<screen.ReviewPageCount-1) { yield return new WaitForSecondsRealtime(.3f); next.onClick(); }
            }
            Check("Last section offers an explicit finish action",rect.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="[A] Finish review"));
            Check("Review navigation preserves finalized state",Count(SessionEventType.SessionEnded)==1&&ending.AcceptedEnding!=null&&SceneTurnover.Phase==ScenarioPhase.InvestigationComplete);
        }
        screen.DismissForTest();
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name); yield return null;
        Check("Fresh scene resets results, closeout milestones, inventory, timer and cordon", SceneTurnover.IsPending && EndingManager.Instance.AcceptedEnding == null
            && !SOCOCloseout.Instance.InventoryConfirmed && !SOCOCloseout.Instance.FinalSurveyCompleted && !SOCOCloseout.Instance.IOCConfirmed
            && !SOCOCloseout.Instance.ReleaseCompleted && !EvidenceInventory.Instance.AllEntryIds.Any() && SceneTurnover.InvestigationSeconds == 0f && FindFirstObjectByType<SceneCordon>().CordonVisible);
        Finish();
    }
    private void CaptureReview(SOCOCloseout flow)
    {
#if UNITY_EDITOR
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = Camera.main;
        var target = new RenderTexture(1100, 1000, 24);
        var old = camera.targetTexture; var active = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(1100, 1000, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1100, 1000), 0, 0); texture.Apply();
        System.IO.Directory.CreateDirectory("docs/evidence/custody-pass"); System.IO.File.WriteAllBytes("docs/evidence/custody-pass/inventory-review.png", texture.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = active; Destroy(texture); target.Release(); Destroy(target);
#endif
    }
    private void CaptureFinalReview(string filename)
    {
#if UNITY_EDITOR
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera=Camera.main; var old=camera.targetTexture; var active=RenderTexture.active;
        var target=RenderTexture.GetTemporary(1200,900,24); var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1200,900),0,0); texture.Apply();
            const string folder="docs/evidence/final-review-ui-2026-10-23";
            System.IO.Directory.CreateDirectory(folder); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,filename),texture.EncodeToPNG());
        }
        finally {camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(texture);}
#endif
    }
    private void Finish() { report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL, " + failures + " failures ==="); Report = report.ToString(); IsDone = true; Debug.Log(Report); }
}
