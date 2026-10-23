// Assets/_Project/Scripts/Deduction/CompletionScreenUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Conclusion feedback followed by procedural closeout, then the final case review.
/// (1) a short, auto-advancing sequence of STCS-style
/// narrative lines (matching the tone already established by DialoguePool content - see
/// STCSManager), unchanged from before this pass; then (2) a player-paced, multi-page
/// POST-CASE REVIEW - the Post-Case Assessment feature. Shown exactly once per session,
/// via ShowEnding(EndingDefinition, ConclusionDefinition) - called only by EndingManager,
/// the moment it resolves an ending at Submit Conclusion. In the gated scenario,
/// feedback acknowledges into SOCOCloseout; review/menu return wait until scene release.
///
/// The review pages are entirely DATA-DERIVED at the moment they're built: selected
/// ConclusionDefinition.displayName/description, FindingDefinition.displayName for
/// findings referenced by that conclusion's own availabilityRequirement (via
/// RequirementTreeInspector - never a hardcoded per-conclusion list), RelationshipDefinition.
/// displayName for relationships it references, EvidenceDefinition.displayName for
/// evidence, and live counts from EvidenceStateManager/FindingStateManager/
/// RelationshipStateManager/SessionLogger/EvidenceBoardController.LastScoreResult. No
/// suspect-specific or per-conclusion string is ever written in this file - see each
/// Build*Page method's own comment for its data source. Never shown until the case is
/// procedurally complete. Immediate conclusion feedback remains separate from that review.
///
/// Reuses HypothesisCheckpointUI's own proven shape rather than inventing a new one:
/// a World Space canvas pinned via HudFollowCamera, PlayerUIGate + LocomotionSuspender
/// held while up, the same right-controller A-button/B-button input this project already
/// uses for panel confirmation/close (PhotoAlbumUI/ToolWheelController/
/// HypothesisCheckpointUI/RecorderReportUI). Not the Meta ray -> uGUI pipeline - see
/// RecorderReportUI's own class comment for why that doesn't reliably reach world-space
/// canvases built this way in this project.
/// </summary>
public class CompletionScreenUI : MonoBehaviour
{
    private const int PanelSortingOrder = short.MaxValue - 1;

