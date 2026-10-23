// Assets/_Project/Scripts/Ending/EndingManager.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Singleton owner of the real "Submit Conclusion" action - the single entry point the
/// UtilityMenuController wheel entry now calls (previously wired directly to
/// HypothesisCheckpointManager.FireSubmissionInitiatedCheckpoints, which this still calls
/// internally as step one, preserving HYP-03's existing behavior exactly).
///
/// Flow: check EndingSet.submissionRequirement (blocked+logged+notified if not met - the
/// "not available immediately unless explicitly enabled" gate) -> fire HYP-03-style
/// SubmissionInitiated checkpoints -> show the conclusion picker if any ConclusionOptions
/// are authored -> resolve the ending by priority (falling back to whichever
/// EndingDefinition is marked isFallback) -> hand it to CompletionScreenUI.ShowEnding.
///
/// This replaces CompletionScreenUI's old hardcoded 2-outcome resolution
/// (IsFingerprintMatched("EVD-019") alone) with the player's actual submitted
/// conclusion plus a data-driven requirement tree - see EndingDefinition's own class
/// comment. CompletionScreenUI no longer triggers off HYP-03 answering at all; this is
/// now the only thing that shows it.
/// </summary>
public class EndingManager : MonoBehaviour
{
    private const int PickerSortingOrder = short.MaxValue - 2;

    // Shared spacing scale (canvas units, same "unit = world mm at this canvas's
    // 0.001 localScale" convention every runtime-built panel in this project already
    // uses) - see this class's own UI-build methods below and RecorderReportUI's
    // equivalent layout constants for the same ratios applied elsewhere.
    private const float SpaceSmall = 12f;
    private const float SpaceMedium = 24f;
    private const float SpaceLarge = 36f;
    private const float SpaceSection = 48f;

    private const float CanvasWidth = 900f;
    private const float CanvasHeight = 640f;
    private const float CardWidth = CanvasWidth - (SpaceLarge * 2f);

    public static EndingManager Instance { get; private set; }

    [SerializeField] private EndingSet endingSet;
    [SerializeField] private CompletionScreenUI completionScreen;
    [SerializeField] private AudioSource audioSource;

    [Tooltip("The fallback \"case stays open\" conclusion. Shown in the picker only while no OTHER Case-level conclusion is available - see GetSelectableCaseConclusions. ConclusionStateManager still tracks it as available; this only changes what the player can pick.")]
    [SerializeField] private string inconclusiveConclusionId = "CONCLUSION-INCONCLUSIVE";

    private InputAction navigateAction;
    private InputAction confirmAction, backAction;
    private ResponsivePanelLayout pickerLayout;
    private WorldPanelPointer pickerPointer;
    private TMP_Text reviewSummary, primaryLabel, backLabel;
    private RectTransform reviewRow;
    private bool reviewingConclusion, cancelledSubmission;
    private string pendingConclusionId;
    private float pickerInputAfter;
    public bool IsPickerShowing => waitingForConclusionPick && pickerRoot != null && pickerRoot.activeInHierarchy;
    public bool IsReviewingConclusion => IsPickerShowing && reviewingConclusion;
    public ResponsivePanelLayout PickerLayout => pickerLayout;
    public WorldPanelPointer PickerPointer => pickerPointer;

    private GameObject pickerRoot;
    private TMP_Text pickerLabel;
    private TMP_Text instructionLabel;
    private TMP_Text confirmHintLabel;
    private RectTransform optionsContainer;
    private readonly List<TMP_Text> optionLabels = new();
    private readonly List<Image> optionCardBackgrounds = new();

    private bool waitingForConclusionPick;
    private bool holdingSubmissionUI;
    private int highlightedIndex;
    private string selectedConclusionId;
    public string SelectedConclusionId { get; private set; }
    public string ResolvedEndingId => AcceptedEnding != null ? AcceptedEnding.endingId : null;
    public EndingDefinition AcceptedEnding { get; private set; }
    public bool FeedbackAccepted { get; private set; }
    private ConclusionDefinition acceptedConclusion;
    private bool finalized;
    private float navigateCooldown;

    // Populated fresh each time the picker opens - see RefreshPickerOptions. Replaces the
    // old static endingSet.conclusionOptions as the picker's actual source of truth; that
    // field is left in the data (still read by nothing) rather than deleted, since this
    // task only replaces the runtime behavior, not the asset schema.
    private readonly List<ConclusionDefinition> currentCaseOptions = new();

