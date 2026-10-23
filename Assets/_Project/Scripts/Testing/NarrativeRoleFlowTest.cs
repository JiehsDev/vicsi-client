using System.Collections;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Real briefing/choice/vendor handlers, with a lab-ready print fixture. No uploads.</summary>
public sealed class NarrativeRoleFlowTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== NarrativeRoleFlowTest ===\n");
    private int failures;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private void Check(string label, bool value) { report.AppendLine("  " + (value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    private static NPCPresentation IOC() => FindObjectsByType<NPCPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Single(n => n.CharacterRole == NPCPresentation.Role.IOC);
    private static void ClearGuidance() { STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip(); NotificationManager.Instance.UI.HidePrompt(); }
    private static void SkipVisible(bool preserveAmbient = false)
    {
        var dialogue = STCSNotificationUI.Instance;
        if (dialogue.IsShowing && dialogue.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f) dialogue.Skip();
        NotificationManager.Instance.UI.ClearTooltip();
        if (!preserveAmbient) NotificationManager.Instance.UI.HidePrompt();
    }
    private static IEnumerator DrainAll()
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        while ((STCSNotificationUI.Instance.IsShowing || STCSNotificationUI.Instance.PendingCount > 0) && Time.realtimeSinceStartupAsDouble < deadline)
        { SkipVisible(); yield return null; }
        yield return null;
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(NarrativeRoleFlowTest));
        yield return null;
        var manager = ReasoningCheckpointManager.Instance;
        var hypothesis = HypothesisCheckpointManager.Instance;
        var training = ProceduralTrainingGuidance.Instance;
        var ui = FindAnyObjectByType<HypothesisCheckpointUI>();
        var actor = IOC(); var workflow = FindAnyObjectByType<SOCOWorkflowInteraction>();
        var marker = GameObject.Find("IOC_Start_Ground_Circle");
        var vendor = VendorFollowupInterview.Instance;
        manager.enabled = false; hypothesis.enabled = false; training.enabled = false;
        Check("Fresh session has one existing IOC actor and locked vendor follow-up", actor != null && actor.gameObject.activeInHierarchy
            && !vendor.FollowupUnlocked && !vendor.CanAsk && !vendor.IsDiscovered);
        Check("IOC starting ground marker is visible at the start of a fresh scenario", workflow.IsIOCStartingMarkerVisible);
        Check("IOC ground marker is a standalone, editable scene object", marker != null && marker.activeInHierarchy
            && marker.transform.parent == null && marker.GetComponent<Collider>() == null);
        Check("IOC guidance settings are serialized with editable defaults", workflow.InteractionRadius > 0f
            && workflow.GuidanceTitle == "Meet the IOC" && !string.IsNullOrWhiteSpace(workflow.GuidanceMessage)
            && workflow.ShowGuidanceOnApproach && workflow.HideMarkerOnBriefing);
        SceneTurnover.Controller.CompleteWithoutDialogue();
        var assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        foreach (var observation in assessment.Definition.observations) SceneTurnover.RecordIOCObservation(observation.observationId);
        SceneTurnover.CompleteIOCAssessment(); SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested);
        ClearGuidance();
        Check("Existing arrival acknowledges into opening IOC briefing", workflow.AcknowledgeArrival() && SceneTurnover.Phase == ScenarioPhase.SOCOTurnover);
        Check("Arrival holds gameplay input and rejects a second arrival", PlayerUIGate.IsBlocked && !workflow.AcknowledgeArrival());
        var arrivalHead = Camera.main.transform;
        Vector3 beforeArrival = arrivalHead.position;
        bool reachedBlack = false, movedUnderBlack = false, uncoveredMove = false;
        bool moved = false;
        double arrivalDeadline = Time.realtimeSinceStartupAsDouble + 8;
        while (workflow.IsArriving && Time.realtimeSinceStartupAsDouble < arrivalDeadline)
        {
            reachedBlack |= workflow.ArrivalAlpha >= .999f;
            float distance = Vector2.Distance(new Vector2(arrivalHead.position.x, arrivalHead.position.z), new Vector2(beforeArrival.x, beforeArrival.z));
            if (!moved && distance > .1f) { moved = true; movedUnderBlack = workflow.ArrivalAlpha >= .999f; uncoveredMove = !movedUnderBlack; }
            yield return null;
        }
        Check("Arrival reaches opaque black before repositioning", reachedBlack && !uncoveredMove && (!moved || movedUnderBlack));
        Check("Fade ends clear and releases its UI lock", !workflow.IsArriving && workflow.ArrivalAlpha == 0 && !PlayerUIGate.IsBlockedByOther(null));
        var arrivalCharacter = FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        Check("Arrival capsule is grounded at the authored entrance floor", arrivalCharacter != null
            && Mathf.Abs(arrivalCharacter.Pose.position.y - arrivalCharacter.Height * .5f - workflow.ArrivalFeetAnchor.position.y) < .1f);
        float revealedHeight = arrivalHead.position.y;
        yield return new WaitForSecondsRealtime(.4f);
        Check("Revealed view has no settling drop", Mathf.Abs(arrivalHead.position.y - revealedHeight) < .03f);
        Check("IOC visible and collider enabled for required briefing", actor.IsVisible && actor.GetComponent<Collider>().enabled);
        ClearGuidance();
        typeof(SOCOWorkflowInteraction).GetField("interactionRadius", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workflow, 3.75f);
        typeof(SOCOWorkflowInteraction).GetField("guidanceTitle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workflow, "Custom IOC guidance title");
        typeof(SOCOWorkflowInteraction).GetField("guidanceMessage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workflow, "Custom IOC guidance message.");
        Vector3 authoredMarkerPosition = marker.transform.position;
        marker.transform.position += new Vector3(.5f, 0f, .3f);
        Vector3 movedMarkerPosition = marker.transform.position;
        yield return null;
        Check("Manually moving the marker remains authoritative at runtime", marker.transform.position == movedMarkerPosition);
        Transform playerRig = Camera.main.GetComponentInParent<OVRCameraRig>().transform;
        playerRig.position = marker.transform.position + Vector3.forward * (workflow.InteractionRadius + 1f);
        yield return null;
        Check("IOC briefing prompt stays unavailable outside the editable interaction radius", !workflow.IsWithinIOCInteractionRadius
            && !workflow.IsPanelVisible && !workflow.BeginBriefing());
        playerRig.position = marker.transform.position + Vector3.forward * (workflow.InteractionRadius - .25f);
        yield return null;
        Check("Approach shows the editable IOC guidance and existing interaction prompt", workflow.IsWithinIOCInteractionRadius
            && workflow.IsPanelVisible && NotificationManager.Instance.UI.CurrentAmbientPrompt == "Custom IOC guidance title\nCustom IOC guidance message.");
        Check("Real IOC briefing starts with actor still present and hides its ground marker", workflow.BeginBriefing()
            && actor.IsVisible && workflow.IsTalking && !workflow.IsIOCStartingMarkerVisible
            && string.IsNullOrEmpty(NotificationManager.Instance.UI.CurrentAmbientPrompt));
        bool visibleThroughout = true;
        double deadline = Time.realtimeSinceStartupAsDouble + 35;
        while (workflow.IsTalking && Time.realtimeSinceStartupAsDouble < deadline)
        { visibleThroughout &= actor.IsVisible; SkipVisible(); yield return null; }
        yield return null;
        Check("IOC stays visible through every opening sentence", visibleThroughout
            && DialogueHistoryManager.Instance.Entries.Count(e => e.sourceId == SOCOWorkflowInteraction.BriefingSourceId) == 3);
        Check("Dialogue completion enters investigation and hides rendering/interaction", !workflow.IsTalking
            && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation && !actor.IsVisible && !actor.GetComponent<Collider>().enabled);
        Check("Ground marker stays hidden through the role handoff", !workflow.IsIOCStartingMarkerVisible);
        Check("Actor/root references remain alive while student processes evidence", actor.gameObject.activeInHierarchy
            && !SceneTurnover.IsInvestigationBlocked && SOCOCloseout.Instance.CanReportCompletion());
        Check("Reporting no longer depends on the physical IOC", SOCOCloseout.Instance.CanReportCompletion()
            && !actor.IsVisible && !SceneTurnover.TryAdvance(ScenarioPhase.SceneRelease));
        ClearGuidance();
        manager.Evaluate(); manager.RecordResponse("RC-01-PETER", "RC-01-PETER-C"); manager.RecordResponse("RC-02-SCENE", "RC-02-SCENE-B");
        manager.enabled = true; hypothesis.enabled = true; training.enabled = true;
        Check("Paul question still unavailable before knife identification", !vendor.FollowupUnlocked && !vendor.CanAsk);
        var state = EvidenceStateManager.Instance;
        var knife = state.GetRecord("EVD-014");
        // Existing lab identification path, without replaying the separately tested physical kit.
        knife.fingerprintingDone = true; knife.fingerprintLabStatus = FingerprintLabStatus.SentToLab;
        ProceduralTrainingGuidance.Schedule("TRAIN-FINGERPRINT");
        Check("Real lab match confirms Paul and makes the fingerprint checkpoint eligible", state.MarkMatched("EVD-014") == TransitionResult.Applied
            && knife.fingerprintLabMatchResult == "Paul" && manager.IsEligible("RC-03-FINGERPRINT"));
        Check("Identification alone cannot unlock vendor question or release interpretation", !vendor.CanAsk
            && manager.HoldsTraining("TRAIN-FINGERPRINT") && manager.HoldsTraining(ProceduralTrainingGuidance.VendorLeadId)
            && !training.HasScheduled(ProceduralTrainingGuidance.VendorLeadId));
        deadline = Time.realtimeSinceStartupAsDouble + 15;
        while (!ui.IsShowing && Time.realtimeSinceStartupAsDouble < deadline) { ClearGuidance(); yield return null; }
        Check("Fingerprint reasoning prompt appears before investigative hints", ui.IsShowing
            && ui.DisplayedPrompt.Contains(manager.Definitions.checkpoints[2].prompt)
            && !DialogueHistoryManager.Instance.Entries.Any(e => e.sourceId == ProceduralTrainingGuidance.VendorLeadId || e.sourceId == "TRAIN-FINGERPRINT"));
        ui.SelectResponse(0); // Deliberately poor interpretation: the lead must not depend on getting it right.
        Check("Selection alone does not unlock or teach the next step", !manager.IsAnswered("RC-03-FINGERPRINT") && !vendor.FollowupUnlocked);
        ui.ConfirmSelection();
        Check("Confirmation locks the response and unlocks the vendor condition", manager.IsAnswered("RC-03-FINGERPRINT")
            && manager.Records.Single(r => r.CheckpointId == "RC-03-FINGERPRINT").Weight == 1 && vendor.FollowupUnlocked);
        Check("Same-session response cannot be replaced or repeated", !manager.RecordResponse("RC-03-FINGERPRINT", "RC-03-FINGERPRINT-C"));
        ui.AnswerForTest(2); // Finish unchanged HYP-02 justification so explanatory dialogue can run.
        yield return null;
        deadline = Time.realtimeSinceStartupAsDouble + 40;
        while (!DialogueHistoryManager.Instance.Entries.Any(e => e.sourceId == ProceduralTrainingGuidance.VendorLeadId)
            && Time.realtimeSinceStartupAsDouble < deadline)
        {
            NotificationManager.Instance.UI.ShowPrompt("Prepare Body for Removal - Press A");
            SkipVisible(true); yield return null;
        }
        var history = DialogueHistoryManager.Instance.Entries;
        var lesson = history.FirstOrDefault(e => e.sourceId == "TRAIN-FINGERPRINT");
        var lead = history.FirstOrDefault(e => e.sourceId == ProceduralTrainingGuidance.VendorLeadId);
        Check("Existing fingerprint lesson is delivered before the new vendor lead", lesson != null && lead != null && lesson.sequence < lead.sequence);
        Check("Nearby evidence prompt cannot indefinitely delay post-checkpoint guidance", lead != null
            && NotificationManager.Instance.UI.HasAmbientPrompt && STCSNotificationUI.Instance.IsShowing);
        Check("Post-checkpoint lead preserves contact/timing/purpose limits without revealing sighting", lead != null
            && lead.speaker == "Professor" && lead.text.Contains("at some point") && lead.text.Contains("when he touched it")
            && lead.text.Contains("what he did with it") && lead.text.Contains("isn't proof") && lead.text.Contains("vendor")
            && !lead.text.Contains("3:00") && !lead.text.Contains("three in the morning"));
        yield return DrainAll();
        Check("IOC remains retired after fingerprint reasoning and teaching", !actor.IsVisible && actor.gameObject.activeInHierarchy);
        Check("Physical vendor now exposes the existing follow-up", vendor.CanAsk && VendorFollowupTest.OpenAtVendor());
        yield return null;
        Check("Question uses the non-spoiling anyone-else/besides-Peter wording", GameDialogue.Text(VendorFollowupInterview.QuestionId)
            == "Did you see anyone else near John's house besides Peter?");
        Check("Vendor answer begins before its discovery flag is set", vendor.AskAnyoneElse() && !vendor.IsDiscovered);
        yield return VendorFollowupTest.DrainDialogue(vendor);
        Check("Delivered answer records the existing 3AM statement once", vendor.HasDiscovered(VendorFollowupInterview.StatementId)
            && DialogueHistoryManager.Instance.Entries.Count(e => e.sourceId == VendorFollowupInterview.StatementId && e.speaker == "Vendor") == 1);
        Check("Vendor only reports a sighting and explicitly limits further knowledge", GameDialogue.Text(VendorFollowupInterview.AnswerId)
            .Contains("I didn't see where he went after that") && vendor.StatementDetail.Contains("did not witness the attack"));
        var relationships = RelationshipStateManager.Instance;
        var rel = relationships.GetRecord(VendorFollowupInterview.RelationshipId);
        Check("Statement unlocks the unchanged timeline contradiction relationship", rel.state == RelationshipState.Available && rel.definition.isCorrect);
        relationships.MarkSelectedForPair(rel.definition.sourceId, rel.definition.targetId); relationships.CompleteAllSelected();
        Check("Existing semantic relationship completes through normal board-state API", rel.state == RelationshipState.Completed);
        Check("Lead/dialogue cannot repeat the discovery or award procedural score", !vendor.CanAsk && !vendor.AskAnyoneElse()
            && FinalDeductionAssessment.Rules.checkpointWeight == 60 && FinalDeductionAssessment.Rules.relationshipWeight == 25
            && FinalDeductionAssessment.Rules.conclusionWeight == 15);
        Check("Active case roster is still exactly the four current evidence items", state.AllRecords.Select(r => r.definition.evidenceId)
            .OrderBy(x => x).SequenceEqual(new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" }));
        SessionLogger.Instance.Flush();
        var events = SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath);
        Check("Persisted response precedes vendor question and statement discovery", events.Single(e => e.eventType == "ReasoningCheckpointSubmitted" && e.targetId == "RC-03-FINGERPRINT").sequenceNumber
            < events.Single(e => e.eventType == "WitnessFollowupQuestionSelected").sequenceNumber
            && events.Count(e => e.eventType == "WitnessStatementDiscovered") == 1);
        System.IO.Directory.CreateDirectory("docs/evidence/narrative-role-flow");
        System.IO.File.WriteAllText("docs/evidence/narrative-role-flow/dialogue-history.json", JsonUtility.ToJson(new HistoryCapture { text = history.Select(e => e.speaker + ": " + e.text).ToArray() }, true));
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path); yield return null;
        actor = IOC(); workflow = FindAnyObjectByType<SOCOWorkflowInteraction>();
        manager = ReasoningCheckpointManager.Instance; vendor = VendorFollowupInterview.Instance;
        Check("New session clears vendor unlock, discovery and locked fingerprint response", !vendor.FollowupUnlocked && !vendor.IsDiscovered
            && !manager.IsAnswered("RC-03-FINGERPRINT") && manager.Records.Count == 0
            && !ProceduralTrainingGuidance.Instance.HasScheduled(ProceduralTrainingGuidance.VendorLeadId));
        Check("New session resets timeline relationship and learned history", RelationshipStateManager.Instance.GetRecord(VendorFollowupInterview.RelationshipId).state == RelationshipState.Locked
            && !DialogueHistoryManager.Instance.Entries.Any(e => e.sourceId == VendorFollowupInterview.StatementId));
        marker = GameObject.Find("IOC_Start_Ground_Circle");
        Check("New session restores the marker and default guidance settings", marker != null && marker.activeInHierarchy
            && marker.transform.parent == null && workflow.IsIOCStartingMarkerVisible
            && workflow.GuidanceTitle == "Meet the IOC" && workflow.InteractionRadius == 2.5f);
        manager.enabled = false; HypothesisCheckpointManager.Instance.enabled = false; ClearGuidance();
        SceneTurnover.Controller.CompleteWithoutDialogue(); assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var restartedCordon = FindAnyObjectByType<SceneCordon>();
        restartedCordon.PPEStation.PrepareDevelopmentGloves();
        restartedCordon.AccessRegister.PrepareDevelopmentAuthorization();
#endif
        foreach (var observation in assessment.Definition.observations) SceneTurnover.RecordIOCObservation(observation.observationId);
        SceneTurnover.CompleteIOCAssessment(); SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested); ClearGuidance();
        FindAnyObjectByType<SOCOWorkflowInteraction>().AcknowledgeArrival();
        yield return null;
        workflow.enabled = false;
        Check("Disabling during fade clears the view and releases transition state", !workflow.IsArriving && workflow.ArrivalAlpha == 0 && !PlayerUIGate.IsBlockedByOther(null));
        workflow.enabled = true;
        Check("Restart restores the IOC for the next required briefing", SceneTurnover.Phase == ScenarioPhase.SOCOTurnover
            && actor.IsVisible && actor.GetComponent<Collider>().enabled && workflow.IsIOCStartingMarkerVisible);
        Check("Restart restores editable marker transform and guidance approach state", marker.transform.position == authoredMarkerPosition
            && !NotificationManager.Instance.UI.HasAmbientPrompt && workflow.IsWithinIOCInteractionRadius == false);
        Finish();
    }
    [System.Serializable] private class HistoryCapture { public string[] text; }
    private void Finish() { report.AppendLine(failures == 0 ? "=== PASS ===" : "=== " + failures + " FAILURE(S) ==="); Report = report.ToString(); IsDone = true; Debug.Log(Report); }
}