    [Header("Authored UI (optional - leave blank to auto-generate at runtime)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text lineLabel;
    [SerializeField] private TMP_Text hintLabel;

    [Header("Pacing")]
    [Tooltip("How long each line stays up before auto-advancing to the next - matches STCSManager.lineDisplaySeconds for a consistent feel.")]
    [SerializeField] private float lineDisplaySeconds = 3f;

    [Header("Return to menu")]
    [SerializeField] private string menuSceneName = "Main";

    // Generic scene-wrapper framing shown around EVERY ending's own endingText,
    // regardless of which one resolved - short radio/comms lines matching the voice
    // already established by the real DialoguePool content (Dispatch/Recorder - see the
    // solo-player STCS rename that merged Team Leader/Sketcher/Photographer into
    // "Dispatch"), never a scorecard - no "good job", no "correct", nothing that reads
    // as a grade. Case-specific content belongs in EndingDefinition.endingText, authored
    // per scenario, not here.
    private static string OpeningLine => GameDialogue.Text("ending-radio-opening");
    private static string ClosingLine => GameDialogue.Text("ending-radio-closing");
    private static string FarewellLine => GameDialogue.Text("ending-radio-farewell");

    // Findings/evidence entries used only as internal dev/test fixtures - never real case
    // content, so never surfaced on the player-facing review even though they're tracked
    // like any other record. Currently just the one FindingStateManager seeds for smoke
    // testing (see FindingStateManager.sceneFindingDefinitions).
    private const string TestOnlyFindingId = "FIND-TEST-HARMLESS";

    private InputAction submitAction;
    private InputAction closeAction;
    private InputAction feedbackScroll;
    private ResponsivePanelLayout feedbackLayout;
    private WorldPanelPointer feedbackPointer;
    private TMP_Text feedbackHeading;
    private GameObject custodyContinueButton;
    private string submittedConclusionName;
    private TMP_Text reviewProgress, reviewNextLabel;
    private RectTransform reviewProgressRow, finalReviewActions;
    private Image reviewPreviousButton;
    private WorldPanelPointer.Target previousReviewTarget;
    private float reviewInputAfter;
    public bool IsInFinalReview => IsShowing && inReviewPhase;
    public int ReviewPageIndex => reviewIndex;
    public int ReviewPageCount => reviewPages.Count;
    public string ReviewTitle => feedbackHeading != null ? feedbackHeading.text : null;
    public string ReviewBody => lineLabel != null ? lineLabel.text : null;
    public ResponsivePanelLayout ReviewLayout => feedbackLayout;
    public WorldPanelPointer ReviewPointer => feedbackPointer;
    private Coroutine sequenceRoutine;
    private bool awaitingReturn;
    private bool awaitingCloseout;

    // --- Review phase state ---
    private readonly List<string> reviewPages = new();
    private int reviewIndex;
    private bool inReviewPhase;

    /// <summary>True from the moment this screen appears until the scene actually unloads.</summary>
    public bool IsShowing { get; private set; }

    private void Awake()
    {
        submitAction = PlayerUIGate.CreateConfirmAction(this, "CompletionScreen_Submit", () => IsShowing, 600);
        closeAction = new InputAction("CompletionScreen_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");

        if (panelRoot == null || GetComponent<EditableSceneVisuals>() != null)
        {
            BuildRuntimeUI();
        }
        if (feedbackLayout == null) BuildFeedbackDetails();

        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        submitAction.Enable();
        closeAction.Enable();
        feedbackScroll?.Enable();
    }

    private void OnDisable()
    {
        ToolInputLock.Exit(this);
        submitAction.Disable();
        closeAction.Disable();
        feedbackScroll?.Disable(); feedbackPointer?.Disable();

        // Defensive, same reasoning as HypothesisCheckpointUI.OnDisable: never leave
        // locomotion suspended or the UI gate held if this component is torn down
        // mid-sequence.
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    private void OnDestroy()
    {
        submitAction?.Dispose();
        closeAction?.Dispose();
        feedbackScroll?.Dispose(); feedbackPointer?.Dispose();
    }

    /// <summary>
    /// Shows this screen for the given resolved ending - called only by EndingManager,
    /// once per session (EndingManager itself only resolves/shows an ending once, at
    /// Submit Conclusion).
    ///
    /// selectedConclusion is the ConclusionDefinition the player actually selected (looked
    /// up by EndingManager via ConclusionStateManager.GetRecord, using the same conclusionId
    /// passed to ResolveEnding) - null if none was ever tracked (e.g. an invalid id, or an
    /// ending resolved with no conclusion selection at all). Every review page below
    /// tolerates a null selectedConclusion by simply omitting the sections that need one.
    /// </summary>
    public void ShowEnding(EndingDefinition ending, ConclusionDefinition selectedConclusion = null)
    {
        if (IsShowing || ending == null)
        {
            return;
        }

        IsShowing = true;
        submittedConclusionName=selectedConclusion!=null?selectedConclusion.displayName:null;
        feedbackHeading.text="Conclusion recorded";
        custodyContinueButton.SetActive(false);
        feedbackPointer.Enable();
        awaitingReturn = false;
        awaitingCloseout = false;
        inReviewPhase = false;
        SetFinalReviewControls(false);
        LocomotionSuspender.Suspend();
        PlayerUIGate.Enter(this);
        ToolInputLock.Enter(this);

        HudFollowCamera.ConfigureShared(panelRoot, this, 600);
        panelRoot.SetActive(true);
        InteractionFeedback.Confirm(panelRoot.transform);

        if (hintLabel != null)
        {
            hintLabel.text = string.Empty;
        }

        BuildReviewPages(ending, selectedConclusion);
        sequenceRoutine = StartCoroutine(PlayLines(BuildLines(ending, selectedConclusion)));
    }

    private string[] BuildLines(EndingDefinition ending, ConclusionDefinition selectedConclusion)
    {
        var lines = new List<string> { OpeningLine };

        // Result header - authored title/displayName only, never a raw id and never a
        // score/correctness judgment.
        if (!string.IsNullOrEmpty(ending.title))
        {
            lines.Add(ending.title);
        }
        if (selectedConclusion != null && !string.IsNullOrEmpty(selectedConclusion.displayName))
        {
            lines.Add($"Submitted conclusion: {selectedConclusion.displayName}");
        }

        // endingText may be authored as several blank-line-separated paragraphs, each
        // shown as its own auto-advancing line - see EndingDefinition.endingText's own
        // tooltip. A single block (no blank lines) displays as one line.
        if (!string.IsNullOrEmpty(ending.endingText))
        {
            var paragraphs = ending.endingText.Split(new[] { "\n\n", "\r\n\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var paragraph in paragraphs)
            {
                string trimmed = paragraph.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    lines.Add(trimmed);
                }
            }
        }

        if (!SceneTurnover.IsGated) { lines.Add(ClosingLine); lines.Add(FarewellLine); }

        return lines.ToArray();
    }

    private IEnumerator PlayLines(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            SetLine(lines[i]);
            yield return new WaitForSeconds(lineDisplaySeconds);
        }

        sequenceRoutine = null;

        if (SceneTurnover.IsGated && SceneTurnover.Phase != ScenarioPhase.InvestigationComplete)
        {
            awaitingCloseout = true;
            hintLabel.text = "Next: Transfer your stored evidence to the evidence box.";
            SetLine((string.IsNullOrEmpty(submittedConclusionName)?"Your conclusion":"<b>"+submittedConclusionName+"</b>")
                + "\n\nYour conclusion has been recorded. Continue to evidence custody to secure the collected items and complete scene closeout.");
            custodyContinueButton.SetActive(true); feedbackLayout.Refresh();
            yield break;
        }

        // Straight into the player-paced review - no extra "press A to begin" gate, since
        // the review itself already starts on the first page rather than an empty screen.
        if (reviewPages.Count > 0)
        {
            inReviewPhase = true;
            reviewIndex = 0;
            ShowCurrentReviewPage();
        }
        else
        {
            EnterAwaitingReturn();
        }
    }

    private void EnterAwaitingReturn()
    {
        awaitingReturn = true;
        if (hintLabel != null)
        {
            hintLabel.text = "Press A to return to the main menu.";
        }
    }

    private void SetLine(string text)
    {
        if (lineLabel != null)
        {
            lineLabel.text = EvidenceDisplay.Text(text);
            feedbackLayout?.Refresh();
        }
    }

    private void ShowCurrentReviewPage()
    {
        string page=reviewPages[reviewIndex];
        int headingEnd=page.IndexOf('\n');
        string heading=headingEnd>=0?page.Substring(0,headingEnd).Trim():"Case review";
        feedbackHeading.text=heading=="CASE COMPLETE"?"Case summary":heading;
        reviewProgress.text="Case complete   |   Section "+(reviewIndex+1)+" of "+reviewPages.Count;
        SetLine(headingEnd>=0?page.Substring(headingEnd+1).Trim():page);
        bool last=reviewIndex==reviewPages.Count-1;
        reviewNextLabel.text=last?"[A] Finish review":"[A] Next";
        previousReviewTarget.enabled=reviewIndex>0;
        reviewPreviousButton.color=reviewIndex>0?ForensicUIStyle.SelectedFill:ForensicUIStyle.HeaderBackground;
        hintLabel.text="Right stick: scroll   |   [B] Previous section";
        SetFinalReviewControls(true);
        feedbackLayout.Body.Scroll.verticalNormalizedPosition=1;
        feedbackLayout.Body.Content.anchoredPosition=Vector2.zero;
        reviewInputAfter=Time.unscaledTime+.25f;
    }
    public void NextReviewPage()
    {
        if(!IsInFinalReview||Time.unscaledTime<reviewInputAfter||PlayerUIGate.IsBlockedByOther(this))return;
        if(reviewIndex>=reviewPages.Count-1) { ReturnToMenu(); return; }
        reviewIndex++; ShowCurrentReviewPage();
    }
    public void PreviousReviewPage()
    {
        if(!IsInFinalReview||reviewIndex==0||Time.unscaledTime<reviewInputAfter||PlayerUIGate.IsBlockedByOther(this))return;
        reviewIndex--; ShowCurrentReviewPage();
    }
    private void ReturnFromReview()
    {
        if(IsInFinalReview&&Time.unscaledTime>=reviewInputAfter&&!PlayerUIGate.IsBlockedByOther(this)) ReturnToMenu();
    }
    private void SetFinalReviewControls(bool visible)
    {
        if(finalReviewActions==null)return;
        finalReviewActions.gameObject.SetActive(visible);
        reviewProgressRow.gameObject.SetActive(visible);
        if(visible) custodyContinueButton.SetActive(false);
        feedbackLayout.Refresh();
    }

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }
        feedbackPointer?.Tick();
        float scroll=feedbackScroll!=null?feedbackScroll.ReadValue<Vector2>().y:0;
        if(Mathf.Abs(scroll)>.25f) feedbackLayout?.ScrollOptions(scroll*350*Time.unscaledDeltaTime);

        if (awaitingCloseout)
        {
            if (PlayerUIGate.TryConsumeConfirm(this, submitAction)) ContinueToCloseout();
            return;
        }

        if (inReviewPhase)
        {
            if (closeAction.WasPressedThisFrame()) PreviousReviewPage();
            else if (PlayerUIGate.TryConsumeConfirm(this, submitAction)) NextReviewPage();
            return;
        }

        if (!awaitingReturn)
        {
            return;
        }

        if (PlayerUIGate.TryConsumeConfirm(this, submitAction))
        {
            ReturnToMenu();
        }
    }

    private void ReturnToMenu()
    {
        if (SceneTurnover.IsGated && SceneTurnover.Phase != ScenarioPhase.InvestigationComplete) return;
        ReleaseHolds();
        IsShowing = false;
        LoadMainLobby();
    }

    /// <summary>Confirmed settings exit uses the same authored lobby destination, without completing/uploading an attempt.</summary>
    public void ReturnToLobbyFromSettings()
    {
        HideFeedback();
        LoadMainLobby();
    }

    private void LoadMainLobby()
    {
        StudentIdentificationHandoff.Clear();
        SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>
    /// Test-only escape hatch: closes the screen WITHOUT loading the menu scene.
    /// Deliberately NOT the same code path ReturnToMenu takes - a real SceneManager.LoadScene
    /// call would tear down the very Play session GreyboxFlowTest is running in, which
    /// is a fundamentally different problem from every other test-only dismiss method
    /// in this project (HypothesisCheckpointUI.AnswerForTest, SwabTool, EvidenceBagTool,
    /// ...): those all drive the REAL end state, this one deliberately does not, because
    /// the real end state is destructive to the thing calling it. Public for
    /// GreyboxFlowTest to call after HYP-03 fires mid-run, the same way it already
    /// dismisses a real HypothesisCheckpointUI panel that pops open unexpectedly.
    /// </summary>
    public void DismissForTest()
    {
        HideFeedback();
    }
    private void HideFeedback()
    {
        if (!IsShowing)
        {
            return;
        }

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        ReleaseHolds();
        IsShowing = false;
        awaitingReturn = false;
        inReviewPhase = false;
        awaitingCloseout = false;
        panelRoot.SetActive(false);
        feedbackPointer?.Disable();
    }

    public bool ContinueToCloseout()
    {
        if (!IsShowing || !awaitingCloseout || EndingManager.Instance == null) return false;
        // Release the feedback hold before guidance and the next panel can take ownership.
        HideFeedback();
        // Consume the A acknowledgement for this frame before evidence processing resumes.
        ToolInputLock.Enter(this);
        StartCoroutine(ReleaseAcknowledgementInput());
        return EndingManager.Instance.AcceptConclusionFeedback();
    }
    private IEnumerator ReleaseAcknowledgementInput() { yield return null; ToolInputLock.Exit(this); }

    public void ShowFinalReview(EndingDefinition ending, ConclusionDefinition conclusion)
    {
        if (ending == null || IsShowing || SceneTurnover.Phase != ScenarioPhase.InvestigationComplete) return;
        IsShowing = true; awaitingCloseout = false; awaitingReturn = false;
        feedbackHeading.text="Case review"; custodyContinueButton.SetActive(false); feedbackPointer.Enable();
        LocomotionSuspender.Suspend(); PlayerUIGate.Enter(this); ToolInputLock.Enter(this); HudFollowCamera.ConfigureShared(panelRoot, this, 600);
        panelRoot.SetActive(true);
        BuildReviewPages(ending, conclusion);
        reviewIndex = 0; inReviewPhase = reviewPages.Count > 0;
        if (inReviewPhase) ShowCurrentReviewPage(); else EnterAwaitingReturn();
    }

    private void ReleaseHolds()
    {
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);
        ToolInputLock.Exit(this);
    }

    // --- Post-case review page builders --------------------------------------
    // Each method returns 0 or 1 page of text; BuildReviewPages assembles whichever ones
    // have content. Every one reads live, already-authoritative data - none recalculates
    // anything DeductionScorer/RequirementEvaluator/the various StateManagers already own.

    private void BuildReviewPages(EndingDefinition ending, ConclusionDefinition selectedConclusion)
    {
        reviewPages.Clear();

        AddPageIfNotEmpty(BuildSummaryPage(ending, selectedConclusion));
        AddPageIfNotEmpty(BuildSupportPage(selectedConclusion));
        AddPageIfNotEmpty(BuildInvestigationReviewPage());
        AddPageIfNotEmpty(BuildScenePhotographyPage());
        AddPageIfNotEmpty(BuildMeasurementPage());
        AddPageIfNotEmpty(BuildDeductionPerformancePage());
        AddPageIfNotEmpty(BuildMissedOpportunitiesPage(selectedConclusion));
        AddPageIfNotEmpty(BuildEvidenceReviewPage());
    }

    private void AddPageIfNotEmpty(string page)
    {
        if (!string.IsNullOrEmpty(page))
        {
            reviewPages.Add(page);
        }
    }

    /// <summary>Scene photography summary - omitted entirely in a scene with no scene photography route.</summary>
    private string BuildScenePhotographyPage()
    {
        var manager = ScenePhotographyManager.Instance;
        if (manager == null || manager.Points.Count == 0)
        {
            return null;
        }

        var r = manager.BuildAssessment();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Scene Photography — Training");
        sb.AppendLine();
        sb.AppendLine($"Coverage: {r.completedPointCount}/{r.requiredPointCount}");
        sb.AppendLine($"Sequence: {r.sequenceAccuracyPercent:0}%");
        sb.AppendLine($"Position & Angle: {r.procedureAccuracyPercent:0}%");
        sb.AppendLine();
        foreach (var line in ScenePhotographyScoring.BuildFeedback(r))
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Evidence measurement summary - omitted in a scene with no measurement requirements.
    /// Never shows expected distances: only whether each reference was measured, within
    /// tolerance, and before the item was disturbed.
    /// </summary>
    private string BuildMeasurementPage()
    {
        var manager = MeasurementStateManager.Instance;
        if (manager == null)
        {
            return null;
        }

        var r = manager.BuildAssessment();
        if (r.requirementCount == 0)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Evidence Measurement — Training");
        sb.AppendLine();
        sb.AppendLine($"Both references measured: {r.completedCount}/{r.requirementCount}");
        sb.AppendLine($"Accuracy: {r.accuracyPercent:0}%");
        sb.AppendLine($"Measured before disturbance: {r.timingPercent:0}%");
        sb.AppendLine();
        foreach (var item in r.items)
        {
            string label = ResolveEvidenceLabel(item.evidenceId) ?? "Evidence item";
            sb.AppendLine($"{label}: {DescribeMeasurement(item)}");
        }
        return sb.ToString();
    }

    private static string DescribeMeasurement(MeasurementItemResult item)
    {
        if (!item.completedA && !item.completedB)
        {
            return "not measured";
        }

        var notes = new List<string>();
        if (!item.completedBoth)
        {
            notes.Add("one reference missing");
        }
        if ((item.completedA && !item.withinToleranceA) || (item.completedB && !item.withinToleranceB))
        {
            notes.Add("outside tolerance");
        }
        if (item.late)
        {
            notes.Add("measured after disturbance");
        }
        return notes.Count == 0 ? "complete" : string.Join(", ", notes);
    }

    /// <summary>Page 1: authored conclusion displayName + ending title only - source: ConclusionDefinition/EndingDefinition, never a raw id.</summary>
    private string BuildSummaryPage(EndingDefinition ending, ConclusionDefinition selectedConclusion)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("CASE COMPLETE");
        sb.AppendLine();
        sb.AppendLine("<color=#BEC3CA>Submitted conclusion</color>");
        sb.AppendLine("<b>"+(selectedConclusion != null && !string.IsNullOrEmpty(selectedConclusion.displayName)
            ? selectedConclusion.displayName
            : "No conclusion selected")+"</b>");

        if (selectedConclusion != null && !string.IsNullOrEmpty(selectedConclusion.description))
        {
            sb.AppendLine();
            sb.AppendLine(selectedConclusion.description);
        }
        sb.AppendLine();
        sb.AppendLine("<color=#BEC3CA>Outcome</color>");
        sb.AppendLine("<b>"+(!string.IsNullOrEmpty(ending.title) ? ending.title : "Unresolved")+"</b>");

        return sb.ToString();
    }

    /// <summary>
    /// Page 2: "why this conclusion was supported" - findingIds/relationshipIds are read
    /// directly out of selectedConclusion.availabilityRequirement via
    /// RequirementTreeInspector (never a hardcoded per-conclusion list), then filtered to
    /// ones the player actually confirmed/completed (never shows an unconfirmed finding as
    /// though it were established) and resolved to their own authored displayName.
    /// </summary>
    private string BuildSupportPage(ConclusionDefinition selectedConclusion)
    {
        if (selectedConclusion == null)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Why This Conclusion Was Supported");
        sb.AppendLine();

        bool anyContent = false;
        var fsm = FindingStateManager.Instance;
        if (fsm != null)
        {
            foreach (var findingId in RequirementTreeInspector.CollectFindingIds(selectedConclusion.availabilityRequirement))
            {
                if (!fsm.IsConfirmed(findingId))
                {
                    continue;
                }
                var record = fsm.GetRecord(findingId);
                string label = record?.definition != null && !string.IsNullOrEmpty(record.definition.displayName)
                    ? record.definition.displayName
                    : null;
                if (label == null)
                {
                    continue;
                }
                sb.AppendLine($"• {label}");
                anyContent = true;
            }
        }

        var rsm = RelationshipStateManager.Instance;
        if (rsm != null)
        {
            foreach (var relationshipId in RequirementTreeInspector.CollectRelationshipIds(selectedConclusion.availabilityRequirement))
            {
                var record = rsm.GetRecord(relationshipId);
                if (record == null || record.state != RelationshipState.Completed)
                {
                    continue;
                }
                string label = !string.IsNullOrEmpty(record.definition?.displayName) ? record.definition.displayName : null;
                if (label == null)
                {
                    continue;
                }
                sb.AppendLine($"• {label}");
                anyContent = true;
            }
        }

        return anyContent ? sb.ToString() : null;
    }

    /// <summary>Page 3: procedural stats - source: EvidenceStateManager.AllRecords, FindingStateManager.AllRecords, RelationshipStateManager.AllRecords, SessionLogger.EventCounts/ElapsedSeconds. No invented metrics.</summary>
    private string BuildInvestigationReviewPage()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Investigation Review");
        sb.AppendLine();

        var esm = EvidenceStateManager.Instance;
        if (esm != null)
        {
            int totalEvidence = 0, discovered = 0, fullyProcessed = 0;
            foreach (var record in esm.AllRecords)
            {
                totalEvidence++;
                if (record.status != EvidenceStatus.NotFound)
                {
                    discovered++;
                }
                if (record.status == EvidenceStatus.Processed)
                {
                    fullyProcessed++;
                }
            }
            sb.AppendLine($"Evidence discovered: {discovered} / {totalEvidence}");
            sb.AppendLine($"Evidence fully processed: {fullyProcessed} / {totalEvidence}");
        }

        var fsm = FindingStateManager.Instance;
        if (fsm != null)
        {
            int totalFindings = 0, confirmedFindings = 0;
            foreach (var record in fsm.AllRecords)
            {
                if (record?.definition == null || record.definition.findingId == TestOnlyFindingId)
                {
                    continue;
                }
                totalFindings++;
                if (record.isConfirmed)
                {
                    confirmedFindings++;
                }
            }
            sb.AppendLine($"Confirmed findings: {confirmedFindings} / {totalFindings}");
        }

        var rsm = RelationshipStateManager.Instance;
        if (rsm != null)
        {
            int reachable = 0, completed = 0;
            foreach (var record in rsm.AllRecords)
            {
                if (record.state == RelationshipState.Locked)
                {
                    continue;
                }
                reachable++;
                if (record.state == RelationshipState.Completed)
                {
                    completed++;
                }
            }
            sb.AppendLine($"Relationships completed: {completed} / {reachable}");
        }

        var logger = SessionLogger.Instance;
        if (logger != null)
        {
            int violations = SumEventCounts(logger, SessionEventType.EvidenceTransitionBlocked, SessionEventType.EvidenceMarkerRejected, SessionEventType.MarkerReclaimBlocked, SessionEventType.EvidenceDepositBlocked);
            sb.AppendLine($"Procedural violations: {violations}");

            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(logger.ElapsedSeconds));
            sb.AppendLine($"Investigation time: {totalSeconds / 60}:{(totalSeconds % 60):D2}");
        }

