using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Witness access, required follow-up, review and deduction checks. Reuses the knife lifecycle fixture;
/// never submits a conclusion or uploads a session. Reload verifies the actual reset.</summary>
public class VendorFollowupTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== VendorFollowupTest ===\n");
    private int failures;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }

    private IEnumerator Run()
    {
        var interview = VendorFollowupInterview.Instance;
        // Access/deduction suite: arrange the new prerequisites; the real checkpoint
        // and teaching order are covered by NarrativeRoleFlowTest/ReasoningCheckpointTest.
        var checkpoints = ReasoningCheckpointManager.Instance; checkpoints.enabled = false;
        ProceduralTrainingGuidance.Instance.enabled = false;
        var review = FindAnyObjectByType<WitnessAccountsUI>();
        var witness = FindAnyObjectByType<VendorWitnessInteraction>();
        // Offline render captures must not hand control back to an unavailable
        // headset pose before the real controller-input checks.
        var offlineRig=Camera.main.GetComponentInParent<OVRCameraRig>();
        if(offlineRig!=null)offlineRig.enabled=false;
        var offlineEye=Camera.main.transform.position;
        offlineEye.y=witness.transform.position.y+1.6f;
        Camera.main.transform.position=offlineEye;
        var menu = FindAnyObjectByType<UtilityMenuController>();
        var menuEntries = (System.Collections.Generic.List<UtilityMenuController.MenuEntry>)typeof(UtilityMenuController)
            .GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu);
        Check("Utility replaces global Vendor Follow-up with Witness Accounts; no accounts before turnover",
            !menuEntries.Any(entry => entry.displayName == "Vendor Follow-up")
            && menuEntries.Single(entry => entry.displayName == "Witness Accounts").onSelect.GetPersistentTarget(0) == review
            && menuEntries.Single(entry => entry.displayName == "Witness Accounts").onSelect.GetPersistentMethodName(0) == "Toggle"
            && review.GetKnownAccounts().Count == 0);
        var relationships = RelationshipStateManager.Instance;
        var board = FindFirstObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        var node = FindObjectsByType<EvidenceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(n => n.nodeId == VendorFollowupInterview.StatementId);
        var paul = FindObjectsByType<EvidenceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(n => n.nodeId == "statement_paul_timeline");
        node.RefreshVisibility();
        relationships.MarkSelectedForPair(paul.nodeId, node.nodeId);
        Check("Before follow-up: no statement/card/relationship, and IOC cannot ask",
            !interview.IsDiscovered && !node.gameObject.activeSelf && !interview.AskAnyoneElse()
            && relationships.GetRecord(VendorFollowupInterview.RelationshipId).state == RelationshipState.Locked
            && string.IsNullOrEmpty(interview.StatementSummary));

        SceneTurnover.Controller.CompleteWithoutDialogue();
        var assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        foreach (var observation in assessment.Definition.observations) SceneTurnover.RecordIOCObservation(observation.observationId);
        SceneTurnover.CompleteIOCAssessment();
        SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested);
        SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover);
        SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation);
        yield return null;

        STCSNotificationUI.Instance.HideImmediately(); // isolate review UI from the procedural briefing queue
        review.Show();
        var known = review.GetKnownAccounts();
        bool readOnly = review.IsShowing && known.Count == 6
            && known.Count(account => account.Person == "Paul") == 2 && known.Count(account => account.Person == "Peter") == 1
            && known.Count(account => account.Person == "Vendor") == 3
            && !known.Any(account => account.CatalogId == "statement-paul-knife" || account.CatalogId == "vendor-paul-3am-summary"
                || account.CatalogId == "statement-peter-appearance" || account.CatalogId == "statement-peter-money")
            && !interview.AskAnyoneElse() && !witness.Interact();
        CaptureReview("witness-accounts-before.png");
        review.Hide();
        MoveToVendor(witness.InteractionRadius + 5f);
        yield return null;
        interview.ShowFromVendor(witness);
        witness.RefreshHint();
        Check("Review shows only known grouped accounts; remote opening/question are refused",
            readOnly && !interview.IsShowing && !witness.IsPromptShown && !witness.Interact() && !interview.AskAnyoneElse());
        Check("Vendor hint stays hidden outside interaction range",!witness.IsHintVisible);

        MoveToVendor(1.2f);
        yield return null;
        NotificationManager.Instance.UI.ClearTooltip(); witness.RefreshHint();
        Check("Approaching vendor gives an explicit talk hint before follow-up unlock",witness.IsHintVisible&&witness.HintText.Contains("[A] Talk"));
        typeof(VendorWitnessInteraction).GetField("playerHead",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(witness,null);
        Check("Vendor range recovers a head missing during initialization",witness.PlayerInRange());
        var originalCamera=Camera.main; string originalTag=originalCamera.tag;
        var replacement=new GameObject("Vendor replacement camera test",typeof(Camera)); replacement.tag="MainCamera"; originalCamera.tag="Untagged";
        try
        {
            replacement.transform.position=witness.InteractionPosition+Vector3.right*(witness.InteractionRadius+5);
            Check("Camera replacement cannot leave vendor range bound to a stale head",!witness.PlayerInRange());
            replacement.transform.position=witness.InteractionPosition+Vector3.right;
            Check("Current camera can approach and use the vendor",witness.PlayerInRange());
        }
        finally {replacement.tag="Untagged";originalCamera.tag=originalTag;Destroy(replacement);}
        witness.RefreshHint();
        using(VendorWitnessInteraction.HideForCapture())
        {
            using(VendorWitnessInteraction.HideForCapture()) {witness.RefreshHint();Check("Photo capture hides vendor guidance",!witness.IsHintVisible);}
            witness.RefreshHint();Check("Nested capture stays hidden",!witness.IsHintVisible);
        }
        Check("Vendor guidance returns without owning UI",witness.IsHintVisible&&!PlayerUIGate.IsBlocked&&!ToolInputLock.HasUIOwners);
        var gloves=ArrivalPPEStation.Instance;
        const System.Reflection.BindingFlags privateFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var gloveState=typeof(ArrivalPPEStation).GetField("<State>k__BackingField",privateFlags);
        var sampleSource=typeof(ArrivalPPEStation).GetField("<LastSampleSource>k__BackingField",privateFlags);
        var oldState=gloveState.GetValue(gloves); var oldSource=sampleSource.GetValue(gloves);
        try
        {
            gloveState.SetValue(gloves,ArrivalPPEStation.GloveState.NeedsChange); sampleSource.SetValue(gloves,"EVD-014");
            witness.RefreshHint();
            Check("Blocked approach explains the real procedure requirement",ProcedureInterlock.BlocksUnrelatedActions&&witness.IsHintVisible
                &&witness.HintText.Contains(ProcedureInterlock.Reason)&&!witness.Interact());
            yield return CaptureVendorHint(witness,"vendor-blocked-hint.png");
        }
        finally {gloveState.SetValue(gloves,oldState);sampleSource.SetValue(gloves,oldSource);}
        witness.RefreshHint();
        Check("Paul follow-up stays locked before confirmed knife comparison", !interview.CanAsk && !interview.RequiresFollowup);
        var knifeRecord = EvidenceStateManager.Instance.GetRecord("EVD-014");
        knifeRecord.fingerprintLabStatus = FingerprintLabStatus.SentToLab;
        Check("Lifted but unmatched knife print does not require the interview", !interview.FollowupUnlocked && VendorFollowupInterview.CompletionBlockReason == null);
        EvidenceStateManager.Instance.MarkMatched("EVD-014");
        FindingStateManager.Instance.RefreshFindings(); checkpoints.Evaluate();
        Check("Confirmed Paul knife match immediately unlocks and requires the question without a reasoning answer",
            interview.CanAsk && interview.RequiresFollowup && VendorFollowupInterview.CompletionBlockReason != null && !checkpoints.IsAnswered("RC-03-FINGERPRINT"));
        checkpoints.RecordResponse("RC-03-FINGERPRINT", "RC-03-FINGERPRINT-C");
        Check("Reasoning response leaves the required question available", interview.CanAsk);
        witness.RefreshHint();
        var destination=FindAnyObjectByType<ProcedureDestinationGuide>(); destination.Refresh();
        Check("Nearby interaction hint takes over from the travel waypoint",destination.TargetId!="vendor-followup");
        yield return CaptureVendorHint(witness,"vendor-ready-hint.png");
        MoveToVendor(1.2f); yield return null; yield return null; witness.RefreshHint();
        Check("Unlocked follow-up hint explains both opening and asking",witness.IsHintVisible&&witness.HintText.Contains("[A] Talk")&&witness.HintText.Contains("follow-up question"));
        report.AppendLine("INFO: ready hint="+witness.IsHintVisible+"; eye="+Camera.main.transform.position+"; tooltip="+NotificationManager.Instance.UI.CurrentTooltip);
        bool nearPrompt = witness.IsPromptShown;
        report.AppendLine("INFO: vendor A ready="+nearPrompt+"; range="+witness.PlayerInRange()+"; owners="+string.Join(",",PlayerUIGate.LockOwnerNames)+"; toolGuard="+ToolInputLock.HasUIOwners);

        UnityEngine.InputSystem.InputSystem.RegisterLayout("{\"name\":\"ViCSIVendorController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\"}]}");
        var vendorDevice = UnityEngine.InputSystem.InputSystem.AddDevice("ViCSIVendorController");
        UnityEngine.InputSystem.InputSystem.SetDeviceUsage(vendorDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
        UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent((UnityEngine.InputSystem.Controls.ButtonControl)vendorDevice["primaryButton"], true);
        yield return null; yield return null;
        bool openingConsumed = interview.IsShowing && !interview.IsTalking && !interview.IsDiscovered;
        report.AppendLine("INFO: vendor A opened="+openingConsumed+"; range="+witness.PlayerInRange()+"; owners="+string.Join(",",PlayerUIGate.LockOwnerNames));
        Check("Actual vendor-opening A is consumed against jump until release", openingConsumed && PlayerUIGate.SuppressGameplayJump);
        witness.RefreshHint(); Check("Opening the interview hides nearby guidance",!witness.IsHintVisible);
        UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent((UnityEngine.InputSystem.Controls.ButtonControl)vendorDevice["primaryButton"], false);
        yield return null;
        UnityEngine.InputSystem.InputSystem.RemoveDevice(vendorDevice);
        UnityEngine.InputSystem.InputSystem.RemoveLayout("ViCSIVendorController");
        yield return null;
        Check("Physical vendor range shows contextual prompt and A opening does not also ask", nearPrompt && openingConsumed
            && witness.PlayerInRange() && witness.InteractionRadius == 2.25f);
        bool panel = interview.IsShowing;
        Vector3 point = interview.Pointer.WorldCentre(VendorFollowupInterview.QuestionId);
        var row = interview.Pointer.Targets.First(t => t.id == VendorFollowupInterview.QuestionId).rect;
        bool asked = interview.Pointer.ClickWithRay(new Ray(point - row.forward * .5f, row.forward))
            == VendorFollowupInterview.QuestionId && interview.IsTalking;
        bool notYetKnown = !interview.IsDiscovered && !interview.AskAnyoneElse();
        yield return DrainDialogue(interview);
        Check("One question delivers the answer and discovers/logs exactly once; repeats refused",
            panel && asked && notYetKnown && interview.IsDiscovered && !interview.AskAnyoneElse()
            && Count(SessionEventType.WitnessFollowupQuestionSelected) == 1
            && Count(SessionEventType.WitnessStatementDiscovered) == 1
            && DialogueHistoryManager.Instance.Entries.Count(e => e.sourceId == VendorFollowupInterview.StatementId
                && e.speaker == "Vendor") == 1);
        Check("Only the delivered vendor answer clears the required completion gate", !interview.RequiresFollowup
            && VendorFollowupInterview.CompletionBlockReason == null);

        node.RefreshVisibility();
        OpenAtVendor();
        Check("Read-only vendor panel does not register an unavailable A question", interview.IsShowing
            && !interview.CanAsk && !Equals(PlayerUIGate.ConfirmOwnerToken, interview));
        Check("Discovered statement is available in Statements and on the board, with observation limits",
            node.gameObject.activeSelf && interview.IsShowing
            && interview.HasDiscovered(node.nodeId) && interview.StatementSummary.Contains("3:00 AM")
            && interview.StatementDetail.Contains("did not see Paul enter")
            && interview.StatementDetail.Contains("did not witness the attack")
            && relationships.GetRecord(VendorFollowupInterview.RelationshipId).state == RelationshipState.Available);
        interview.Hide();
        yield return null; yield return null;
        witness.RefreshHint(); Check("Completed follow-up offers reviewing the recorded account",witness.IsHintVisible&&witness.HintText.Contains("Review the recorded"));

        STCSNotificationUI.Instance.HideImmediately(); // isolate the second read-only review from queued BODY-A teaching, as above
        review.Show();
        report.AppendLine("INFO: discovered review showing=" + review.IsShowing
            + "; owners=" + string.Join(",", PlayerUIGate.LockOwnerNames)
            + "; dialogue=" + STCSNotificationUI.Instance.CurrentText
            + "; account count=" + review.GetKnownAccounts().Count(account => account.CatalogId == "vendor-paul-3am-summary")
            + "; summary present=" + review.ReviewText.Contains(interview.StatementSummary)
            + "; question present=" + review.ReviewText.Contains(GameDialogue.Text(VendorFollowupInterview.QuestionId)));
        Check("Witness Accounts adds discovered 3AM account once without interviewing or duplicating history",
            review.IsShowing && review.GetKnownAccounts().Count(account => account.CatalogId == "vendor-paul-3am-summary") == 1
            && review.ReviewText.Contains(interview.StatementSummary) && !review.ReviewText.Contains(GameDialogue.Text(VendorFollowupInterview.QuestionId))
            && Count(SessionEventType.WitnessFollowupQuestionSelected) == 1 && Count(SessionEventType.WitnessStatementDiscovered) == 1);
        CaptureReview("witness-accounts-after.png");
        review.Hide();

        bool sightingInsufficient = EndingManager.Instance.ResolveEnding("conclusion-brother").Ending?.endingId
            != "ending-brother-accused";
        var flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        flow.SetEvidenceIdsForTest(new[] { "EVD-014" }, skipSubmit: true);
        // Remove the earlier lab-ready arrangement before replaying the physical
        // dust/lift fixture, which must resolve its own match from the initial status.
        knifeRecord.fingerprintLabStatus = FingerprintLabStatus.NotSent;
        knifeRecord.fingerprintLabMatchResult = string.Empty;
        // This witness/board fixture arranges the already completed blood trace.
        // BiologicalSamplingTest covers taking, sealing and explicitly storing it;
        // an unstored sample would correctly prevent the later knife bag seal.
        knifeRecord.swabbingDone = true;
        knifeRecord.swabSampleSealed = true;
        string fixture = flow.RunFullFlow();
        System.IO.Directory.CreateDirectory("docs/evidence/narrative-role-flow");
        System.IO.File.WriteAllText("docs/evidence/narrative-role-flow/vendor-knife-fixture.txt", fixture);
        yield return null;
        Check("Vendor sighting plus identified knife print cannot support Paul without the other findings",
            sightingInsufficient && fixture.Contains("=== PASS")
            && FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER")
            && review.GetKnownAccounts().Any(account => account.CatalogId == "statement-paul-knife")
            && EndingManager.Instance.ResolveEnding("conclusion-brother").Ending?.endingId != "ending-brother-accused");

        // Focus here is the requirement chain, using the same public report-selection
        // APIs as StoryContentTest. Physical procedures are covered separately.
        var esm = EvidenceStateManager.Instance;
        esm.MarkFound("BODY-A", default);
        esm.RecordReportLinesSelected("BODY-A", new[] { "body-a-wound-sharp-object" });
        esm.RecordReportLinesSelected("EVD-014", new[] { "knife-wound-match" });
        esm.MarkFound("EVD-015", default);
        esm.RecordReportLinesSelected("EVD-015", new[] { "blood-spatter-mechanism-only" });
        foreach (string id in new[] { "REL-knife-consistent-wound", "REL-spatter-consistent-reconstruction", "REL-paul-print-handled-knife" })
        {
            var def = relationships.GetRecord(id).definition;
            relationships.MarkSelectedForPair(def.sourceId, def.targetId);
        }
        relationships.CompleteAllSelected();
        Check("Full physical chain plus discovery still resolves premature Paul without timeline deduction",
            relationships.AllRecords.Count(r => r.state == RelationshipState.Completed) == 3
            && EndingManager.Instance.ResolveEnding("conclusion-brother").Ending?.endingId == "ending-premature-submission");

        board.TryConnect(paul, node);
        Check("Retired board linking cannot complete the timeline relationship", relationships.GetRecord(VendorFollowupInterview.RelationshipId).state != RelationshipState.Completed);
        var timelineRule = FinalDeductionAssessment.Rules.relationships.Find(r=>r.id==VendorFollowupInterview.RelationshipId);
        EvidenceRelationshipsReview.Instance.ConfirmResponse(timelineRule.id,timelineRule.correctInterpretationId);
        Check("Confirmed review interpretation completes timeline contradiction; combined chain supports Paul, Peter stays unsupported",
            relationships.GetRecord(VendorFollowupInterview.RelationshipId).state == RelationshipState.Completed
            && relationships.GetRecord(VendorFollowupInterview.RelationshipId).definition.isCorrect
            && EndingManager.Instance.ResolveEnding("conclusion-brother").Ending?.endingId == "ending-brother-accused"
            && EndingManager.Instance.ResolveEnding("conclusion-peter").Ending?.endingId == "ending-peter-unsupported");

        // Run the independent completion-guard fixture after the physical interview:
        // completing scene photographs/sketches moves the fixture camera and advances
        // evidence states, so it must not seed the earlier interaction/knife replay.
        CheckRequiredCompletionGates();
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path);
        yield return null;
        interview = VendorFollowupInterview.Instance;
        node = FindObjectsByType<EvidenceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(n => n.nodeId == VendorFollowupInterview.StatementId);
        node.RefreshVisibility();
        Check("New investigation resets statement, card, dialogue history and timeline relationship",
            !interview.IsDiscovered && !interview.IsTalking && !node.gameObject.activeSelf
            && string.IsNullOrEmpty(interview.StatementDetail)
            && !DialogueHistoryManager.Instance.Entries.Any(e => e.sourceId == VendorFollowupInterview.StatementId)
            && RelationshipStateManager.Instance.GetRecord(VendorFollowupInterview.RelationshipId).state == RelationshipState.Locked
            && FindAnyObjectByType<WitnessAccountsUI>().GetKnownAccounts().Count == 0);
        report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures} failure(s) ===");
        Report = report.ToString(); IsDone = true;
    }

    private static int Count(SessionEventType type) => SessionLogger.Instance.EventCounts.TryGetValue(type, out int n) ? n : 0;
    private IEnumerator CaptureVendorHint(VendorWitnessInteraction witness,string filename)
    {
#if UNITY_EDITOR
        var camera=Camera.main; var rig=camera.GetComponentInParent<OVRCameraRig>(); bool rigEnabled=rig!=null&&rig.enabled;
        var position=camera.transform.position; var rotation=camera.transform.rotation;
        if(rig!=null)rig.enabled=false;
        try
        {
            camera.transform.position=witness.transform.position+new Vector3(1.2f,1.6f,0);
            camera.transform.LookAt(witness.transform.position+Vector3.up*1.1f); witness.RefreshHint();
            yield return new WaitForSecondsRealtime(.45f);
            var label=FindObjectsByType<TMPro.TMP_Text>().FirstOrDefault(t=>t.name=="Vendor hint");
            label?.ForceMeshUpdate();
            Check(filename+": nearby hint fits without clipping",label!=null&&!label.isTextOverflowing&&!label.isTextTruncated);
            var previous=camera.targetTexture; var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1200,900,24); var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1200,900),0,0); texture.Apply();
                const string folder="docs/evidence/vendor-nearby-hint-2026-10-23";
                System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,filename),texture.EncodeToPNG());
            }
            finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(texture);}
        }
        finally {camera.transform.SetPositionAndRotation(position,rotation);if(rig!=null)rig.enabled=rigEnabled;witness.RefreshHint();}
