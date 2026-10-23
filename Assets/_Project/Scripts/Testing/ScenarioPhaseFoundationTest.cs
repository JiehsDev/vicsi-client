using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Seven focused IOC decision/arrival/turnover boundary checks, not a full investigation.</summary>
public class ScenarioPhaseFoundationTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(CheckFoundation()); }
    private IEnumerator CheckFoundation()
    {
        // Checkpoint presentation is tested separately. Synthetic A requires frames
        // between observations, during which eligible checkpoints would own this fixture's UI.
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        var checkpointUI = FindAnyObjectByType<HypothesisCheckpointUI>();
        if (checkpointUI != null) checkpointUI.enabled = false;
        var flow = FindAnyObjectByType<SOCOWorkflowInteraction>();
        var assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        var logger = SessionLogger.Instance;
        var objectives = ObjectiveStateManager.Instance;
        var standingHighlight = assessment != null && assessment.ObservePoint != null
            ? assessment.ObservePoint.Find("IOC_Observe_Standing_Highlight") : null;
        Check("Survey standing hint is hidden until the FR handover, with no obstructing colliders",
            standingHighlight != null && !standingHighlight.gameObject.activeInHierarchy
            && standingHighlight.GetComponentsInChildren<Collider>(true).Length == 0);
        if (flow == null || assessment == null || SceneTurnover.Controller == null)
        { Check("Workflow scene wiring", false); Finish(); yield break; }
        bool initial = SceneTurnover.IsPending && SceneTurnover.PlayerRole == ScenarioPlayerRole.IOC
            && !SceneTurnover.Form1Received && !SceneTurnover.Form2Accomplished && !SceneTurnover.SOCOAssistanceRequested;
        // Exercise the real FR conversation and IOC gaze boundary, without processing evidence.
        var owner = SceneTurnover.Controller;
        var dialogue = STCSNotificationUI.Instance;
        bool handedOver = owner.Interact();
        double turnoverDeadline = Time.realtimeSinceStartupAsDouble + 28d;
        while (SceneTurnover.IsPending && Time.realtimeSinceStartupAsDouble < turnoverDeadline)
        {
            if (dialogue != null && dialogue.IsShowing
                && dialogue.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f) dialogue.Skip();
            yield return null;
        }
        handedOver &= SceneTurnover.Form1Received && SceneTurnover.Phase == ScenarioPhase.IOCAssessment
            && Count(logger, SessionEventType.FirstResponderInteractionStarted) == 1
            && Count(logger, SessionEventType.CSIForm1Received) == 1
            && Count(logger, SessionEventType.SceneTurnoverCompleted) == 1 && !owner.Interact();
        Check("FR handover keeps the floor circle and arrow hidden with waypoint guidance intact",
            standingHighlight != null && !standingHighlight.gameObject.activeInHierarchy
            && assessment.ObservePoint.gameObject.activeInHierarchy
            && assessment.GetComponent<IOCAssessmentGuide>() != null
            && GameDialogue.Text("ioc-assessment-guidance").Contains("Follow the waypoint"));
        var statuses = EvidenceStateManager.Instance.AllRecords.ToDictionary(r => r.definition.evidenceId, r => r.status);
        // Tool-local placeholder props have no evidence ID and can move with the rig.
        // This preservation assertion concerns the actual scene evidence.
        var props = FindObjectsByType<EvidenceProp>().Where(p => !string.IsNullOrEmpty(p.evidenceId)).ToArray();
        var positions = props.ToDictionary(p => p, p => p.transform.position);
        var camera = Camera.main.transform;
        Vector3 oldPosition = camera.position; Quaternion oldRotation = camera.rotation;
        var observePoint = assessment.ObservePoint;
        Vector3 authoredObservePosition = observePoint != null ? observePoint.position : Vector3.zero;
        Quaternion authoredObserveRotation = observePoint != null ? observePoint.rotation : Quaternion.identity;
        Vector3 surveyEye = observePoint != null ? observePoint.position + Vector3.up * 1.6f : new Vector3(-3.8f, 1.6f, 9.5f);
        bool pointConfigured = observePoint != null && assessment.ObserveRadius > 0f
            && assessment.RequireFacing && assessment.FacingAngle > 0f && assessment.ObservePromptText == "A – Observe";
        if (observePoint != null)
        {
            camera.SetPositionAndRotation(observePoint.position + Vector3.up * 1.6f + observePoint.forward * (assessment.ObserveRadius + .5f),
                Quaternion.LookRotation(observePoint.forward));
            bool outsideRejected = assessment.ObservationAtGaze() != "ioc-main-scene-survey";
            camera.SetPositionAndRotation(surveyEye, Quaternion.LookRotation(-observePoint.forward));
            bool wrongFacingRejected = assessment.ObservationAtGaze() != "ioc-main-scene-survey";
            camera.SetPositionAndRotation(surveyEye, Quaternion.LookRotation(observePoint.forward));
            pointConfigured &= outsideRejected && wrongFacingRejected;
        }
        else camera.SetPositionAndRotation(surveyEye, Quaternion.LookRotation(assessment.Definition.surveyLookAt - surveyEye));
        bool pointUnchanged = observePoint == null || observePoint.position == authoredObservePosition;
        var body = props.First(p => p.evidenceId == "BODY-A");
        Vector3 bodySightTarget = body.transform.position + Vector3.up * .2f;
        Physics.SyncTransforms();
        bool bodySightClear = !Physics.RaycastAll(surveyEye, (bodySightTarget - surveyEye).normalized,
            Vector3.Distance(surveyEye, bodySightTarget), ~0, QueryTriggerInteraction.Ignore)
            .Any(hit => !hit.transform.IsChildOf(body.transform));
        Check("Rear-kitchen observation point has a clear forward body sightline and preserves interaction settings",
            observePoint != null && assessment.Definition.surveyArea.Contains(authoredObservePosition + Vector3.up * 1.2f)
            && Vector3.Angle(observePoint.forward, Vector3.ProjectOnPlane(bodySightTarget - surveyEye,Vector3.up)) < 30f && bodySightClear
            && Mathf.Approximately(assessment.ObserveRadius, 1.5f) && assessment.RequireFacing
            && Mathf.Approximately(assessment.FacingAngle, 45f));
        Check("Scene-survey observation explains preservation and glove limits without added findings",
            assessment.Definition.Find("ioc-main-scene-survey").dialogueId == "ioc-observation-survey"
            && GameDialogue.Text("ioc-observation-survey").Contains("body appears undisturbed")
            && GameDialogue.Text("ioc-observation-survey").Contains("Gloves don't prevent all contamination")
            && GameDialogue.Text("ioc-observation-survey").Contains("in place for SOCO"));
        double promptDeadline = Time.realtimeSinceStartupAsDouble + 18d;
        while (STCSNotificationUI.Instance != null
            && (STCSNotificationUI.Instance.IsShowing || STCSNotificationUI.Instance.PendingCount > 0)
            && Time.realtimeSinceStartupAsDouble < promptDeadline)
        {
            if (STCSNotificationUI.Instance.IsShowing
                && STCSNotificationUI.Instance.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f)
                STCSNotificationUI.Instance.Skip();
            yield return null;
        }
        yield return null;
        bool promptTextEditable = assessment.CurrentPrompt == assessment.ObservePromptText
            && NotificationManager.Instance.UI.CurrentAmbientPrompt == assessment.ObservePromptText;
        bool observed = SceneTurnover.IOCObservations.Count == 0
            && assessment.ObservationAtGaze() == "ioc-main-scene-survey";
        Check("IOC Observe owns A at its actionable observation point", PlayerUIGate.ConfirmOwnerToken == assessment);
        UnityEngine.InputSystem.InputSystem.RegisterLayout("{\"name\":\"ViCSIObserveController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\"}]}");
        var observeDevice = UnityEngine.InputSystem.InputSystem.AddDevice("ViCSIObserveController");
        UnityEngine.InputSystem.InputSystem.SetDeviceUsage(observeDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
        UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent((UnityEngine.InputSystem.Controls.ButtonControl)observeDevice["primaryButton"], true);
        yield return null; yield return null;
        observed &= SceneTurnover.IOCObservations.Contains("ioc-main-scene-survey");
        Check("Real observation input hides the standing highlight after recording the survey",
            SceneTurnover.IOCObservations.Contains("ioc-main-scene-survey")
            && standingHighlight != null && !standingHighlight.gameObject.activeInHierarchy);
        Check("Actual XR A records IOC observation and consumes the held press against jump", observed && PlayerUIGate.SuppressGameplayJump);
        UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent((UnityEngine.InputSystem.Controls.ButtonControl)observeDevice["primaryButton"], false);
        yield return null;
        UnityEngine.InputSystem.InputSystem.RemoveDevice(observeDevice);
        UnityEngine.InputSystem.InputSystem.RemoveLayout("ViCSIObserveController");
        bool surveyCompleted = SceneTurnover.IOCObservations.Contains("ioc-main-scene-survey")
            && assessment.ObservationAtGaze() != "ioc-main-scene-survey" && pointUnchanged && assessment.CurrentPrompt == null;
        Check("Editor-configured IOC observation point, radius, facing gate and editable A – Observe prompt are active",
            pointConfigured && pointUnchanged && promptTextEditable);
        Check("Observation prompt is unavailable outside the point radius or when facing away, then records and clears the scene-survey objective",
            pointConfigured && observed && surveyCompleted);
        foreach (string id in new[] { "ioc-victim", "ioc-possible-weapon", "ioc-drinking-context" })
        {
            var item = assessment.Definition.Find(id);
            Vector3 target = props.First(p => p.evidenceId == item.evidenceId).transform.position + item.worldOffset;
            Vector3 eye = new Vector3(target.x, 1.6f, target.z - 1.4f);
            camera.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            string selected = assessment.ObservationAtGaze();
            bool recorded = selected == id && assessment.ObserveAtGaze();
            report.AppendLine("INFO: " + id + "; selected=" + selected + "; recorded=" + recorded
                + "; UI blocked=" + PlayerUIGate.IsBlocked);
            observed &= recorded;
        }
        camera.SetPositionAndRotation(oldPosition, oldRotation);
        EvidenceStateManager.Instance.MarkFound("EVD-014", default);
        report.AppendLine("INFO: statuses unchanged=" + EvidenceStateManager.Instance.AllRecords.All(r => statuses[r.definition.evidenceId] == r.status)
            + "; props unchanged=" + props.All(p => positions[p] == p.transform.position)
            + "; observation events=" + Count(logger, SessionEventType.IOCObservationRecorded));
        foreach (var prop in props)
            report.AppendLine("INFO: evidence drift " + prop.evidenceId + "=" + Vector3.Distance(positions[prop], prop.transform.position).ToString("F6") + " m");
        observed &= EvidenceStateManager.Instance.AllRecords.All(r => statuses[r.definition.evidenceId] == r.status)
            && props.All(p => positions[p] == p.transform.position)
            && Count(logger, SessionEventType.IOCObservationRecorded) == 4
            && !SceneTurnover.RecordIOCObservation("ioc-victim") && Gated();
        bool completedAssessment = SceneTurnover.CompleteIOCAssessment();
        bool prepared = handedOver && observed && completedAssessment;
        report.AppendLine("INFO: handover=" + handedOver + "; observed=" + observed + "; ready=" + SceneTurnover.AssessmentReady
            + "; records=" + string.Join(",", SceneTurnover.IOCObservations));
        yield return null;
        Check("IOC startup, real FR/Form 1 handover and intentional IOC assessment preserve evidence and reach the decision with SOCO locked",
            initial && prepared && SceneTurnover.Phase == ScenarioPhase.IOCSOCODecision && flow.IsPanelVisible
            && objectives.GetRecord("obj-ioc-soco-decision").state == ObjectiveState.Active && Gated());

        int guidance = 0;
        System.Action<DialogueRequest> feedback = r => { if (r.id == "soco-decision-reconsider") guidance++; };
        GameDialogue.OnRequested += feedback;
        string clicked = Click(flow, "continue-without-soco");
        Click(flow, "continue-without-soco");
        GameDialogue.OnRequested -= feedback;
        Check("Actual choice pointer rejects Continue without SOCO, offers reconsideration, logs that choice once and leaves gates intact",
            clicked == "continue-without-soco" && guidance == 2
            && Count(logger, SessionEventType.SOCOAssistanceDecision) == 1
            && SceneTurnover.Phase == ScenarioPhase.IOCSOCODecision && !SceneTurnover.SOCOAssistanceRequested && Gated());

        clicked = Click(flow, "request-soco");
        Check("Request choice advances only to SOCORequested, records decision/request once and activates Await arrival",
            clicked == "request-soco" && SceneTurnover.Phase == ScenarioPhase.SOCORequested
            && SceneTurnover.SOCOAssistanceRequested && !SceneTurnover.Form2Accomplished && Gated()
            && Count(logger, SessionEventType.SOCOAssistanceDecision) == 2
            && Count(logger, SessionEventType.SOCORequested) == 1
            && objectives.IsCompleted("obj-ioc-soco-decision")
            && objectives.GetRecord("obj-await-soco-arrival").state == ObjectiveState.Active
            && !flow.Choose(true) && !flow.BeginBriefing());
        var rig = Camera.main.GetComponentInParent<OVRCameraRig>().transform;
        Vector3 entrance = rig.position;
        Quaternion entranceRotation = rig.rotation;
        rig.position += new Vector3(-2f, 0f, 10f);
        rig.rotation = entranceRotation * Quaternion.Euler(0f, 45f, 0f);
        double arrivalDeadline = Time.realtimeSinceStartupAsDouble + 5d;
        while (SceneTurnover.Phase == ScenarioPhase.SOCORequested && Time.realtimeSinceStartupAsDouble < arrivalDeadline)
        {
            prepared &= Gated();
            yield return null;
        }
        while (flow.IsArriving) yield return null;
        bool arrived = SceneTurnover.Phase == ScenarioPhase.SOCOTurnover && prepared;
        report.AppendLine("INFO: arrival=" + arrived + "; rig=" + rig.position + "; expected entrance=" + entrance
            + "; phase=" + SceneTurnover.Phase + "; form2=" + SceneTurnover.Form2Accomplished + "; gated=" + Gated()
            + "; await=" + objectives.GetRecord("obj-await-soco-arrival").state
            + "; briefing objective=" + objectives.GetRecord("obj-receive-ioc-briefing").state
            + "; cordon=" + FindAnyObjectByType<SceneCordon>().CordonVisible);
        var arrivalCapsule = FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        Vector3 actualFeet = arrivalCapsule.Pose.position - Vector3.up * arrivalCapsule.Height * .5f;
        report.AppendLine("INFO: arrived feet=" + actualFeet + "; authored feet=" + flow.ArrivalFeetAnchor.position
            + "; arrival yaw error=" + Quaternion.Angle(rig.rotation, flow.ArrivalFeetAnchor.rotation));
        Check("Automatic arrival completes Form 2 once, restores entrance yaw at a grounded destination and enters locked IOC-role SOCOTurnover",
            arrived && Vector3.Distance(actualFeet, flow.ArrivalFeetAnchor.position) < .15f
            && Quaternion.Angle(rig.rotation, flow.ArrivalFeetAnchor.rotation) < .1f
            && !flow.AcknowledgeArrival()
            && SceneTurnover.Phase == ScenarioPhase.SOCOTurnover && SceneTurnover.Form2Accomplished && Gated()
            && Count(logger, SessionEventType.CSIForm2Completed) == 1
            && Count(logger, SessionEventType.SOCOTurnoverStarted) == 1
            && objectives.IsCompleted("obj-await-soco-arrival")
            && objectives.GetRecord("obj-receive-ioc-briefing").state == ObjectiveState.Active
            && FindAnyObjectByType<SceneCordon>().CordonVisible && !FindAnyObjectByType<SceneCordon>().EntranceIsOpen);

        int started = 0;
        // The authored ground marker is now the briefing interaction anchor. Arrival
        // restores the entrance; the student must still approach the marker.
        var briefingMarker = GameObject.Find("IOC_Start_Ground_Circle");
        if (briefingMarker != null)
            rig.position = new Vector3(briefingMarker.transform.position.x, rig.position.y, briefingMarker.transform.position.z);
        yield return null;
        Check("Student approaches the authored IOC marker before briefing", flow.IsWithinIOCInteractionRadius);
        System.Action activated = () => started++;
        SceneTurnover.OnInvestigationStarted += activated;
        bool briefing = flow.BeginBriefing() && flow.IsTalking && !flow.BeginBriefing() && Gated();
        var panel = STCSNotificationUI.Instance;
        var seen = new System.Collections.Generic.HashSet<string>();
        double deadline = Time.realtimeSinceStartupAsDouble + 28d;
        while (flow.IsTalking && Time.realtimeSinceStartupAsDouble < deadline)
        {
            briefing &= Gated();
            if (panel != null && panel.IsShowing)
            {
                foreach (string id in flow.BriefingLineIds)
                    if (panel.CurrentText.Contains(GameDialogue.Text(id))) seen.Add(id);
                if (panel.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + .05f) panel.Skip();
            }
            yield return null;
        }
        yield return null;
        SceneTurnover.OnInvestigationStarted -= activated;
        var history = DialogueHistoryManager.Instance;
        Check("Complete three-line IOC briefing displays/skips normally with SOCO locked throughout, records history once and enters investigation once",
            briefing && seen.Count == 3 && !flow.IsTalking && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation
            && history.Entries.Count(e => e.sourceId == SOCOWorkflowInteraction.BriefingSourceId) == 3
            && flow.BriefingLineIds.All(id => history.Entries.Count(e => e.text == GameDialogue.Text(id)) == 1)
            && Count(logger, SessionEventType.SOCOTurnoverCompleted) == 1 && started == 1);

        var photos = ScenePhotographyManager.Instance;
        // Preserve the authored two-second first-instruction delay rather than changing the route.
        double photoDeadline = Time.realtimeSinceStartupAsDouble + 4d;
        while (photos.CurrentPoint == null && Time.realtimeSinceStartupAsDouble < photoDeadline) yield return null;
        report.AppendLine("INFO: SOCO role=" + SceneTurnover.PlayerRole + "; tools locked=" + ToolInputLock.IsLocked
            + "; timer=" + SceneTurnover.InvestigationSeconds + "; photo=" + (photos.CurrentPoint != null ? photos.CurrentPoint.photoPointId : "null")
            + "; marker=" + (photos.CurrentPoint != null && photos.CurrentPoint.IsZoneVisible)
            + "; body objective=" + objectives.GetRecord("obj-document-body").state
            + "; evidence=" + string.Join(",", EvidenceStateManager.Instance.AllRecords.Select(r => r.definition.evidenceId + ":" + r.status)));
        Check("SOCO boundary changes role, unlocks tools, activates the original first photo marker/objectives and starts timing without processing evidence",
            SceneTurnover.PlayerRole == ScenarioPlayerRole.SOCO && !SceneTurnover.IsInvestigationBlocked && !ToolInputLock.IsLocked
            && SceneTurnover.Form2Accomplished && SceneTurnover.InvestigationStarted && SceneTurnover.InvestigationSeconds > 0f
            && photos.CurrentPoint != null && photos.CurrentPoint == photos.Points.OrderBy(p => p.sequenceIndex).First()
            && photos.CurrentPoint.IsZoneVisible == !photos.CurrentPoint.ContainsPlayer(Camera.main.transform.position) && EvidenceStateManager.Instance.AllRecords.All(r => r.status == EvidenceStatus.NotFound)
            && objectives.IsCompleted("obj-receive-ioc-briefing")
            && objectives.GetRecord("obj-document-body").state == ObjectiveState.Active
            && Count(logger, SessionEventType.SOCOInvestigationStarted) == 1
            && !flow.BeginBriefing() && !SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation)
            && FindAnyObjectByType<SceneCordon>().CordonVisible);

        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        yield return null;
        var restartedAssessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        var restartedHighlight = restartedAssessment.ObservePoint.Find("IOC_Observe_Standing_Highlight");
        Check("Scene restart resets the standing hint for the next FR handover",
            restartedHighlight != null && !restartedHighlight.gameObject.activeInHierarchy
            && !SceneTurnover.IOCObservations.Contains("ioc-main-scene-survey"));
        Check("Restart restores the relocated observation point and unrecorded survey objective",
            restartedAssessment != null && restartedAssessment.ObservePoint != null
            && restartedAssessment.ObservePoint.position == authoredObservePosition
            && Quaternion.Angle(restartedAssessment.ObservePoint.rotation, authoredObserveRotation) < .001f
            && !SceneTurnover.IOCObservations.Contains("ioc-main-scene-survey"));
        Check("Fresh-session reload restores IOC turnover and clears request/Form 2/assessment/timer with SOCO locked",
            SceneTurnover.IsPending && SceneTurnover.PlayerRole == ScenarioPlayerRole.IOC && SceneTurnover.Form1Prepared
            && !SceneTurnover.Form1Received && !SceneTurnover.Form2Accomplished && !SceneTurnover.SOCOAssistanceRequested
            && SceneTurnover.IOCObservations.Count == 0 && Gated()
            && !FindAnyObjectByType<SOCOWorkflowInteraction>().IsPanelVisible
            && !FindAnyObjectByType<SOCOWorkflowInteraction>().IsTalking
            && Count(SessionLogger.Instance, SessionEventType.SOCORequested) == 0
            && Count(SessionLogger.Instance, SessionEventType.CSIForm2Completed) == 0
            && ObjectiveStateManager.Instance.GetRecord("obj-talk-first-responder").state == ObjectiveState.Active
            && !FindAnyObjectByType<SceneCordon>().EntranceIsOpen);
        Finish();
    }
    private static string Click(SOCOWorkflowInteraction flow, string id)
    {
        Vector3 point = flow.Pointer.WorldCentre(id);
        var panel = flow.Pointer.Targets.First(t => t.id == id).rect;
        return flow.Pointer.ClickWithRay(new Ray(point - panel.forward * .5f, panel.forward));
    }
    private static bool Gated() => SceneTurnover.PlayerRole == ScenarioPlayerRole.IOC
        && SceneTurnover.IsInvestigationBlocked && ToolInputLock.IsLocked && !SceneTurnover.InvestigationStarted
        && SceneTurnover.InvestigationSeconds == 0f && ScenePhotographyManager.Instance.CurrentPoint == null
        && ScenePhotographyManager.Instance.Points.All(p => !p.IsZoneVisible);
    private static int Count(SessionLogger logger, SessionEventType type) =>
        logger != null && logger.EventCounts.TryGetValue(type, out int count) ? count : 0;
    private void Check(string name, bool pass)
    { if (!pass) failures++; report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); }
    private void Finish()
    {
        report.AppendLine(failures == 0 ? "=== PASS: 7 focused IOC SOCO transition checks ===" : "=== FAIL: IOC SOCO transition ===");
        Report = report.ToString(); IsDone = true; Debug.Log(Report);
    }
}