    private void Awake()
    {
        Instance = this;

        if (completionScreen == null)
        {
            completionScreen = FindFirstObjectByType<CompletionScreenUI>();
        }

        navigateAction = new InputAction("EndingManager_Navigate", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        confirmAction = PlayerUIGate.CreateConfirmAction(this, "EndingManager_Confirm", () => waitingForConclusionPick, 600);
        backAction = new InputAction("EndingManager_Back", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        navigateAction?.Dispose();
        confirmAction?.Dispose();
        backAction?.Dispose(); pickerPointer?.Dispose();
    }

    private void OnEnable()
    {
        SceneTurnover.OnPhaseChanged += HandleCompletionPhase;
        navigateAction.Enable();
        confirmAction.Enable();
        backAction.Enable();
    }

    private void OnDisable()
    {
        SceneTurnover.OnPhaseChanged -= HandleCompletionPhase;
        navigateAction.Disable();
        confirmAction.Disable();
        backAction.Disable(); pickerPointer?.Disable();
        cancelledSubmission = true;
        if (pickerRoot != null) pickerRoot.SetActive(false);

        // Defensive, same reasoning as HypothesisCheckpointUI/CompletionScreenUI's own
        // OnDisable: never leave locomotion suspended or the UI gate held.
        waitingForConclusionPick = false;
        ReleaseSubmissionHold();
    }

    // One gate token + one locomotion suspend for the whole submission, from the moment
    // Submit is pressed until the completion screen has taken its own - so there is no
    // frame between HYP-03, the picker and the ending where the player can move or open
    // another screen.
    private void AcquireSubmissionHold()
    {
        if (holdingSubmissionUI)
        {
            return;
        }

        holdingSubmissionUI = true;
        LocomotionSuspender.Suspend();
        PlayerUIGate.Enter(this);
    }

    private void ReleaseSubmissionHold()
    {
        if (!holdingSubmissionUI)
        {
            return;
        }

        holdingSubmissionUI = false;
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);
    }

    private void Update()
    {
        if (!waitingForConclusionPick || endingSet == null)
        {
            return;
        }

        pickerPointer?.Tick();
        if (Time.unscaledTime < pickerInputAfter) return;
        if (backAction.WasPressedThisFrame()) { BackFromConclusion(); return; }
        int count = currentCaseOptions.Count;

        navigateCooldown -= Time.unscaledDeltaTime;
        Vector2 stick = navigateAction.ReadValue<Vector2>();
        if (!reviewingConclusion && count > 0 && navigateCooldown <= 0f && Mathf.Abs(stick.x) > 0.5f)
        {
            highlightedIndex = ((highlightedIndex + (stick.x > 0f ? 1 : -1)) % count + count) % count;
            UpdatePickerHighlight();
            navigateCooldown = 0.35f;
        }
        if (Mathf.Abs(stick.y) > .25f) pickerLayout.ScrollOptions(stick.y * 350 * Time.unscaledDeltaTime);

        if (PlayerUIGate.TryConsumeConfirm(this, confirmAction))
        {
            ActivatePickerPrimary();
        }
    }

    public void ActivatePickerPrimary()
    {
        if (!IsPickerShowing || Time.unscaledTime < pickerInputAfter || PlayerUIGate.IsBlockedByOther(this)) return;
        if (reviewingConclusion) ConfirmConclusionPick(pendingConclusionId);
        else if (currentCaseOptions.Count > 0) BeginConclusionReview(highlightedIndex);
    }
    public void BackFromConclusion()
    {
        if (!IsPickerShowing || Time.unscaledTime < pickerInputAfter || PlayerUIGate.IsBlockedByOther(this)) return;
        if (reviewingConclusion)
        {
            reviewingConclusion = false; pendingConclusionId = null;
            UpdatePickerStage(); UpdatePickerHighlight();
        }
        else
        {
            cancelledSubmission = true; selectedConclusionId = null; waitingForConclusionPick = false;
            pickerRoot.SetActive(false); pickerPointer.Disable();
        }
        pickerInputAfter = Time.unscaledTime + .25f;
    }
    private void BeginConclusionReview(int index)
    {
        if (!IsPickerShowing || index < 0 || index >= currentCaseOptions.Count) return;
        highlightedIndex = index; pendingConclusionId = currentCaseOptions[index].conclusionId;
        reviewingConclusion = true;
        UpdatePickerStage(); pickerInputAfter = Time.unscaledTime + .3f;
    }

    /// <summary>
    /// Every currently-available Case-level ConclusionDefinition, read fresh from
    /// ConclusionStateManager - the one place "what conclusions can the player submit
    /// right now" is decided. Never includes a Factual/Interpretive-level conclusion (those
    /// stay internal to Notes/Insights - see ConclusionDefinition.level's own comment) and
    /// never includes an unavailable one, so this list alone is what the picker is allowed
    /// to show.
    /// </summary>
    private List<ConclusionDefinition> GetAvailableCaseConclusions()
    {
        var result = new List<ConclusionDefinition>();
        if (ConclusionStateManager.Instance == null)
        {
            return result;
        }

        foreach (var def in ConclusionStateManager.Instance.GetAvailableConclusions())
        {
            if (def != null && def.level == ConclusionLevel.Case)
            {
                result.Add(def);
            }
        }
        return result;
    }

    /// <summary>
    /// What the picker may actually offer: every available Case-level conclusion, except
    /// that the fallback Inconclusive conclusion is withheld whenever an available case
    /// conclusion has reached its authored supported-case requirement. This keeps an
    /// early accusation and Inconclusive available together while the investigation is
    /// incomplete, but preserves the historical behaviour once a supported case exists.
    /// Read-only: ConclusionStateManager's monotonic availability is untouched;
    /// Inconclusive stays internally available and simply isn't selectable.
    /// </summary>
    public List<ConclusionDefinition> GetSelectableCaseConclusions()
    {
        var available = GetAvailableCaseConclusions();

        bool supportedAlternativeExists = false;
        foreach (var def in available)
        {
            if (!IsInconclusive(def.conclusionId)
                && def.hidesInconclusiveWhenSupported
                && RequirementEvaluator.IsSatisfied(def.inconclusiveSuppressionRequirement))
            {
                supportedAlternativeExists = true;
                break;
            }
        }

        if (supportedAlternativeExists)
        {
            available.RemoveAll(def => IsInconclusive(def.conclusionId));
        }
        return available;
    }

    /// <summary>Whether conclusionId may be submitted right now, under the same rule the picker uses.</summary>
    public bool IsConclusionSelectable(string conclusionId)
    {
        if (string.IsNullOrEmpty(conclusionId))
        {
            return false;
        }

        foreach (var def in GetSelectableCaseConclusions())
        {
            if (def.conclusionId == conclusionId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Ids the picker is currently offering (empty while it isn't showing) - test inspection only.</summary>
    public List<string> GetPickerOptionIdsForTest()
    {
        var ids = new List<string>();
        if (waitingForConclusionPick)
        {
            foreach (var def in currentCaseOptions)
            {
                ids.Add(def.conclusionId);
            }
        }
        return ids;
    }

    private bool IsInconclusive(string conclusionId)
    {
        return !string.IsNullOrEmpty(inconclusiveConclusionId) && conclusionId == inconclusiveConclusionId;
    }

    /// <summary>
    /// The real, gated Submit Conclusion action - public so both the real UtilityMenuController
    /// wheel entry (via a UnityEvent persistent call) and GreyboxFlowTest (driving "the real
    /// state-machine entry point a button press would reach", same discipline as every other
    /// TryX method in this project) call this exact logic.
    ///
    /// DYNAMIC UNLOCK RULE (replaces the old fixed endingSet.submissionRequirement gate,
    /// which hard-required two specific items - BODY-A and EVD-014 - reach Logged,
    /// regardless of what the rest of the investigation looked like): available when (1) the
    /// player has submitted a theory at least once (EvidenceBoardController.
    /// HasEverSubmittedTheory - the authoritative signal for that actual action, not an
    /// inferred proxy), (2) ConclusionStateManager reports at least one available Case-level
    /// conclusion (evidence-driven, never "all evidence" or "all relationships"), and (3) no
    /// blocking UI/player state currently owns the screen. endingSet.submissionRequirement
    /// itself is left in the data (nothing else references it) but is no longer read here -
    /// see EndingSet.submissionRequirement's own updated tooltip.
    /// </summary>
    public void TrySubmitConclusion()
    {
        if (holdingSubmissionUI || waitingForConclusionPick) return;
        if (AcceptedEnding != null || (SceneTurnover.IsGated && SceneTurnover.Phase != ScenarioPhase.SOCOInvestigation)) return;
        if (SceneTurnover.IsInvestigationBlocked) return;
        if (endingSet == null)
        {
            Debug.LogWarning("[EndingManager] No EndingSet assigned - Submit Conclusion does nothing.");
            return;
        }

        if (PlayerUIGate.IsBlocked)
        {
            GameDialogue.Say("conclusion-close-panels-first");
            InteractionFeedback.Blocked();
            return;
        }

        if (BlockForRequiredVendorFollowup() || BlockForUnsubmittedSketch()) return;

        var availableCaseOptions = GetSelectableCaseConclusions();
        bool reviewReady = EvidenceRelationshipsReview.Instance?.IsReadyForConclusion == true;
        bool ready = reviewReady && availableCaseOptions.Count > 0;

        if (!ready)
        {
            // Deliberately neutral - never names which evidence/finding is missing, see
            // this feature's own explicit "do not reveal the required path" constraint.
            GameDialogue.Say("conclusion-theory-first");
            InteractionFeedback.Blocked();
            SessionLogger.Instance?.LogEvent(SessionEventType.ConclusionSubmissionBlocked, null, new Dictionary<string, string>
            {
                { "reason", !reviewReady ? "relationship review not completed" : "no available Case-level conclusion" }
            });
            return;
        }

        InteractionFeedback.Confirm();
        SessionLogger.Instance?.LogEvent(SessionEventType.ConclusionSubmissionAttempted);

        AcquireSubmissionHold();
        cancelledSubmission = false;
        HypothesisCheckpointManager.Instance?.FireSubmissionInitiatedCheckpoints();

        currentCaseOptions.Clear();
        currentCaseOptions.AddRange(availableCaseOptions);
        StartCoroutine(ShowConclusionPickerRoutine());
    }

    private IEnumerator ShowConclusionPickerRoutine()
    {
        // Anything FireSubmissionInitiatedCheckpoints just opened (HYP-03) occupies the
        // same HUD slot and the same right-hand stick/A bindings as the picker and the
        // completion screen, so it has to be finished before either of them appears.
        while (PlayerUIGate.IsBlockedByOther(this))
        {
            yield return null;
        }

        BuildPickerUIIfNeeded();

        // Activate before measuring card sizes, not after: RefreshPickerOptions
        // measures each card's wrapped-text height via TMP's GetPreferredValues,
        // which silently returns near-zero garbage while its Canvas/CanvasScaler
        // ancestor is still inactive (their OnEnable, which TMP's internal scale
        // setup depends on, hasn't run yet) - a real bug hit while building this
        // feature: with the old build-then-activate order every card came out a few
        // units tall and overlapped the next one. Activating first costs nothing
        // visible: nothing has content yet, and RefreshPickerOptions fills it in the
        // same synchronous call before this coroutine ever yields.
        HudFollowCamera.ConfigureShared(pickerRoot, this, PlayerUIGate.CriticalPriority);
        pickerRoot.SetActive(true);

        // Re-read after HYP-03 has closed rather than trusting the list taken at
        // Submit - availability can change while the checkpoint was up.
        currentCaseOptions.Clear();
        currentCaseOptions.AddRange(GetSelectableCaseConclusions());
        highlightedIndex = 0;
        waitingForConclusionPick = true;
        selectedConclusionId = null;
        reviewingConclusion = false; pendingConclusionId = null;
        RefreshPickerOptions();
        pickerPointer.Enable(); pickerInputAfter = Time.unscaledTime + .3f;
        UpdatePickerHighlight();

        while (waitingForConclusionPick)
        {
            yield return null;
        }

        pickerRoot.SetActive(false);
        pickerPointer.Disable();
        if (cancelledSubmission) { ReleaseSubmissionHold(); yield break; }

        if (VendorFollowupInterview.CompletionBlockReason != null)
        {
            ReleaseSubmissionHold();
            BlockForRequiredVendorFollowup();
            yield break;
        }
        if (CrimeSceneSketchManager.CompletionBlockReason != null)
        {
            ReleaseSubmissionHold();
            CrimeSceneSketchUI.Instance?.Show();
            yield break;
        }

        // Facts can arrive asynchronously while this modal is open (for example a
        // vendor answer finishing). Never freeze a conclusion with a newly pending
        // relationship review. Return ownership to that review instead.
        if (selectedConclusionId == null || EvidenceRelationshipsReview.Instance?.IsReadyForConclusion != true)
        {
            ReleaseSubmissionHold();
            EvidenceRelationshipsReview.Instance?.Show();
            yield break;
        }

        // The completion screen takes its own gate token/suspend inside FinishSubmission;
        // releasing ours only afterwards hands ownership over without a free frame.
        FinishSubmission(selectedConclusionId);
        ReleaseSubmissionHold();
    }

    private bool ConfirmConclusionPick(string conclusionId)
    {
        if (string.IsNullOrEmpty(conclusionId)) return false;
        if (BlockForRequiredVendorFollowup() || BlockForUnsubmittedSketch())
        {
            waitingForConclusionPick = false;
            selectedConclusionId = null;
            return false;
        }
        if (EvidenceRelationshipsReview.Instance?.IsReadyForConclusion != true)
        {
            waitingForConclusionPick = false;
            selectedConclusionId = null;
            GameDialogue.Say("conclusion-theory-first");
            SessionLogger.Instance?.LogEvent(SessionEventType.ConclusionSubmissionBlocked, conclusionId,
                new Dictionary<string,string> {{"reason", "new relationship review required"}});
            return false;
        }
        // Stale-selection safeguard: the list on screen was built when the picker
        // opened. If what's selectable has changed since (e.g. a supported Case
        // conclusion became available and now supersedes Inconclusive), refuse this
        // confirm and rebuild the picker - never submit a conclusion the picker would
        // no longer offer.
        if (!IsConclusionSelectable(conclusionId))
        {
            SessionLogger.Instance?.LogEvent(SessionEventType.ConclusionSubmissionBlocked, conclusionId, new Dictionary<string, string>
            {
                { "reason", "conclusion no longer selectable" }
            });
            GameDialogue.Say("conclusion-options-changed");
            InteractionFeedback.Blocked();

            currentCaseOptions.Clear();
            currentCaseOptions.AddRange(GetSelectableCaseConclusions());
            RefreshPickerOptions();
            highlightedIndex = 0;
            UpdatePickerHighlight();
            return false;
        }

        selectedConclusionId = conclusionId;
        waitingForConclusionPick = false;

        SessionLogger.Instance?.LogEvent(SessionEventType.ConclusionSelected, conclusionId);
        InteractionFeedback.Confirm();
        return true;
    }

    /// <summary>
    /// Test-only escape hatch: picks conclusionId as if the player had confirmed it -
    /// same "documented test-only equivalent of a real confirm" shape as RecorderReportUI.
    /// SelectLineForTest/HypothesisCheckpointUI.AnswerForTest. No-op if the picker isn't
    /// currently showing; false (and the picker refreshed) if conclusionId isn't
    /// selectable - same safeguard as a real confirm.
    /// </summary>
    public bool SelectConclusionForTest(string conclusionId)
    {
        if (!waitingForConclusionPick)
        {
            return false;
        }

        // Diagnostic callers explicitly select and submit; player input always passes
        // through the separate review screen and its release/cooldown protection.
        int index = currentCaseOptions.FindIndex(c => c.conclusionId == conclusionId);
        if (index >= 0) BeginConclusionReview(index);
        return ConfirmConclusionPick(conclusionId);
    }

    private bool BlockForRequiredVendorFollowup()
    {
        string reason = VendorFollowupInterview.CompletionBlockReason;
        if (reason == null) return false;
        GameDialogue.ShowText(reason);
        InteractionFeedback.Blocked();
        SessionLogger.Instance?.LogEvent(SessionEventType.ConclusionSubmissionBlocked, null,
            new Dictionary<string, string> { { "reason", "required vendor follow-up not completed" } });
        return true;
    }

    private bool BlockForUnsubmittedSketch()
    {
        string reason = CrimeSceneSketchManager.CompletionBlockReason;
        if (reason == null) return false;
        GameDialogue.ShowText(reason);
        InteractionFeedback.Blocked();
        SessionLogger.Instance?.LogEvent(SessionEventType.ConclusionSubmissionBlocked, null,
            new Dictionary<string, string> { { "reason", "scene sketch not submitted" } });
        return true;
    }

    private void FinishSubmission(string conclusionId)
    {
        if (AcceptedEnding != null) return;
        if (BlockForRequiredVendorFollowup() || BlockForUnsubmittedSketch()) return;
        var resolution = ResolveEnding(conclusionId);

        SessionLogger.Instance?.LogEvent(SessionEventType.EndingSelected, resolution.Ending?.endingId, new Dictionary<string, string>
        {
            { "rule", resolution.Rule.ToString() },
            { "selectedConclusionId", conclusionId ?? string.Empty }
        });

        if (resolution.Ending == null)
        {
            Debug.LogWarning("[EndingManager] No ending resolved and no fallback authored - nothing to show.");
            return;
        }

        if (audioSource != null && resolution.Ending.endingAudioClip != null)
        {
            audioSource.PlayOneShot(resolution.Ending.endingAudioClip);
        }

        if (!string.IsNullOrEmpty(resolution.Ending.followUpNarrativeEventId))
        {
            NarrativeEventManager.Instance?.Fire(resolution.Ending.followUpNarrativeEventId);
        }

        if (completionScreen == null)
        {
            completionScreen = FindFirstObjectByType<CompletionScreenUI>();
        }

        // The full definition, not just its displayName - the post-case review screen
        // reads its description/availabilityRequirement too (see CompletionScreenUI.
        // BuildSupportPage/BuildMissedOpportunitiesPage). GetRecord returns the tracked
        // record regardless of its availability, so this stays safe even for an id
        // ConclusionStateManager never marked available; null for an unrecognized id,
        // which every CompletionScreenUI review page tolerates by omitting that section.
        ConclusionDefinition selectedConclusion = ConclusionStateManager.Instance?.GetRecord(conclusionId)?.definition;
        SelectedConclusionId = conclusionId;
        AcceptedEnding = resolution.Ending;
        acceptedConclusion = selectedConclusion;
        FinalDeductionAssessment.CaptureConclusion(conclusionId, resolution.Ending.endingId);
        completionScreen?.ShowEnding(resolution.Ending, selectedConclusion);
        if (!SceneTurnover.IsGated) FinalizeSession();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentCloseout()
    {
        SceneTestStart.RequirePreparation();
        // Fixture result only; do not submit/score a student conclusion or queue an
        // assessment to reach the closeout under test.
        var fixture = endingSet.endings.Find(e => e != null && e.isFallback);
        if (fixture == null) throw new System.InvalidOperationException("Missing fallback ending for closeout fixture.");
        SelectedConclusionId = "CONCLUSION-INCONCLUSIVE";
        AcceptedEnding = fixture;
        FeedbackAccepted = true;
        if (!SceneTurnover.TryAdvance(ScenarioPhase.SOCOCloseout))
            throw new System.InvalidOperationException("Cannot enter closeout fixture.");
    }
#endif

    // The feedback screen calls this after its player-paced acknowledgement.
    public bool AcceptConclusionFeedback()
    {
        if (AcceptedEnding == null || SceneTurnover.Phase != ScenarioPhase.SOCOInvestigation) return false;
        FeedbackAccepted = true;
        return SceneTurnover.TryAdvance(ScenarioPhase.SOCOCloseout);
    }

    private void HandleCompletionPhase(ScenarioPhase phase)
    {
        if (phase != ScenarioPhase.InvestigationComplete) return;
        StartCoroutine(FinalizeAfterMilestones());
    }
    private IEnumerator FinalizeAfterMilestones()
    {
        // Let every phase subscriber record its final objective/milestone before flushing.
        yield return null;
        FinalizeSession();
        completionScreen?.ShowFinalReview(AcceptedEnding, acceptedConclusion);
    }

    private void FinalizeSession()
    {
        if (finalized || AcceptedEnding == null) return;
        finalized = true;
        SessionLogger.Instance?.EndSession();

        // The one integration point between this project's existing, working conclusion
        // flow and the separate assessment-upload layer (Assets/_Project/Scripts/
        // Assessment) - queues the just-completed session for upload without touching
        // anything about how the ending itself was resolved or shown above. Never blocks:
        // AssessmentUploadService.QueueSessionForUpload does its local save synchronously
        // (fast) and fires the network attempt fire-and-forget, so the completion screen
        // showing above is never frozen waiting on a network call.
        if (AssessmentUploadService.Instance != null && SessionLogger.Instance != null)
        {
            string logPath = SessionLogger.Instance.ActiveLogFilePath;
            if (!string.IsNullOrEmpty(logPath))
            {
                AssessmentUploadService.Instance.QueueSessionForUpload(logPath, SelectedConclusionId, ResolvedEndingId);
            }
        }
    }

    /// <summary>
    /// Priority-ordered resolution: the highest-priority EndingDefinition whose
    /// resolutionRequirement passes AND (requiredConclusionId is blank or matches
    /// selectedConclusionId) wins; ties keep authoring order. Falls back to whichever
    /// ending is marked isFallback if nothing matches. Read-only - never mutates any
    /// other system's state.
    /// </summary>
    public EndingResolution ResolveEnding(string selectedConclusionId)
    {
        EndingDefinition best = null;
        EndingDefinition fallback = null;

        if (endingSet?.endings != null)
        {
            foreach (var ending in endingSet.endings)
            {
                if (ending == null)
                {
                    continue;
                }

                if (ending.isFallback && fallback == null)
                {
                    fallback = ending;
                }

                bool conclusionMatches = string.IsNullOrEmpty(ending.requiredConclusionId) || ending.requiredConclusionId == selectedConclusionId;
                if (!conclusionMatches || !RequirementEvaluator.IsSatisfied(ending.resolutionRequirement))
                {
                    continue;
                }

                if (best == null || ending.priority > best.priority)
                {
                    best = ending;
                }
            }
        }

        if (best != null)
        {
            return new EndingResolution { Ending = best, Rule = EndingResolutionRule.PriorityMatch };
        }

        if (fallback != null)
        {
            return new EndingResolution { Ending = fallback, Rule = EndingResolutionRule.Fallback };
        }

        return new EndingResolution { Ending = null, Rule = EndingResolutionRule.NoneAvailable };
    }

    // --- Conclusion picker runtime UI - same recipe as CompletionScreenUI/
    // HypothesisCheckpointUI's own BuildRuntimeUI, purpose-built rather than shared. ---

    [EditableSceneBuilder]
    private void BuildPickerUIIfNeeded()
    {
        using (EditableSceneVisuals.Begin(this,"BuildPickerUIIfNeeded"))
        {
        if (pickerRoot != null)
        {
            return;
        }

        var canvasGo = EditableSceneVisuals.Create("ConclusionPickerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudFollowCamera));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = PickerSortingOrder;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        canvasRect.localScale = Vector3.one * 0.001f;

        canvasGo.GetComponent<HudFollowCamera>().Distance = 0.75f;

        var background = EditableSceneVisuals.Create("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasRect, false);
        var bgImage = background.GetComponent<Image>();
        bgImage.color = new Color(0.06f, 0.06f, 0.08f, 0.96f);
        Stretch(background.GetComponent<RectTransform>());

        // Clear top-down hierarchy: title -> short instruction -> option cards (built
        // per-open in RefreshPickerOptions, since count/content is data-driven) ->
        // confirm hint, each separated by the shared spacing scale above rather than
        // ad hoc values - see this feature's own "Submit Conclusion layout" spec.
        float y = -SpaceLarge;
        pickerLabel = CreateHeaderText(canvasRect, "PromptLabel", 30f, y, 40f);
        pickerLabel.text = "Submit your conclusion:";
        pickerLabel.fontStyle = FontStyles.Bold;
        y -= 40f + SpaceSmall;

        instructionLabel = CreateHeaderText(canvasRect, "InstructionLabel", 18f, y, 26f);
        instructionLabel.text = "Right thumbstick to highlight a conclusion. A to confirm.";
        instructionLabel.color = new Color(0.72f, 0.72f, 0.76f, 1f);
        y -= 26f + SpaceSection;

        var optionsGo = EditableSceneVisuals.Create("Options", typeof(RectTransform));
        optionsGo.transform.SetParent(canvasRect, false);
        optionsContainer = optionsGo.GetComponent<RectTransform>();
        optionsContainer.anchorMin = new Vector2(0.5f, 1f);
        optionsContainer.anchorMax = new Vector2(0.5f, 1f);
        optionsContainer.pivot = new Vector2(0.5f, 1f);
        optionsContainer.sizeDelta = new Vector2(CardWidth, 1f);
        optionsContainer.anchoredPosition = new Vector2(0f, y);

        confirmHintLabel = CreateHeaderText(canvasRect, "ConfirmHintLabel", 16f, -(CanvasHeight - SpaceLarge - 22f), 22f);
        confirmHintLabel.text = "Press A to confirm - this cannot be undone.";
        confirmHintLabel.color = new Color(0.58f, 0.58f, 0.63f, 1f);

        pickerRoot = canvasGo;
        pickerRoot.SetActive(false);
            }
        BuildAccessiblePicker();
    }

    private void BuildAccessiblePicker()
    {
        using (EditableSceneVisuals.Begin(this,"BuildAccessiblePicker"))
        {
            var rect=(RectTransform)pickerRoot.transform;
            pickerPointer=new WorldPanelPointer(rect,"ConclusionPicker",this);
            pickerLayout=ResponsivePanelLayout.Create(rect,pickerLabel,optionsContainer,confirmHintLabel,680,900,40,14);
            pickerLayout.AddText(instructionLabel,pickerLayout.Header,64,8);
            reviewSummary=ForensicUIStyle.Text(pickerRoot.transform,"Selected conclusion",29,ForensicUIStyle.TextPrimary);
            reviewRow=pickerLayout.AddText(reviewSummary,pickerLayout.Body,100,20);
            var actions=ResponsivePanelLayout.Rect(pickerLayout.Footer.Content,"Conclusion actions");
            var height=EditableSceneVisuals.Component<LayoutElement>(actions.gameObject);
            height.minHeight=height.preferredHeight=76; height.flexibleHeight=0;
            var primary=PickerButton(actions,"Primary action",0,.55f,ForensicUIStyle.Accent,ForensicUIStyle.PanelBackground,out primaryLabel);
            var back=PickerButton(actions,"Back action",.58f,1,ForensicUIStyle.SelectedFill,ForensicUIStyle.TextPrimary,out backLabel);
            pickerPointer.Register("conclusion-primary",primary.rectTransform,Vector2.zero,ActivatePickerPrimary,
                hover=>primary.color=hover?new Color(.96f,.82f,.51f):ForensicUIStyle.Accent);
            pickerPointer.Register("conclusion-back",back.rectTransform,Vector2.zero,BackFromConclusion,
                hover=>back.color=hover?new Color(.27f,.37f,.50f):ForensicUIStyle.SelectedFill);
            actions.SetAsFirstSibling();
            pickerLayout.RegisterControls(pickerPointer,"conclusion-scroll-");
        }
        ((RectTransform)pickerRoot.transform).sizeDelta=new Vector2(CanvasWidth,740);
        pickerRoot.GetComponent<HudFollowCamera>().Distance=1.25f;
        pickerRoot.transform.Find("Background").GetComponent<Image>().color=ForensicUIStyle.PanelBackground;
        pickerLabel.fontSize=36; pickerLabel.fontStyle=FontStyles.Bold; pickerLabel.alignment=TextAlignmentOptions.MidlineLeft;
        instructionLabel.fontSize=26; instructionLabel.color=ForensicUIStyle.TextSecondary; instructionLabel.alignment=TextAlignmentOptions.MidlineLeft;
        confirmHintLabel.fontSize=23; confirmHintLabel.color=ForensicUIStyle.TextSecondary;
        reviewRow.gameObject.SetActive(false);
    }
    private Image PickerButton(RectTransform parent,string name,float left,float right,Color fill,Color ink,out TMP_Text label)
    {
        var image=ForensicUIStyle.Rect(parent,name,fill);
        var rect=image.rectTransform; rect.anchorMin=new Vector2(left,0); rect.anchorMax=new Vector2(right,1);
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        label=ForensicUIStyle.Text(rect,name+" label",28,ink,FontStyles.Bold,TextAlignmentOptions.Center);
        label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=new Vector2(14,10); label.rectTransform.offsetMax=new Vector2(-14,-10);
        return image;
    }
    private void UpdatePickerStage()
    {
        pickerLabel.text=reviewingConclusion?"Review your conclusion":"Choose your conclusion";
        instructionLabel.text=reviewingConclusion?"Check your selection before submitting. You can still go back."
            :currentCaseOptions.Count==0?"No conclusions are currently available. Return to the investigation."
            :"Choose the interpretation you can support with the findings.";
        primaryLabel.text=reviewingConclusion?"[A] Submit conclusion":currentCaseOptions.Count==0?"Unavailable":"[A] Review selection";
        backLabel.text=reviewingConclusion?"[B] Back to choices":"[B] Cancel";
        confirmHintLabel.text=reviewingConclusion?"Submitting records your conclusion for this investigation."
            :"Right stick left/right: choose   |   up/down: scroll";
        if(reviewingConclusion)
        {
            var definition=currentCaseOptions.Find(c=>c.conclusionId==pendingConclusionId);
            reviewSummary.text=definition!=null?"<b>"+definition.displayName+"</b>\n\n"+definition.description:"This selection is no longer available. Go back to choices.";
        }
        reviewRow.gameObject.SetActive(reviewingConclusion);
        foreach(var label in optionLabels) label.transform.parent.gameObject.SetActive(!reviewingConclusion);
        pickerLayout.Refresh(); pickerLayout.Body.Scroll.verticalNormalizedPosition=1;
        pickerLayout.Body.Content.anchoredPosition=Vector2.zero;
    }

    /// <summary>
    /// Builds the picker's option cards from currentCaseOptions (set immediately before
    /// this is called, in TrySubmitConclusion) - each shows the authored displayName plus
    /// description, never a raw conclusionId, evidence id, or finding id. Only ever shows
    /// conclusions ConclusionStateManager has already marked available - see
    /// GetAvailableCaseConclusions. Each card's height is measured from its own wrapped
    /// text (TMP GetPreferredValues) plus consistent internal padding, rather than a
    /// single arbitrary fixed height that would either clip a long description or waste
    /// space on a short one - the "consistent height RULE" the card spec asks for.
    /// </summary>
    private void RefreshPickerOptions()
    {
        pickerPointer.Unregister("conclusion-option-");
        foreach (var label in optionLabels)
        {
            if (label != null)
            {
                label.transform.parent.gameObject.SetActive(false);
                Destroy(label.transform.parent.gameObject);
            }
        }
        optionLabels.Clear();
        optionCardBackgrounds.Clear();

        reviewingConclusion=false; pendingConclusionId=null;
        for (int i = 0; i < currentCaseOptions.Count; i++)
        {
            var def = currentCaseOptions[i];
            var cardImage=ForensicUIStyle.Rect(optionsContainer,"Conclusion choice "+(i+1),ForensicUIStyle.HeaderBackground);
            optionCardBackgrounds.Add(cardImage);
            var label=ForensicUIStyle.Text(cardImage.transform,"Conclusion text",29,ForensicUIStyle.TextPrimary,FontStyles.Normal,TextAlignmentOptions.TopLeft);
            label.font=TMP_Settings.defaultFontAsset;
            label.text="<b>"+def.displayName+"</b>\n<size=26>"+def.description+"</size>";
            pickerLayout.AddOption(cardImage.rectTransform,label,100);
            optionLabels.Add(label);
            int index=i;
            pickerPointer.Register("conclusion-option-"+i,cardImage.rectTransform,Vector2.zero,()=>
            {
                if(IsPickerShowing && !reviewingConclusion && Time.unscaledTime>=pickerInputAfter && !PlayerUIGate.IsBlockedByOther(this)) BeginConclusionReview(index);
            });
        }
        highlightedIndex=Mathf.Clamp(highlightedIndex,0,Mathf.Max(0,currentCaseOptions.Count-1));
        UpdatePickerStage(); UpdatePickerHighlight();
    }

    private void UpdatePickerHighlight()
    {
        for (int i = 0; i < optionLabels.Count; i++)
        {
            bool selected = i == highlightedIndex;
            var def=currentCaseOptions[i];
            optionLabels[i].text=(selected?"<color=#DCB45C>Selected</color>\n":"")+"<b>"+def.displayName+"</b>\n<size=26>"+def.description+"</size>";
            optionLabels[i].color=ForensicUIStyle.TextPrimary;

            if (i < optionCardBackgrounds.Count)
            {
                optionCardBackgrounds[i].color = selected
                    ? ForensicUIStyle.SelectedFill : ForensicUIStyle.HeaderBackground;
            }
        }
        pickerLayout.Refresh();
        if(!reviewingConclusion && optionLabels.Count>0) pickerLayout.EnsureVisible((RectTransform)optionLabels[highlightedIndex].transform.parent);
    }

    /// <summary>Full-width single line, positioned by an explicit distance in canvas units from the canvas's top edge (see BuildPickerUIIfNeeded's running "y" cursor) - avoids the old normalized-0..1-anchor math, which silently repositioned every label whenever CanvasHeight changed.</summary>
    private static TMP_Text CreateHeaderText(RectTransform parent, string name, float fontSize, float yFromTop, float height)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.color = Color.white;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(CanvasWidth - (SpaceLarge * 2f), height);
        rect.anchoredPosition = new Vector2(0f, yFromTop);

        return text;
    }

    private static TMP_Text CreateBodyText(RectTransform parent, string name, float fontSize, float width)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        // A just-created TextMeshProUGUI hasn't resolved its font asset yet (normally
        // lazy, via OnEnable) - RefreshPickerOptions calls GetPreferredValues on this
        // same frame to measure card height, which throws a NullReferenceException
        // deep in TMP's material-array setup unless the font is assigned explicitly
        // first. Every other runtime TMP_Text in this project only ever *displays*
        // text (Unity gets a frame to initialize it first), never measures it
        // same-frame, which is why this assignment isn't needed anywhere else.
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.enableWordWrapping = true;
        text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(width, 100f);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
