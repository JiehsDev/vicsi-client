using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Procedural closeout; projects existing evidence/custody state, never processes evidence.</summary>
[DefaultExecutionOrder(-110)]
public sealed class SOCOCloseout : MonoBehaviour
{
    public static SOCOCloseout Instance { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentInventory(bool includeFingerprints = false)
    {
        SceneTestStart.RequirePreparation();
        var tape = FindFirstObjectByType<FingerprintTapeTool>(FindObjectsInactive.Include);
        EvidenceInventory.Instance.PrepareDevelopmentStart(completedSamplePrefab, includeFingerprints ? tape?.DevelopmentLiftedVisual : null);
    }
#endif
    [SerializeField] private EvidenceDefinition[] requiredEvidence;
    [SerializeField] private GameObject closedBagPrefab;
    [SerializeField] private GameObject completedSamplePrefab;
    [SerializeField] private Transform ioc;
    [SerializeField] private SceneCordon cordon;
    [SerializeField] private Bounds finalSurveyArea = new Bounds(new Vector3(-2f, 1.5f, 10f), new Vector3(4f, 3f, 3f));
    [SerializeField] private Vector3 finalSurveyLookAt = new Vector3(-3f, .6f, 12.8f);
    [SerializeField] private float iocRange = 2.25f;
    private InputAction accept, close, utility;
    private GameObject panel, objectivePanel;
    private TMP_Text title, body, actionLabel, objective;
    private WorldPanelPointer pointer;
    private string prompt;
    private bool queuedReview, iocQuestionResolved, talking, holdingUI;
    private int closedFrame = -1;
    private float inputAfter;
    private ScenarioPhase? authorizedTransition;
    public bool IsShowing => panel != null && panel.activeSelf;
    public bool IsTalking => talking || (IsShowing && SceneTurnover.Phase == ScenarioPhase.IOCCompletionReport);
    public bool InventoryConfirmed => SceneTurnover.HasReachedPhase(ScenarioPhase.FinalSceneSurvey);
    public bool FinalSurveyCompleted => SceneTurnover.HasReachedPhase(ScenarioPhase.IOCCompletionReport);
    public Vector3 FinalSurveyPosition => finalSurveyArea.center;
    public bool IOCConfirmed => SceneTurnover.HasReachedPhase(ScenarioPhase.SceneRelease);
    public bool ReleaseCompleted { get; private set; }
    public bool AllAccountedFor => requiredEvidence != null && requiredEvidence.Length > 0
        && requiredEvidence.All(d => d != null && EvidenceComplete(d))
        && EvidenceInventory.Instance != null && EvidenceInventory.Instance.CurrentHeldEvidenceId == null;
    public bool IsObjectiveVisible => objectivePanel != null && objectivePanel.activeInHierarchy;
    public string ObjectiveGuidance => objective != null ? objective.text : null;
    public static string SuggestedInvestigationMenuEntry
    {
        get
        {
            if (Instance == null || !Instance.isActiveAndEnabled || SceneTestStart.IsPreparing || SceneTurnover.IsDeductionBlocked
                || !Instance.AllAccountedFor || VendorFollowupInterview.CompletionBlockReason != null) return null;
            var review = EvidenceRelationshipsReview.Instance;
            if (review == null || review.EligibleQuestions().Count == 0) return null;
            if (!review.IsReadyForConclusion) return "Relationships";
            if (CrimeSceneSketchManager.CompletionBlockReason != null) return "Scene Sketch";
            return "Submit Conclusion";
        }
    }
    public string ReviewText => BuildInventoryText();
    public bool CustodyPassComplete => EvidenceBoxDepositZone.Instance?.PassComplete == true;
    public void NotifyCustodyPassCompleted() { queuedReview = true; }
    private bool DialogueShowing => STCSNotificationUI.Instance != null && STCSNotificationUI.Instance.IsShowing;
    private bool WorldInputAvailable => !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners && !DialogueShowing
        && closedFrame != Time.frameCount;
    internal bool IsTransitionAuthorized(ScenarioPhase phase) => authorizedTransition == phase;
    private bool Advance(ScenarioPhase phase)
    {
        authorizedTransition = phase;
        try { return SceneTurnover.TryAdvance(phase); }
        finally { authorizedTransition = null; }
    }

    private void Awake()
    {
        Instance = this;
        accept = PlayerUIGate.CreateConfirmAction(this, "Closeout_A", () => IsShowing || (WorldInputAvailable && prompt != null), 600);
        close = new InputAction("Closeout_B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        utility = new InputAction("Closeout_XGuard", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        BuildUI();
    }
    private void OnEnable()
    {
        accept.Enable(); close.Enable(); utility.Enable();
        SceneTurnover.OnPhaseChanged += PhaseChanged;
    }
    private void OnDisable()
    {
        SceneTurnover.OnPhaseChanged -= PhaseChanged;
        HidePanel(); ClearPrompt(); objectivePanel.SetActive(false);
        StopAllCoroutines(); talking = false;
        accept.Disable(); close.Disable(); utility.Disable();
    }
    private void OnDestroy()
    {
        accept.Dispose(); close.Dispose(); utility.Dispose(); pointer.Dispose();
        if (Instance == this) Instance = null;
    }

    public bool SampleComplete(EvidenceDefinition source)
    {
        var inventory = EvidenceInventory.Instance;
        var record = source != null ? EvidenceStateManager.Instance?.GetRecord(source.evidenceId) : null;
        if (record == null || inventory == null || !record.swabbingDone || !record.swabSampleSealed || completedSamplePrefab == null) return false;
        string sampleId = source.collectedBySwab ? source.evidenceId : "blood-sample:" + source.evidenceId;
        return inventory.GetSampleSource(sampleId) == source.evidenceId && inventory.IsSampleSealed(sampleId)
            && inventory.IsExplicitlyStoredSample(sampleId)
            && inventory.GetDefinition(sampleId)?.heldPrefab == completedSamplePrefab && inventory.IsSecuredCustody(sampleId);
    }
    public bool EvidenceComplete(EvidenceDefinition definition)
    {
        var state = EvidenceStateManager.Instance;
        var record = definition != null ? state?.GetRecord(definition.evidenceId) : null;
        if (record == null) return false;
        if (definition.examinedInPlace) return record.status == EvidenceStatus.Logged
            && (!definition.requiresBodyRemoval || record.bodyRemovalStatus == BodyRemovalStatus.Removed);
        if (record.status != EvidenceStatus.Processed) return false;
        if (definition.requiresSwabbing && !SampleComplete(definition)) return false;
        if (definition.requiresFingerprinting && (!record.fingerprintingDone || record.fingerprintLabStatus != FingerprintLabStatus.Matched)) return false;
        if (definition.collectedBySwab) return SampleComplete(definition);
        var inventory = EvidenceInventory.Instance;
        return inventory != null && closedBagPrefab != null && definition.heldPrefab == closedBagPrefab
            && inventory.IsBaggedEvidence(definition.evidenceId) && inventory.IsExplicitlyStoredBag(definition.evidenceId)
            && inventory.IsSecuredCustody(definition.evidenceId);
    }
    public string MissingFeedback()
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return ProcedureInterlock.Reason;
        if (EvidenceInventory.Instance?.CurrentHeldEvidenceId != null) return "Store the item in your hand before transferring evidence to the box.";
        if (requiredEvidence == null || requiredEvidence.Length == 0) return GameDialogue.Text("closeout-not-ready");
        foreach (var definition in requiredEvidence)
        {
            if (definition == null) return GameDialogue.Text("closeout-not-ready");
            if (!EvidenceComplete(definition))
            {
                var map = EvidenceProcedureMapResolver.ResolveMap(definition.evidenceId);
                if (map.current != null)
                    return definition.PlayerDisplayName + ": " + (map.current.subStep ?? map.current.guidance ?? map.current.displayName);
                if (definition.requiresSwabbing && !SampleComplete(definition))
                    return "Seal and explicitly store the blood evidence from " + definition.PlayerDisplayName + ".";
                if (definition.requiresFingerprinting && !EvidenceStateManager.Instance.IsFingerprintMatched(definition.evidenceId))
                    return "Complete the fingerprint comparison for " + definition.PlayerDisplayName + ".";
                return "Complete packaging and storage for " + definition.PlayerDisplayName + ".";
            }
        }
        return EvidenceInventory.Instance?.CurrentHeldEvidenceId != null ? GameDialogue.Text("closeout-store-held") : null;
    }
    public bool TryReviewInventory()
    {
        if (SceneTurnover.Phase != ScenarioPhase.SOCOCloseout || !WorldInputAvailable) return false;
        if (!AllAccountedFor) { GameDialogue.ShowText(MissingFeedback()); return false; }
        return SceneTurnover.TryAdvance(ScenarioPhase.EvidenceInventoryReview);
    }
    public void RequestInventoryReview()
    {
        if (SceneTurnover.Phase == ScenarioPhase.SOCOCloseout) TryReviewInventory(); else ShowInventoryReview();
    }
    public bool ShowInventoryReview()
    {
        if (SceneTurnover.Phase != ScenarioPhase.EvidenceInventoryReview || !CustodyPassComplete || IsShowing || !WorldInputAvailable) return false;
        ShowPanel("closeout-inventory-title", BuildInventoryText(), "closeout-inventory-confirm");
        return true;
    }
    public bool ConfirmInventory()
    {
        if (!IsShowing || SceneTurnover.Phase != ScenarioPhase.EvidenceInventoryReview || PlayerUIGate.IsBlockedByOther(this)) return false;
        if (!AllAccountedFor || !CustodyPassComplete) { body.text = BuildInventoryText(); return false; }
        HidePanel();
        return Advance(ScenarioPhase.FinalSceneSurvey);
    }
    public bool SurveyAvailable()
    {
        var head = Camera.main;
        if (head == null || !finalSurveyArea.Contains(head.transform.position)) return false;
        return Vector3.Angle(head.transform.forward, finalSurveyLookAt - head.transform.position) <= 35f;
    }
    public bool CompleteFinalSurvey()
    {
        if (SceneTurnover.Phase != ScenarioPhase.FinalSceneSurvey || !WorldInputAvailable || !SurveyAvailable() || !AllAccountedFor || !CustodyPassComplete) return false;
        AcquireUI();
        bool advanced = Advance(ScenarioPhase.IOCCompletionReport);
        StartCoroutine(ReleaseSurveyInput());
        return advanced;
    }
    private IEnumerator ReleaseSurveyInput() { yield return null; ReleaseUI(); closedFrame = Time.frameCount; }
    public bool CanReportCompletion()
    {
        // Student owns the role after handoff; completion is a guided report, not an
        // approach to the retired opening actor. Serialized IOC references remain intact.
        return Camera.main != null && SceneTurnover.HasReachedPhase(ScenarioPhase.SOCOInvestigation);
    }
    public bool PlayerNearIOC() => CanReportCompletion(); // historical caller compatibility
    public bool BeginIOCReport()
    {
        if (SceneTurnover.Phase != ScenarioPhase.IOCCompletionReport || !WorldInputAvailable || !CanReportCompletion()
            || !AllAccountedFor || !CustodyPassComplete || talking || IsShowing || STCSNotificationUI.Instance == null) return false;
        talking = true; iocQuestionResolved = false; ClearPrompt();
        AcquireUI();
        GameDialogue.SayLine("closeout-ioc-question", "Professor", "soco-closeout-ioc");
        StartCoroutine(WaitForIOCQuestion());
        return true;
    }
    private IEnumerator WaitForIOCQuestion()
    {
        yield return null;
        while (STCSNotificationUI.Instance != null && STCSNotificationUI.Instance.HasLinesFrom("soco-closeout-ioc")) yield return null;
        talking = false;
        ReleaseUI();
        if (SceneTurnover.Phase == ScenarioPhase.IOCCompletionReport && WorldInputAvailable && CanReportCompletion())
        {
            iocQuestionResolved = true;
            ShowPanel("closeout-ioc-heading", GameDialogue.Text("closeout-ioc-question"), "closeout-ioc-confirm");
        }
    }
    public bool ConfirmIOCReport()
    {
        if (!IsShowing || !iocQuestionResolved || SceneTurnover.Phase != ScenarioPhase.IOCCompletionReport
            || PlayerUIGate.IsBlockedByOther(this) || !CanReportCompletion() || !AllAccountedFor || !CustodyPassComplete) return false;
        HidePanel();
        return Advance(ScenarioPhase.SceneRelease);
    }
    private void PhaseChanged(ScenarioPhase phase)
    {
        ClearPrompt();
        if (phase == ScenarioPhase.EvidenceInventoryReview) queuedReview = true;
        if (phase == ScenarioPhase.IOCCompletionReport) GameDialogue.SayLine("closeout-survey-done", "Professor", "soco-final-survey");
        if (phase == ScenarioPhase.SceneRelease)
        {
            GameDialogue.SayLine("closeout-ioc-release", "Professor", "soco-scene-release");
            StartCoroutine(FinishRelease());
        }
    }
    private IEnumerator FinishRelease()
    {
        yield return null;
        while (DialogueShowing || PlayerUIGate.IsBlocked) yield return null;
        if (SceneTurnover.Phase != ScenarioPhase.SceneRelease || cordon == null || !IOCConfirmed) yield break;
        // Administrative closeout completes the session; the secured cordon stays in place.
        ReleaseCompleted = true;
        if (ReleaseCompleted) SceneTurnover.TryAdvance(ScenarioPhase.InvestigationComplete);
    }
    private void Update()
    {
        var phase = SceneTurnover.Phase;
        RefreshObjective();
        if (IsShowing)
        {
            ClearPrompt();
            if (phase == ScenarioPhase.IOCCompletionReport && !CanReportCompletion()) { HidePanel(); return; }
            if (PlayerUIGate.IsBlockedByOther(this) || close.WasPressedThisFrame()) { HidePanel(); return; }
            if (phase == ScenarioPhase.EvidenceInventoryReview) body.text = BuildInventoryText();
            pointer.Tick();
            if (Time.unscaledTime >= inputAfter && PlayerUIGate.TryConsumeConfirm(this, accept)) ConfirmPanel();
            return;
        }
        if (!WorldInputAvailable || utility.WasPressedThisFrame()) { ClearPrompt(); return; }
        if (phase == ScenarioPhase.SOCOCloseout && AllAccountedFor) { TryReviewInventory(); return; }
        if (queuedReview && phase == ScenarioPhase.EvidenceInventoryReview) { queuedReview = false; ShowInventoryReview(); return; }
        string nextPrompt = phase == ScenarioPhase.EvidenceInventoryReview && CustodyPassComplete ? GameDialogue.Text("closeout-review-prompt")
            : phase == ScenarioPhase.FinalSceneSurvey && SurveyAvailable() ? GameDialogue.Text("closeout-survey-prompt")
            : phase == ScenarioPhase.IOCCompletionReport && CanReportCompletion() && !talking ? GameDialogue.Text("closeout-ioc-prompt") : null;
        if (prompt != nextPrompt)
        {
            ClearPrompt(); prompt = nextPrompt;
            if (prompt != null) NotificationManager.ShowPrompt(prompt, new Vector3(0, -.16f, 0));
        }
        if (PlayerUIGate.TryConsumeConfirm(this, accept) && prompt != null)
        {
            if (phase == ScenarioPhase.EvidenceInventoryReview) ShowInventoryReview();
            else if (phase == ScenarioPhase.FinalSceneSurvey) CompleteFinalSurvey();
            else BeginIOCReport();
        }
    }
    public void RefreshObjective()
    {
        var phase = SceneTurnover.Phase;
        objective.text = ObjectiveText(phase);
        bool relevant = !string.IsNullOrEmpty(objective.text);
        var notifications = NotificationManager.Instance?.UI;
        objectivePanel.SetActive(relevant && !SceneTestStart.IsPreparing && !PlayerUIGate.IsBlocked
            && !ToolInputLock.HasUIOwners && !DialogueShowing
            && (STCSNotificationUI.Instance == null || STCSNotificationUI.Instance.PendingCount == 0)
            && (notifications == null || string.IsNullOrEmpty(notifications.CurrentTooltip)));
    }
    public static string ObjectiveText(ScenarioPhase phase)
    {
        if (phase == ScenarioPhase.SOCOInvestigation)
        {
            string next = SuggestedInvestigationMenuEntry;
            if (next == null) return null;
            string heading = next == "Relationships" ? "Next: Link evidence" : next == "Scene Sketch" ? "Next: Submit your scene sketch" : "Next: Submit your conclusion";
            string action = next == "Relationships" ? "Review how your collected findings connect.\n" : "";
            string menuLabel = MenuWheelComfortProfile.Label(next).Replace("\n", " ");
            return "<b>" + heading + "</b>\n" + action + "Hold [X], select <b>" + menuLabel + "</b> with the right stick, then release [X].";
        }
        if (phase == ScenarioPhase.EvidenceInventoryReview && Instance?.CustodyPassComplete != true)
            return "Take your stored evidence to the evidence box and confirm the custody pass.";
        string id = phase switch
        {
            ScenarioPhase.SOCOCloseout => "closeout-objective-processing",
            ScenarioPhase.EvidenceInventoryReview => "closeout-objective-inventory",
            ScenarioPhase.FinalSceneSurvey => "closeout-objective-survey",
            ScenarioPhase.IOCCompletionReport => "closeout-objective-report",
            _ => null
        };
        return id != null ? GameDialogue.Text(id) : string.Empty;
    }
    private string BuildInventoryText()
    {
        var text = new StringBuilder();
        if (requiredEvidence == null) return string.Empty;
        foreach (var definition in requiredEvidence)
        {
            if (definition == null) continue;
            bool done = EvidenceComplete(definition);
            string state = !done ? "closeout-status-pending" : definition.examinedInPlace ? "closeout-status-body"
                : definition.collectedBySwab ? "closeout-status-sampled" : "closeout-status-stored";
            if (done && definition.requiresBodyRemoval)
            {
                text.AppendLine("<b>" + definition.PlayerDisplayName
                    + "</b>\nDocumented; prepared for medico-legal transport and removed.");
                continue;
            }
            text.AppendLine("<b>" + definition.PlayerDisplayName + "</b>\n" + GameDialogue.Text(state));
            if (definition.requiresSwabbing)
            {
                text.AppendLine("<b>" + GameDialogue.Text("closeout-sample-name", definition.PlayerDisplayName) + "</b>\n"
                    + GameDialogue.Text(SampleComplete(definition) ? "closeout-status-stored" : "closeout-status-pending"));
            }
        }
        var inventory = EvidenceInventory.Instance;
        if (inventory != null)
        {
            text.AppendLine("\n<b>Evidence custody</b>");
            foreach (var id in inventory.AllTrackedEntryIds)
                text.AppendLine(inventory.GetDefinition(id).PlayerDisplayName + " — "
                    + (inventory.IsDeposited(id) ? "In evidence box" : "Awaiting custody pass"));
        }
        return EvidenceDisplay.Text(text.ToString());
    }
    private void ClearPrompt() { if (prompt == null) return; prompt = null; NotificationManager.HidePrompt(); }
    public void HidePanel()
    {
        if (IsShowing) { panel.SetActive(false); pointer.Disable(); }
        ReleaseUI();
        closedFrame = Time.frameCount;
    }
    private void AcquireUI()
    {
        if (holdingUI) return;
        holdingUI = true; PlayerUIGate.Enter(this); ToolInputLock.Enter(this); LocomotionSuspender.Suspend();
    }
    private void ReleaseUI()
    {
        if (!holdingUI) return;
        holdingUI = false; PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume();
    }
    private void ConfirmPanel()
    {
        if (Time.unscaledTime < inputAfter) return;
        if (SceneTurnover.Phase == ScenarioPhase.EvidenceInventoryReview) ConfirmInventory(); else ConfirmIOCReport();
    }
    private void ShowPanel(string heading, string text, string action)
    {
        ClearPrompt(); title.text = GameDialogue.Text(heading); body.text = text; actionLabel.text = GameDialogue.Text(action);
        panel.GetComponent<ResponsivePanelLayout>().Refresh();
        WorldPanelPlacement.PlaceInFront(panel.transform);
        HudFollowCamera.ConfigureShared(panel, this, PlayerUIGate.CriticalPriority); panel.SetActive(true);
        AcquireUI(); pointer.Enable();
        inputAfter = Time.unscaledTime + .25f;
    }
    [EditableSceneBuilder]
    private void BuildUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildUI"))
        {
        panel = EditableSceneVisuals.Create("SOCOCloseoutReview", typeof(RectTransform), typeof(Canvas)); panel.transform.SetParent(transform, false);
        var rect = (RectTransform)panel.transform; rect.sizeDelta = new Vector2(900, 740); rect.localScale = Vector3.one * .001f;
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = short.MaxValue - 4;
        var bg = ForensicUIStyle.Rect(panel.transform, "Background", ForensicUIStyle.PanelBackground).rectTransform;
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        title = Label(panel.transform, "Title", new Vector2(0, 310), new Vector2(830, 65), 36);
        body = Label(panel.transform, "Accounts", new Vector2(0, 5), new Vector2(820, 530), 29); body.alignment = TextAlignmentOptions.TopLeft; body.lineSpacing = 8;
        actionLabel = Label(panel.transform, "Confirm", new Vector2(0, -305), new Vector2(820, 55), 29);
        var controls = Label(panel.transform, "Controls", new Vector2(0, -350), new Vector2(820, 35), 23); controls.text = GameDialogue.Text("closeout-panel-controls");
        pointer = new WorldPanelPointer(rect, "Closeout", this); pointer.Register("closeout-confirm", actionLabel.rectTransform, new Vector2(5, 5), ConfirmPanel);
        var responsive = ResponsivePanelLayout.Create(rect, title, null, controls, 740, 900, 40);
        responsive.AddText(body, responsive.Body, 48);
        var actionRow = responsive.AddText(actionLabel, responsive.Footer, 55);
        actionRow.SetAsFirstSibling();
        pointer.Unregister("closeout-confirm"); pointer.Register("closeout-confirm", actionRow, Vector2.zero, ConfirmPanel);
        responsive.RegisterControls(pointer, "layout-closeout-");
        panel.SetActive(false);
        objectivePanel = EditableSceneVisuals.Create("SOCOCloseoutObjective", typeof(RectTransform), typeof(Canvas), typeof(HudFollowCamera));
        objectivePanel.transform.SetParent(transform, false); objectivePanel.transform.localScale = Vector3.one * .00075f;
        ((RectTransform)objectivePanel.transform).sizeDelta = new Vector2(1100, 65);
        objectivePanel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var follow = objectivePanel.GetComponent<HudFollowCamera>(); follow.Distance = 1.35f; follow.LocalOffset = new Vector3(0, .28f, 0);
        objective = Label(objectivePanel.transform, "Objective", Vector2.zero, new Vector2(1080, 65), 28);
        HudFollowCamera.ConfigureShared(objectivePanel, this, 100, true);
        objectivePanel.SetActive(false);
            }
        using (EditableSceneVisuals.Begin(this,"BuildObjectiveBackdrop"))
        {
            var background = ForensicUIStyle.Rect(objectivePanel.transform,"Next step background",ForensicUIStyle.PanelBackground).rectTransform;
            background.anchorMin=Vector2.zero; background.anchorMax=Vector2.one;
            background.offsetMin=background.offsetMax=Vector2.zero; background.SetAsFirstSibling();
        }
        ((RectTransform)objectivePanel.transform).sizeDelta=new Vector2(1100,190);
        objectivePanel.transform.localScale=Vector3.one*.001f;
        objective.rectTransform.sizeDelta=new Vector2(1040,164);
        objective.fontSize=28; objective.lineSpacing=4;
    }
    private static TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var label = ForensicUIStyle.Text(parent, name, fontSize, ForensicUIStyle.TextPrimary);
        label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position; label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true; return label;
    }
}