        return sb.ToString();
    }

    private static int SumEventCounts(SessionLogger logger, params SessionEventType[] types)
    {
        int total = 0;
        foreach (var type in types)
        {
            if (logger.EventCounts.TryGetValue(type, out int count))
            {
                total += count;
            }
        }
        return total;
    }

    /// <summary>Page 4: deduction/board score breakdown - source: EvidenceBoardController.LastScoreResult, the exact DeductionScoreResult DeductionScorer already computed at Submit Theory (never recalculated here).</summary>
    private string BuildDeductionPerformancePage()
    {
        if (FinalDeductionAssessment.ActiveInScene)
        {
            var assessment = FinalDeductionAssessment.FinalResult(!SceneTurnover.IsGated || SceneTurnover.Phase == ScenarioPhase.InvestigationComplete);
            if (!assessment.hasFinalScore) return "Deduction Performance\n\n" + (assessment.status == "requires_review"
                ? "Assessment requires review. A final grade is unavailable." : "Investigation incomplete. Final assessment pending.");
            return $"Deduction Performance\n\nReasoning Checkpoints: {assessment.reasoningComponent:0.##} / {FinalDeductionAssessment.Rules.checkpointWeight:0.##}\n"
                + $"Evidence Relationships: {assessment.relationshipComponent:0.##} / {FinalDeductionAssessment.Rules.relationshipWeight:0.##}\n"
                + $"Final Conclusion: {assessment.conclusionComponent:0.##} / {FinalDeductionAssessment.Rules.conclusionWeight:0.##}\n\n"
                + $"<b>Overall deduction score</b>\n<size=36>{assessment.totalScore:0.##}%</size>";
        }
        var result = EvidenceBoardController.LastScoreResult;
        if (result == null)
        {
            return null;
        }

        var r = result.Value;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Deduction Performance");
        sb.AppendLine();
        sb.AppendLine($"Required links: {Mathf.RoundToInt(r.requiredLinksScore * 100f)}%");
        sb.AppendLine($"Contradiction avoidance: {Mathf.RoundToInt(r.contradictionScore * 100f)}%");
        sb.AppendLine($"Time efficiency: {Mathf.RoundToInt(r.timeScore * 100f)}%");
        sb.AppendLine();
        sb.AppendLine($"Overall deduction score: {Mathf.RoundToInt(r.total * 100f)}%");

        return sb.ToString();
    }

    /// <summary>
    /// Page 5: neutral, procedure-level "what wasn't finished" - scoped to whatever
    /// findingId/relationshipId any authored Case-level ConclusionDefinition's own
    /// requirement tree references (the scenario's actual case-relevant set), minus
    /// whatever the player already confirmed/completed. Phrasing is generated from the
    /// unconfirmed FindingDefinition's own unlockRequirement leaf TYPE (describes the kind
    /// of procedure, e.g. "fingerprint comparison"), never from which suspect it happens to
    /// implicate - see DescribeMissingProcedure.
    /// </summary>
    private string BuildMissedOpportunitiesPage(ConclusionDefinition selectedConclusion)
    {
        var cm = ConclusionStateManager.Instance;
        var fsm = FindingStateManager.Instance;
        var rsm = RelationshipStateManager.Instance;
        if (cm == null || fsm == null)
        {
            return null;
        }

        var caseRelevantFindingIds = new List<string>();
        var caseRelevantRelationshipIds = new List<string>();
        var seenFindings = new HashSet<string>();
        var seenRelationships = new HashSet<string>();

        foreach (var record in cm.AllRecords)
        {
            if (record?.definition == null || record.definition.level != ConclusionLevel.Case)
            {
                continue;
            }

            foreach (var findingId in RequirementTreeInspector.CollectFindingIds(record.definition.availabilityRequirement))
            {
                if (seenFindings.Add(findingId))
                {
                    caseRelevantFindingIds.Add(findingId);
                }
            }

            foreach (var relationshipId in RequirementTreeInspector.CollectRelationshipIds(record.definition.availabilityRequirement))
            {
                if (seenRelationships.Add(relationshipId))
                {
                    caseRelevantRelationshipIds.Add(relationshipId);
                }
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Missed Investigation Opportunities");
        sb.AppendLine();
        bool anyContent = false;

        // Two different unconfirmed findings on the same evidence item can generate the
        // same generic phrasing (e.g. both gated by a distinct ReportLineSelected leaf) -
        // dedup by the rendered line itself rather than the findingId, so the page never
        // repeats an identical bullet.
        var seenLines = new HashSet<string>();
        foreach (var findingId in caseRelevantFindingIds)
        {
            if (fsm.IsConfirmed(findingId))
            {
                continue;
            }
            var record = fsm.GetRecord(findingId);
            string line = DescribeMissingProcedure(record?.definition);
            if (line == null || !seenLines.Add(line))
            {
                continue;
            }
            sb.AppendLine($"• {line}");
            anyContent = true;
        }

        if (rsm != null)
        {
            foreach (var relationshipId in caseRelevantRelationshipIds)
            {
                var record = rsm.GetRecord(relationshipId);
                if (record == null || record.state == RelationshipState.Completed || record.state == RelationshipState.Locked)
                {
                    continue;
                }
                string label = !string.IsNullOrEmpty(record.definition?.displayName) ? record.definition.displayName : null;
                if (label == null)
                {
                    continue;
                }
                sb.AppendLine($"• Deduction link available but never connected: {label}");
                anyContent = true;
            }
        }

        return anyContent ? sb.ToString() : null;
    }

    /// <summary>
    /// Generic, procedure-level phrasing for an unconfirmed finding - reads only the
    /// KIND of leaf its unlockRequirement uses (which procedure category) plus the source
    /// evidence's own displayName, never the finding's own displayName/description (which
    /// would describe the forensic RESULT, i.e. the hidden answer itself - see this
    /// feature's own "describe missed procedures, not hidden suspect answers" rule).
    /// </summary>
    private static string DescribeMissingProcedure(FindingDefinition definition)
    {
        if (definition == null || definition.unlockRequirement == null)
        {
            return null;
        }

        string evidenceLabel = ResolveEvidenceLabel(definition.sourceEvidenceId);
        var leaf = FindFirstLeaf(definition.unlockRequirement);
        if (leaf == null)
        {
            return evidenceLabel != null ? $"{evidenceLabel}: additional investigation not completed." : null;
        }

        string procedureName = leaf.requirementType switch
        {
            RequirementType.FingerprintLabStatusAtOrAbove => "Fingerprint comparison",
            RequirementType.EvidenceSwabbed => "Biological trace sampling",
            RequirementType.EvidenceFingerprinted => "Fingerprint dusting/lifting",
            RequirementType.ReportLineSelected => "Full report detail",
            RequirementType.EvidenceFlipped => "Full examination",
            RequirementType.ScenePhotographyCompleted => "Scene photography",
            RequirementType.MeasurementCompleted => "Position measurement",
            _ => "Additional processing"
        };

        return evidenceLabel != null
            ? $"{evidenceLabel}: {procedureName.ToLowerInvariant()} not completed."
            : $"{procedureName} not completed.";
    }

    private static RequirementNode FindFirstLeaf(RequirementNode node)
    {
        if (node == null)
        {
            return null;
        }
        if (node.kind == RequirementNodeKind.Leaf)
        {
            return node;
        }
        if (node.children == null)
        {
            return null;
        }
        foreach (var child in node.children)
        {
            var found = FindFirstLeaf(child);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static string ResolveEvidenceLabel(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId))
        {
            return null;
        }
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        return record?.definition != null ? record.definition.PlayerDisplayName : EvidenceDisplay.Label(evidenceId);
    }

    /// <summary>Page 6: compact per-evidence procedural checklist - source: EvidenceStateManager.AllRecords/EvidenceDefinition flags, never a hardcoded evidence list.</summary>
    private string BuildEvidenceReviewPage()
    {
        var esm = EvidenceStateManager.Instance;
        if (esm == null)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Evidence Review");
        sb.AppendLine();

        bool any = false;
        foreach (var record in esm.AllRecords)
        {
            if (record?.definition == null)
            {
                continue;
            }

            any = true;
            sb.AppendLine("<b>"+record.definition.PlayerDisplayName+"</b>");
            sb.AppendLine(Check(record.status != EvidenceStatus.NotFound) + " Discovered");

            if (record.definition.requiresSwabbing)
            {
                sb.AppendLine(Check(record.swabbingDone) + " Swabbed");
            }
            if (record.definition.requiresFingerprinting)
            {
                sb.AppendLine(Check(record.fingerprintingDone) + " Fingerprint processed");
            }
            sb.AppendLine(Check(record.status >= EvidenceStatus.Logged) + " Recorded");
            if(record.definition.examinedInPlace)
            {
                sb.AppendLine(Check(record.status>=EvidenceStatus.Logged)+" Examined in place");
                if(record.definition.requiresBodyRemoval) sb.AppendLine(Check(record.bodyRemovalStatus==BodyRemovalStatus.Removed)+" Medico-legal transport");
            }
            else sb.AppendLine(Check(record.status >= EvidenceStatus.Sealed) + (record.definition.collectedBySwab?" Sample collected":" Collected"));
            sb.AppendLine();
        }

        return any ? sb.ToString() : null;
    }

    private static string Check(bool value) => value ? "<color=#73B885>Done</color>" : "<color=#BDC2C9>Pending</color>";

    // --- Runtime fallback UI -------------------------------------------------
    // A smaller, purpose-built duplicate of HypothesisCheckpointUI.BuildRuntimeUI's
    // recipe - same World Space canvas + HudFollowCamera + dark background approach,
    // no option list since this screen never asks the player to choose from several
    // items (page navigation is a single Next/Close pair, not a highlightable list).

    [EditableSceneBuilder]
    private void BuildRuntimeUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildRuntimeUI"))
        {
        var canvasGo = EditableSceneVisuals.Create("CompletionScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudFollowCamera));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = PanelSortingOrder;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 650f);
        canvasRect.localScale = Vector3.one * 0.001f;

        var follow = canvasGo.GetComponent<HudFollowCamera>();
        follow.Distance = 0.75f;

        var background = CreateChildImage(canvasRect, "Background", new Color(0.06f, 0.06f, 0.08f, 0.96f));
        Stretch(background.rectTransform);

        lineLabel = CreateChildText(canvasRect, "LineLabel", 30f, TextAlignmentOptions.TopLeft);
        var lineRect = lineLabel.rectTransform;
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.offsetMin = new Vector2(60f, 100f);
        lineRect.offsetMax = new Vector2(-60f, -60f);

        hintLabel = CreateChildText(canvasRect, "HintLabel", 20f, TextAlignmentOptions.Center);
        var hintRect = hintLabel.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.offsetMin = new Vector2(40f, 30f);
        hintRect.offsetMax = new Vector2(-40f, 0f);
        hintRect.sizeDelta = new Vector2(hintRect.sizeDelta.x, 70f);
        hintLabel.color = new Color(0.7f, 0.7f, 0.75f, 1f);

        panelRoot = canvasGo;
            }
        BuildFeedbackDetails();
    }

    private void BuildFeedbackDetails()
    {
        if(feedbackLayout!=null) return;
        using(EditableSceneVisuals.Begin(this,"BuildFeedbackDetails"))
        {
            var rect=(RectTransform)panelRoot.transform;
            feedbackHeading=ForensicUIStyle.Text(rect,"Feedback heading",36,ForensicUIStyle.TextPrimary,FontStyles.Bold);
            feedbackPointer=new WorldPanelPointer(rect,"ConclusionFeedback",this);
            feedbackLayout=ResponsivePanelLayout.Create(rect,feedbackHeading,null,hintLabel,760,760,40);
            feedbackLayout.AddText(lineLabel,feedbackLayout.Body,80,20);
            var button=ForensicUIStyle.Rect(feedbackLayout.Footer.Content,"Continue to evidence custody",ForensicUIStyle.Accent);
            custodyContinueButton=button.gameObject;
            var height=EditableSceneVisuals.Component<LayoutElement>(button.gameObject);
            height.minHeight=height.preferredHeight=76; height.flexibleHeight=0;
            var label=ForensicUIStyle.Text(button.transform,"Continue label",28,ForensicUIStyle.PanelBackground,FontStyles.Bold,TextAlignmentOptions.Center);
            label.text="[A] Continue to evidence custody";
            label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(16,10); label.rectTransform.offsetMax=new Vector2(-16,-10);
            button.transform.SetAsFirstSibling();
            feedbackPointer.Register("feedback-custody",button.rectTransform,Vector2.zero,()=>ContinueToCloseout(),
                hover=>button.color=hover?new Color(.96f,.82f,.51f):ForensicUIStyle.Accent);
            feedbackLayout.RegisterControls(feedbackPointer,"feedback-scroll-");
        }
        panelRoot.GetComponent<HudFollowCamera>().Distance=1.25f;
        lineLabel.fontSize=29; lineLabel.alignment=TextAlignmentOptions.TopLeft; lineLabel.lineSpacing=8;
        hintLabel.fontSize=23; hintLabel.color=ForensicUIStyle.TextSecondary;
        panelRoot.transform.Find("Background").GetComponent<Image>().color=ForensicUIStyle.PanelBackground;
        custodyContinueButton.SetActive(false);
        feedbackScroll=new InputAction("ConclusionFeedback_Scroll",InputActionType.Value,"<XRController>{RightHand}/thumbstick",expectedControlType:"Vector2");
        BuildFinalReviewDetails();
    }

    private void BuildFinalReviewDetails()
    {
        using(EditableSceneVisuals.Begin(this,"BuildFinalReviewDetails"))
        {
            reviewProgress=ForensicUIStyle.Text(panelRoot.transform,"Review progress",26,ForensicUIStyle.TextSecondary);
            reviewProgressRow=feedbackLayout.AddText(reviewProgress,feedbackLayout.Header,42,8);
            finalReviewActions=ResponsivePanelLayout.Rect(feedbackLayout.Footer.Content,"Final review actions");
            var height=EditableSceneVisuals.Component<LayoutElement>(finalReviewActions.gameObject);
            height.minHeight=height.preferredHeight=76; height.flexibleHeight=0;
            reviewPreviousButton=ReviewButton("Previous section","[B] Previous",0,.28f,ForensicUIStyle.SelectedFill,ForensicUIStyle.TextPrimary,26,out _);
            var next=ReviewButton("Next section","[A] Next",.31f,.69f,ForensicUIStyle.Accent,ForensicUIStyle.PanelBackground,28,out reviewNextLabel);
            var exit=ReviewButton("Return to lobby","Return to lobby",.72f,1,ForensicUIStyle.SelectedFill,ForensicUIStyle.TextPrimary,24,out _);
            previousReviewTarget=feedbackPointer.Register("review-previous",reviewPreviousButton.rectTransform,Vector2.zero,PreviousReviewPage);
            feedbackPointer.Register("review-next",next.rectTransform,Vector2.zero,NextReviewPage);
            feedbackPointer.Register("review-lobby",exit.rectTransform,Vector2.zero,ReturnFromReview);
            finalReviewActions.SetAsFirstSibling();
        }
        reviewProgress.alignment=TextAlignmentOptions.MidlineLeft;
        SetFinalReviewControls(false);
    }
    private Image ReviewButton(string name,string text,float left,float right,Color fill,Color ink,float fontSize,out TMP_Text label)
    {
        var image=ForensicUIStyle.Rect(finalReviewActions,name,fill);
        var rect=image.rectTransform; rect.anchorMin=new Vector2(left,0); rect.anchorMax=new Vector2(right,1);
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        label=ForensicUIStyle.Text(rect,name+" label",fontSize,ink,FontStyles.Bold,TextAlignmentOptions.Center);
        label.text=text; label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=new Vector2(12,10); label.rectTransform.offsetMax=new Vector2(-12,-10);
        return image;
    }

    private static Image CreateChildImage(RectTransform parent, string name, Color color)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateChildText(RectTransform parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.color = Color.white;
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