#else
        yield break;
#endif
    }
    private void CheckRequiredCompletionGates()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var ending = EndingManager.Instance;
        var closeout = SOCOCloseout.Instance;
        var phase = typeof(SceneTurnover).GetField("<Phase>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var feedback = typeof(EndingManager).GetField("<FeedbackAccepted>k__BackingField", flags);
        var release = typeof(SOCOCloseout).GetField("<ReleaseCompleted>k__BackingField", flags);
        var discovered = typeof(VendorFollowupInterview).GetField("<IsDiscovered>k__BackingField", flags);
        bool originalDiscovery = VendorFollowupInterview.Instance.IsDiscovered;
        var originalPhase = SceneTurnover.Phase;
        bool originalFeedback = ending.FeedbackAccepted, originalRelease = closeout.ReleaseCompleted;
        try
        {
            discovered.SetValue(VendorFollowupInterview.Instance, false);
            Check("Submit Sketch completes before isolating the vendor completion requirement", MeasurementTestUtil.CompleteSceneSketch());
            STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip();
            int blocked = Count(SessionEventType.ConclusionSubmissionBlocked);
            ending.TrySubmitConclusion();
            bool refused = !(bool)typeof(EndingManager).GetMethod("ConfirmConclusionPick", flags).Invoke(ending, new object[] { "conclusion-peter" });
            typeof(EndingManager).GetMethod("FinishSubmission", flags).Invoke(ending, new object[] { "conclusion-peter" });
            Check("Conclusion opening, stale confirmation and direct completion all refuse a pending vendor answer",
                refused && ending.AcceptedEnding == null && ending.GetPickerOptionIdsForTest().Count == 0
                && Count(SessionEventType.ConclusionSubmissionBlocked) == blocked + 3);
            Check("Vendor refusal leaves walking and tools available", !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners);
            feedback.SetValue(ending, true);
            Check("Accepted feedback cannot bypass the vendor requirement into closeout", !SceneTurnover.TryAdvance(ScenarioPhase.SOCOCloseout));
            phase.SetValue(null, ScenarioPhase.SceneRelease); release.SetValue(closeout, true);
            Check("Final scene release cannot bypass an unfinished required vendor interview", !SceneTurnover.TryAdvance(ScenarioPhase.InvestigationComplete));
        }
        finally
        {
            discovered.SetValue(VendorFollowupInterview.Instance, originalDiscovery);
            phase.SetValue(null, originalPhase); feedback.SetValue(ending, originalFeedback); release.SetValue(closeout, originalRelease);
            STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip();
        }
    }
    /// <summary>Fixture uses the real physical vendor entry point; no remote interview bypass.</summary>
    public static bool OpenAtVendor()
    {
        var witness = FindAnyObjectByType<VendorWitnessInteraction>();
        var camera = Camera.main;
        if (witness == null || camera == null) return false;
        MoveToVendor(1.2f);
        return witness.Interact();
    }
    private static void MoveToVendor(float distance)
    {
        var witness = FindAnyObjectByType<VendorWitnessInteraction>();
        var camera = Camera.main;
        Vector3 desired = witness.InteractionPosition + Vector3.right * distance;
        desired.y = camera.transform.position.y;
        var rig = camera.GetComponentInParent<OVRCameraRig>();
        if (rig != null) rig.transform.position += desired - camera.transform.position;
        else camera.transform.position = desired;
    }
    private static void CaptureReview(string name)
    {
        if (!System.Environment.GetCommandLineArgs().Contains("-witnessUICaptures")) return;
        var camera = Camera.main; var target = new RenderTexture(1600, 1000, 24);
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
        System.IO.Directory.CreateDirectory("Temp/witness-ui"); System.IO.File.WriteAllBytes("Temp/witness-ui/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null; Destroy(image); target.Release(); Destroy(target);
    }
    private void Check(string name, bool pass) { if (!pass) failures++; report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); }
    public static IEnumerator DrainDialogue(VendorFollowupInterview interview)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 60d;
        while (interview.IsTalking && Time.realtimeSinceStartupAsDouble < deadline)
        {
            var ui = STCSNotificationUI.Instance;
            if (ui != null && ui.IsShowing && ui.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f) ui.Skip();
            yield return null;
        }
    }
}
