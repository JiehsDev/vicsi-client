// Assets/_Project/Scripts/UI/RecorderReportUI.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The report-writing panel RecorderTool opens once an item reaches PhotographedFinal:
/// a pool of candidate ReportLineOption chips the player moves into a Notes list,
/// multiple choice rather than free text, same "real judgment, fully scoreable, no VR
/// keyboard" reasoning already applied to the Hypothesis Checkpoint panel.
///
/// Follows this project's established exclusive-panel convention rather than inventing
/// a second one - LocomotionSuspender + PlayerUIGate held for as long as the panel is
/// up, an authored-or-generated UI (assign the fields below by hand, or leave them
/// blank and a functional panel is built at runtime), same shape as
/// HypothesisCheckpointUI/MasterSketchUI. Unlike HypothesisCheckpointUI (a forced,
/// non-dismissible hard gate), this panel is DEFERENTIAL like PhotoAlbumUI/the camera
/// viewfinder: Show() refuses to open while PlayerUIGate.IsBlocked, so a player already
/// in another exclusive screen never gets this one stacked on top uninvited. It has no
/// cancel/close binding once open, though - same as HypothesisCheckpointUI, writing
/// the report is a real procedural step, not a screen you back out of once you've
/// committed to entering it.
///
/// Selection is raw-InputAction driven (right thumbstick to move the cursor, B to move
/// the focused line between Pool and Notes, A to confirm), the same
/// HypothesisCheckpointUI/ToolWheelController pattern every other panel in this project
/// already uses successfully - NOT the Meta ISDK ray -> PointableCanvasModule -> uGUI
/// pointer pipeline this panel originally tried (real drag-and-drop chips). That
/// pipeline was confirmed dead for this panel specifically: a hover-highlight test
/// never fired even though the player's own controller ray works fine elsewhere, and
/// no other UI in this project (Tool Wheel, Hypothesis Checkpoint, the Deduction Board
/// as actually built in the scene) turned out to depend on it either. Thumbstick+button
/// input sidesteps the question entirely by never touching that pipeline.
/// </summary>
public class RecorderReportUI : MonoBehaviour
{
    public static RecorderReportUI Instance { get; private set; }
    public const int RequiredLineCount = 3;

    private const float NavDeadzone = 0.5f;

    private enum Column
    {
        Pool,
        Notes
    }

    [Header("Authored UI (optional - leave blank to auto-generate at runtime)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private TMP_Text hintLabel;

    [Header("Optional recovered-content block (EvidenceDefinition.loggingContext)")]
    [Tooltip("Whole context block, header + scrollable body - toggled active/inactive per item in Show(), based on whether that item's EvidenceDefinition.loggingContext is non-empty. Left null/blank for every evidence item, this stays hidden and the panel behaves exactly as it did before this feature existed.")]
    [SerializeField] private GameObject contextPanel;
    [SerializeField] private TMP_Text contextBodyLabel;
    [Tooltip("Optional header of the context block; set to 'Evidence Record' when the block shows spatial measurements.")]
    [SerializeField] private TMP_Text contextHeaderLabel;
    [SerializeField] private ScrollRect contextScrollRect;

    [Tooltip("Parent chips are cloned into before the player moves them out - give it a VerticalLayoutGroup.")]
    [SerializeField] private RectTransform poolContainer;

    [Tooltip("Where committed chips end up. Needs a VerticalLayoutGroup to lay them out.")]
    [SerializeField] private RectTransform notesContainer;

    [Tooltip("An inactive ReportLineChip used as the template for each candidate line.")]
    [SerializeField] private ReportLineChip chipTemplate;

    [Tooltip("Purely visual - tints to show whether Confirm (the A button) would currently do anything. Not click-driven; see confirmAction.")]
    [SerializeField] private Image confirmIndicator;
    [SerializeField] private Color confirmReadyColor = new(0.7f, 0.9f, 0.7f, 1f);
    [SerializeField] private Color confirmNotReadyColor = new(0.4f, 0.4f, 0.4f, 1f);

    /// <summary>True while the report panel is on screen.</summary>
    public bool IsShowing { get; private set; }

    private readonly List<ReportLineChip> spawnedChips = new();
    private string currentEvidenceId;
    private Func<string, List<string>, TransitionResult> confirmCallback;

    // Navigation cursor: which column has focus, and each column's own remembered
    // index, so swapping columns and back doesn't lose your place. Both are clamped
    // every time a chip moves between lists, since that's the one thing that changes
    // list lengths out from under the cursor.
    private Column focusedColumn;
    private int poolIndex;
    private int notesIndex;
    private bool stickVerticalNeutral = true;
    private bool stickHorizontalNeutral = true;

    private InputAction navStickAction;
    private ResponsivePanelLayout.Area poolArea, notesArea;
    private WorldPanelPointer scrollPointer;
    private InputAction selectAction;
    private InputAction confirmAction;

    // Whatever tool was virtually equipped when Show() opened the panel (almost
    // always the Recorder itself), and the hand anchor it was equipped to - saved
    // so ReleaseHolds can hand it back once the report is done. See Show()'s own
    // comment for why holstering it at all is necessary.
    private ToolType heldToolRoleBeforeShow = ToolType.None;
    private Transform heldToolHandAnchor;

    // --- Recovered-content block layout (Requirement 4/5) ------------------------
    // The panel's canvas is now a fixed, slightly taller 1000x900 (up from 1000x700)
    // so there's room for an optional context block above the prompt/columns, but
    // every element below the prompt is repositioned between two fixed layouts each
    // Show() call rather than living at one static position - "collapsed" (no
    // context authored for this item) replicates the ORIGINAL 700-tall panel's exact
    // internal margins from the top edge (shifted +100 to re-align with the new,
    // taller canvas's top edge), so an item with no loggingContext looks and feels
    // completely unchanged from before this feature existed - never a blank gap
    // where a context block would otherwise go. "expanded" (context authored) makes
    // real room at the top for it. confirmIndicator is bottom-anchored and never
    // needs to move between the two.
    private static readonly Vector2 PromptPosCollapsed = new(0f, 400f);
    private static readonly Vector2 PromptPosExpanded = new(0f, 160f);
    private static readonly Vector2 HeaderPosCollapsed = new(0f, 330f); // x sign applied per-side at use
    private static readonly Vector2 HeaderPosExpanded = new(0f, 95f);
    private static readonly Vector2 ColumnPosCollapsed = new(0f, 80f);
    private static readonly Vector2 ColumnPosExpanded = new(0f, -130f);
    private static readonly Vector2 HintPosCollapsed = new(0f, -200f);
    private static readonly Vector2 HintPosExpanded = new(0f, -400f);

    private RectTransform poolHeaderRect;
    private RectTransform notesHeaderRect;
    private RectTransform notesBorderRect;

    private void Awake()
    {
        Instance = this;

        // Same raw-XR-binding pattern as HypothesisCheckpointUI/ToolWheelController.
        // B moves the focused line between Pool and Notes (the same action either
        // way - which list it's currently in decides the direction); A confirms.
        navStickAction = new InputAction("RecorderReport_NavStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        selectAction = new InputAction("RecorderReport_Select", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        confirmAction = PlayerUIGate.CreateConfirmAction(this, "RecorderReport_Confirm", () => IsShowing, 300);

        if (panelRoot == null || GetComponent<EditableSceneVisuals>() != null)
        {
            BuildRuntimeUI();
        }

        if (chipTemplate != null)
        {
            chipTemplate.gameObject.SetActive(false);
        }

        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        navStickAction.Enable();
        selectAction.Enable();
        confirmAction.Enable();
    }

    private void OnDisable()
    {
        scrollPointer?.Disable();
        navStickAction.Disable();
        selectAction.Disable();
        confirmAction.Disable();

        // Defensive: never leave locomotion suspended or the UI gate held if this
        // panel is torn down while a report is in progress.
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    private void OnDestroy()
    {
        scrollPointer?.Dispose();
        if (Instance == this)
        {
            Instance = null;
        }

        navStickAction?.Dispose();
        selectAction?.Dispose();
        confirmAction?.Dispose();
    }

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }
        scrollPointer?.Tick();

        if (PlayerUIGate.TryConsumeConfirm(this, confirmAction))
        {
            Confirm();
            return;
        }

        if (selectAction.WasPressedThisFrame())
        {
            ToggleFocusedChip();
        }

        Vector2 stick = navStickAction.ReadValue<Vector2>();

        if (Mathf.Abs(stick.y) < NavDeadzone)
        {
            stickVerticalNeutral = true;
        }
        else if (stickVerticalNeutral)
        {
            stickVerticalNeutral = false;
            MoveCursor(stick.y > 0f ? -1 : 1);
        }

        if (Mathf.Abs(stick.x) < NavDeadzone)
        {
            stickHorizontalNeutral = true;
        }
        else if (stickHorizontalNeutral)
        {
            stickHorizontalNeutral = false;
            SwitchColumn(stick.x > 0f ? Column.Notes : Column.Pool);
        }
    }

    /// <summary>
    /// Opens the panel for one evidence item's reportLineOptions. onConfirm is called
    /// exactly once, when the player presses Confirm, with (evidenceId, the
    /// ReportLineOption ids currently in the Notes zone) - it returns the
    /// TransitionResult of whatever it did (normally MarkLogged), which Confirm()
    /// passes back up so a caller (RecorderTool, or GreyboxFlowTest exercising this
    /// same path) can tell success from refusal. Caller's responsibility to have
    /// already gate-checked PhotographedFinal and confirmed reportLineOptions is
    /// non-empty - this method does not re-check either, same division of labor as
    /// every other panel's Show().
    /// </summary>
    public void Show(EvidenceProp evidence, Func<string, List<string>, TransitionResult> onConfirm)
    {
        if (evidence == null)
        {
            return;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(evidence.evidenceId);
        var definition = record?.definition;
        if (definition == null || definition.reportLineOptions == null || definition.reportLineOptions.Count == 0)
        {
            Debug.LogWarning($"[RecorderReportUI] '{evidence.evidenceId}' has no reportLineOptions; nothing to show.", this);
            return;
        }

        // Defers to whatever else already owns the screen right now, same contract as
        // PhotoAlbumUI/the camera viewfinder - not a forced interrupt like the
        // Hypothesis Checkpoint panel.
        if (!IsShowing && PlayerUIGate.IsBlocked)
        {
            return;
        }

        currentEvidenceId = evidence.evidenceId;
        confirmCallback = onConfirm;

        if (promptLabel != null)
        {
            promptLabel.text = RecorderDocumentation.AppliesTo(evidence.evidenceId)
                ?$"Select three statements that accurately record your examination.\n{definition.PlayerDisplayName} | Choose 3 of 6 statements."
                :$"Choose exactly {RequiredLineCount} report lines for {definition.PlayerDisplayName}.\nThumbstick to choose a line, B to move it into or out of Notes.";
        }
        if(RecorderDocumentation.AppliesTo(evidence.evidenceId))
        {
            if(poolHeaderRect!=null)poolHeaderRect.GetComponent<TMP_Text>().text="Available statements";
            if(notesHeaderRect!=null)notesHeaderRect.GetComponent<TMP_Text>().text="Selected statements";
        }

        // Recovered-content block (Requirement 4/5): shown only while the Recorder
        // panel itself is open for an item whose EvidenceDefinition.loggingContext is
        // authored - never on discovery, never in a notification, never before this
        // panel's own normal open gate (PhotographedFinal, already enforced by
        // RecorderTool before Show() is ever called - see that class's own comment).
        // Every other item's contextPanel stays inactive, exactly as before this field
        // existed.
        // The item's spatial record - the distances the student actually recorded (fixed
        // references, then evidence distances), at the tape measure's precision. Logging
        // waits for complete scene measurements, so this is the full record by now.
        string spatial = MeasurementStateManager.Instance != null ? MeasurementStateManager.Instance.FormatSpatialSummary(evidence.evidenceId) : string.Empty;
        bool hasSpatial = !string.IsNullOrEmpty(spatial);
        bool hasLoggingContext = hasSpatial || !string.IsNullOrEmpty(definition.loggingContext);
        if (contextPanel != null)
        {
            contextPanel.SetActive(hasLoggingContext);
        }
        if (contextHeaderLabel != null)
        {
            contextHeaderLabel.text = hasSpatial ? "Evidence Record" : "Recovered Messages";
        }
        if (hasLoggingContext && contextBodyLabel != null)
        {
            string body = hasSpatial ? "<b>Spatial measurements</b>\n" + spatial : string.Empty;
            if (!string.IsNullOrEmpty(definition.loggingContext))
            {
                body += (hasSpatial ? "\n\n<b>Recovered messages</b>\n" : string.Empty) + definition.loggingContext;
            }
            contextBodyLabel.text = body;
            if (contextScrollRect != null)
            {
                // Reset scroll position to the top each time the panel opens for this
                // item - a stale mid-scroll position from a previous open would look
                // like clipped/missing text at a glance.
                contextScrollRect.verticalNormalizedPosition = 1f;
            }
        }
        ApplyContextLayout(hasLoggingContext);

        // Versioned Recorder choices are always six neutral candidates. Their
        // assessment metadata is never rendered or used as an interaction gate.
        BuildChips(RecorderDocumentation.Options(evidence.evidenceId)??definition.reportLineOptions);
        UpdateConfirmIndicator();
        UpdateHint();

        if (!IsShowing)
        {
            // Holster whatever tool is currently virtually equipped - almost
            // certainly the Recorder itself, since the player just used it to reach
            // this panel. The right thumbstick/A/B this panel reads are exactly what
            // an equipped tool would otherwise be reading too (see RecorderTool,
            // ToolWheelController); holstering it for the duration of the report and
            // re-equipping on Hide() (see ReleaseHolds) stops the tool contesting
            // that input the whole time the panel is up.
            heldToolRoleBeforeShow = PlayerToolRegistry.VirtuallyEquippedRole;
            if (heldToolRoleBeforeShow != ToolType.None)
            {
                var equippedTool = PlayerToolRegistry.GetTool(heldToolRoleBeforeShow);
                heldToolHandAnchor = equippedTool != null ? equippedTool.transform.parent : null;
                PlayerToolRegistry.HolsterCurrent();
            }

            LocomotionSuspender.Suspend();
            PlayerUIGate.Enter(this);
            IsShowing = true;
        }

        HudFollowCamera.ConfigureShared(panelRoot, this, 300);
        panelRoot.SetActive(true);
        scrollPointer?.Enable();
        InteractionFeedback.Confirm(panelRoot.transform);
    }

    public void Hide()
    {
        scrollPointer?.Disable();
        panelRoot.SetActive(false);

        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }

        ClearChips();
        currentEvidenceId = null;
        confirmCallback = null;
    }

    /// <summary>
    /// Switches promptLabel/pool-notes headers/columns/hintLabel between the
    /// "collapsed" (no context block shown) and "expanded" (context block shown)
    /// anchored positions - see the layout constants' own comment. A no-op for any
    /// authored (non-runtime) UI that doesn't wire poolHeaderRect/notesHeaderRect/
    /// notesBorderRect - those fields are only ever populated by BuildRuntimeUI.
    /// </summary>
    private void ApplyContextLayout(bool expanded)
    {
        if (promptLabel != null)
        {
            promptLabel.rectTransform.anchoredPosition = expanded ? PromptPosExpanded : PromptPosCollapsed;
        }

        Vector2 headerPos = expanded ? HeaderPosExpanded : HeaderPosCollapsed;
        if (poolHeaderRect != null) poolHeaderRect.anchoredPosition = new Vector2(-240f, headerPos.y);
        if (notesHeaderRect != null) notesHeaderRect.anchoredPosition = new Vector2(240f, headerPos.y);

        Vector2 columnPos = expanded ? ColumnPosExpanded : ColumnPosCollapsed;
        if (poolArea != null) poolArea.Root.anchoredPosition = new Vector2(-240f, columnPos.y);
        else if (poolContainer != null) poolContainer.anchoredPosition = new Vector2(-240f, columnPos.y);
        if (notesBorderRect != null) notesBorderRect.anchoredPosition = new Vector2(240f, columnPos.y);
        // notesContainer is a child of notesBorderRect at Vector2.zero (see
        // BuildRuntimeUI) - it moves for free with its parent, no separate update needed.

        if (hintLabel != null)
        {
            hintLabel.rectTransform.anchoredPosition = expanded ? HintPosExpanded : HintPosCollapsed;
        }
    }

    private void ReleaseHolds()
    {
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);

        // Give back whatever was holstered in Show(), on the same hand anchor it
        // came from. ToggleEquip re-equips rather than toggling off here, since
        // HolsterCurrent already cleared VirtuallyEquippedRole back in Show() - the
        // role no longer being equipped is exactly what makes ToggleEquip equip it
        // again instead of holstering it a second time.
        if (heldToolRoleBeforeShow != ToolType.None && heldToolHandAnchor != null)
        {
            PlayerToolRegistry.ToggleEquip(heldToolRoleBeforeShow, heldToolHandAnchor);
        }
        heldToolRoleBeforeShow = ToolType.None;
        heldToolHandAnchor = null;
    }

    // --- Navigation -------------------------------------------------------------

    private RectTransform ContainerFor(Column column) => column == Column.Pool ? poolContainer : notesContainer;

    private int IndexFor(Column column) => column == Column.Pool ? poolIndex : notesIndex;

    private void SetIndexFor(Column column, int value)
    {
        if (column == Column.Pool)
        {
            poolIndex = value;
        }
        else
        {
            notesIndex = value;
        }
    }

    /// <summary>
    /// Real, navigable chips currently parented under a container, in sibling order.
    /// Deliberately excludes chipTemplate itself: it always lives as an inactive
    /// sibling inside poolContainer (see BuildChips/BuildRuntimeUI), and indexing raw
    /// RectTransform children instead of this list would let the cursor land on that
    /// invisible template and B-press it into Notes - confirmed live via a probe
    /// before this filtering existed.
    /// </summary>
    private List<ReportLineChip> GetChips(RectTransform container)
    {
        var result = new List<ReportLineChip>();
        if (container == null)
        {
            return result;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            var chip = container.GetChild(i).GetComponent<ReportLineChip>();
            if (chip != null && chip != chipTemplate)
            {
                result.Add(chip);
            }
        }
        return result;
    }

    /// <summary>Moves the cursor up/down (delta -1/+1) within whichever column currently has focus.</summary>
    private void MoveCursor(int delta)
    {
        var chips = GetChips(ContainerFor(focusedColumn));
        if (chips.Count == 0)
        {
            return;
        }

        int newIndex = Mathf.Clamp(IndexFor(focusedColumn) + delta, 0, chips.Count - 1);
        SetIndexFor(focusedColumn, newIndex);
        RefreshFocusVisual();
    }

    /// <summary>Switches which column has focus - a no-op if it's already the requested one, since the stick can be held past the deadzone on one axis while the other axis is also moving.</summary>
    private void SwitchColumn(Column column)
    {
        if (focusedColumn == column)
        {
            return;
        }

        focusedColumn = column;
        var chips = GetChips(ContainerFor(column));
        int clamped = chips.Count == 0 ? 0 : Mathf.Clamp(IndexFor(column), 0, chips.Count - 1);
        SetIndexFor(column, clamped);
        RefreshFocusVisual();
    }

    /// <summary>B was pressed - move whichever chip is under the cursor to the other column, then keep focus in the same column (now one shorter) so repeated presses process lines without re-navigating.</summary>
    private void ToggleFocusedChip()
    {
        var chips = GetChips(ContainerFor(focusedColumn));
        if (chips.Count == 0)
        {
            return;
        }

        int index = IndexFor(focusedColumn);
        if (index < 0 || index >= chips.Count)
        {
            return;
        }

        var chip = chips[index];

        var destination = focusedColumn == Column.Pool ? notesContainer : poolContainer;
        if (destination == null)
        {
            return;
        }

        if (destination == notesContainer)
        {
            if (!TryAddToNotes(chip))
            {
                UpdateHint();
                InteractionFeedback.Blocked();
                return;
            }
        }
        else chip.RectTransform.SetParent(destination, false);

        InteractionSfx.Select();

        int newCount = chips.Count - 1;
        SetIndexFor(focusedColumn, newCount == 0 ? 0 : Mathf.Clamp(index, 0, newCount - 1));

        RefreshFocusVisual();
        UpdateConfirmIndicator();
        UpdateHint();
    }

    /// <summary>Re-applies SetFocused(true) to exactly the one chip the cursor is currently on, and false to every other spawned chip.</summary>
    private void RefreshFocusVisual()
    {
        poolArea?.RefreshFixed(); notesArea?.RefreshFixed();
        var chips = GetChips(ContainerFor(focusedColumn));
        int index = IndexFor(focusedColumn);
        ReportLineChip focusedChip = chips.Count > 0 ? chips[Mathf.Clamp(index, 0, chips.Count - 1)] : null;

        foreach (var chip in spawnedChips)
        {
            if (chip != null)
            {
                chip.SetFocused(chip == focusedChip);
            }
        }
        if (focusedChip != null) (focusedColumn == Column.Pool ? poolArea : notesArea)?.EnsureVisible(focusedChip.RectTransform);
    }

    // --- Confirm --------------------------------------------------------------

    private bool CanConfirm => notesContainer != null && notesContainer.childCount == RequiredLineCount;

    private bool TryAddToNotes(ReportLineChip chip)
    {
        if (notesContainer == null || notesContainer.childCount >= RequiredLineCount) return false;
        chip.RectTransform.SetParent(notesContainer, false);
        return true;
    }

    /// <summary>
    /// The real, gated confirm action: reads whichever chips are currently in the
    /// Notes zone, invokes the callback given to Show(), and closes the panel. Public
    /// so GreyboxFlowTest can exercise this exact logic directly, the same discipline
    /// every other tool's TryX method in this project follows.
    /// </summary>
    public TransitionResult Confirm()
    {
        if (!CanConfirm || confirmCallback == null)
        {
            return TransitionResult.Violation;
        }

        var selectedIds = new List<string>(notesContainer.childCount);
        foreach (var chip in notesContainer.GetComponentsInChildren<ReportLineChip>())
        {
            selectedIds.Add(chip.LineId);
        }
        if (selectedIds.Count != RequiredLineCount || new HashSet<string>(selectedIds).Count != RequiredLineCount)
            return TransitionResult.Violation;

        var callback = confirmCallback;
        var evidenceId = currentEvidenceId;

        var result = callback(evidenceId, selectedIds);
        if(result==TransitionResult.Applied) InteractionSfx.Play(InteractionSound.Writing,panelRoot.transform.position);
        Hide();
        return result;
    }

    /// <summary>
    /// Moves a specific line into the Notes zone programmatically - the exact
    /// end-state a real B-press produces, without driving the thumbstick/button
    /// InputActions. Exists so GreyboxFlowTest (which drives state directly rather
    /// than simulating VR input - see its own class comment) can still exercise the
    /// real Confirm() gating logic above without needing synthetic controller input.
    /// Public for that reason only; the real player-facing path is always
    /// RecorderReportUI's own thumbstick/B navigation.
    /// </summary>
    public bool SelectLineForTest(string lineId)
    {
        foreach (var chip in spawnedChips)
        {
            if (chip != null && chip.LineId == lineId && chip.transform.parent != notesContainer)
            {
                if (!TryAddToNotes(chip)) return false;
                UpdateConfirmIndicator();
                UpdateHint();
                return true;
            }
        }
        return false;
    }

    private void UpdateConfirmIndicator()
    {
        if (confirmIndicator != null)
        {
            confirmIndicator.color = CanConfirm ? confirmReadyColor : confirmNotReadyColor;
        }
    }

    private void UpdateHint()
    {
        if (hintLabel == null || notesContainer == null)
        {
            return;
        }

        hintLabel.text = notesContainer.childCount == RequiredLineCount
            ? "3 / 3 lines in Notes. A: confirm. To replace a line, move it out with B first."
            : $"Choose exactly 3 lines. Thumbstick: choose a line - B: move it - A: confirm  ({notesContainer.childCount} / 3 in Notes)";
    }

    // --- Chip pool --------------------------------------------------------------

    private void BuildChips(List<ReportLineOption> options)
    {
        ClearChips();

        if (chipTemplate == null || poolContainer == null)
        {
            return;
        }

        foreach (var option in options)
        {
            if (option == null || string.IsNullOrEmpty(option.id))
            {
                continue;
            }

            // Cross-item unlock gate: an option with unmet prerequisites is left out
            // of the candidate pool entirely (no chip spawned at all), not spawned-
            // but-disabled - the player should never see a locked option exists at
            // all, only that it appears once the prerequisite is actually met. See
            // SelectionPrerequisite/EvidenceStateManager.HasSelectedReportLine.
            if (!PrerequisitesSatisfied(option))
            {
                continue;
            }

            var clone = Instantiate(chipTemplate, poolContainer);
            clone.gameObject.SetActive(true);
            clone.Initialize(option);
            spawnedChips.Add(clone);
        }

        focusedColumn = Column.Pool;
        poolIndex = 0;
        notesIndex = 0;
        stickVerticalNeutral = true;
        stickHorizontalNeutral = true;
        RefreshFocusVisual();
    }

    /// <summary>
    /// Whether every prerequisite (if any) is currently met. Delegates to
    /// SelectionPrerequisite.AllSatisfied - the single shared implementation of this
    /// rule, now also used by HypothesisCheckpointManager and HypothesisCheckpointUI -
    /// this used to carry its own copy of the same switch statement; see
    /// SelectionPrerequisite's own comment for why that stopped being acceptable once
    /// a third call site needed it too. Also checks option.extraRequirement (an
    /// ADDITIONAL gate via the shared RequirementEvaluator - see EvidenceDefinition.
    /// ReportLineOption.extraRequirement's own comment), the same combined-check shape
    /// HypothesisCheckpointManager.CorrectOptionPrerequisitesSatisfied already uses.
    /// </summary>
    private static bool PrerequisitesSatisfied(ReportLineOption option)
    {
        return SelectionPrerequisite.AllSatisfied(option.prerequisites) && RequirementEvaluator.IsSatisfied(option.extraRequirement);
    }

    private void ClearChips()
    {
        foreach (var chip in spawnedChips)
        {
            if (chip != null)
            {
                // Detach BEFORE Destroy, not just Destroy - Destroy() is deferred to
                // end-of-frame, but reparenting is immediate. Without this, a chip
                // moved into notesContainer for one item stayed a live child of
                // notesContainer (not yet actually destroyed) if the NEXT item's
                // Show -> BuildChips -> Confirm cycle ran within the same frame -
                // exactly what happens with any back-to-back driving (RecorderTool
                // opening consecutive items with no frame yield, GreyboxFlowTest, or
                // equivalent test driving). Confirm()'s own
                // notesContainer.GetComponentsInChildren<ReportLineChip>() would then
                // pick up that stale prior-item chip alongside the current item's real
                // selections - confirmed as the exact cause of the cross-item
                // ReportLinesSelected accumulation seen live (EVD-018's event carrying
                // all 15 lines selected across the whole session, not just its own 3).
                chip.RectTransform.SetParent(null, false);
                Destroy(chip.gameObject);
            }
        }
        spawnedChips.Clear();
    }

    // --- Runtime fallback UI -------------------------------------------------
    // Only runs when no panelRoot was authored. Mirrors HypothesisCheckpointUI/
    // MasterSketchUI's "author it by hand, or let the script scaffold something
    // functional" arrangement.

    [EditableSceneBuilder]
    private void BuildRuntimeUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildRuntimeUI"))
        {
        var canvasGo = EditableSceneVisuals.Create("RecorderReportCanvas", typeof(RectTransform), typeof(Canvas), typeof(HudFollowCamera));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue - 2;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        // 900 (up from 700) tall, fixed - the extra 200 units is where the optional
        // recovered-content block lives (see ApplyContextLayout/the layout constants
        // above). An item with no loggingContext still uses this same taller canvas,
        // just with its content repositioned back up near the top edge (the
        // "collapsed" layout) so it looks the same as the original 700-tall panel,
        // with the unused ~200 units becoming harmless empty bezel below the hint
        // label rather than a gap in the middle of real content.
        canvasRect.sizeDelta = new Vector2(1000f, 900f);
        // 0.0007 (not the old 0.001) keeps every internal anchored position/size and
        // font size exactly proportional (nothing reflows or clips) while shrinking the
        // panel's physical footprint from ~1m x 0.7m to ~0.7m x 0.49m at the same 0.75m
        // viewing distance - down from ~67 degrees of horizontal FOV to ~50 degrees, so
        // it no longer spills into peripheral vision while staying just as readable
        // relative to itself.
        canvasGo.transform.localScale = Vector3.one * 0.0007f;

        // No GraphicRaycaster/collider/RayInteractable/PointableCanvas chain here -
        // this panel used to need the full Meta ISDK ray -> uGUI pointer chain for
        // real drag-and-drop chips, but that pipeline was confirmed dead for this
        // canvas (a hover-highlight test never fired even with the player's own
        // controller ray working fine elsewhere), and nothing else in this project
        // turned out to depend on it either - see the class doc comment. Selection
        // is entirely thumbstick/button InputActions now (see Update()), which never
        // touches uGUI's event system at all.
        var follow = canvasGo.GetComponent<HudFollowCamera>();
        follow.Distance = 0.75f;

        var background = CreateImage(canvasRect, "Background", new Color(0.06f, 0.06f, 0.08f, 0.96f));
        Stretch(background.rectTransform);

        // Recovered-content block - inactive by default (SetActive(false) below);
        // Show() activates and populates it only for an item whose
        // EvidenceDefinition.loggingContext is non-empty (see ApplyContextLayout for
        // the position swap that makes room for it). A real ScrollRect (this
        // project's first) so authored content longer than the visible viewport
        // scrolls instead of clipping or forcing the text to auto-shrink - see this
        // feature's own explicit "no clipping, no truncation, no aggressive
        // auto-shrinking, use scrolling if needed" requirement.
        var contextGo = EditableSceneVisuals.Create("ContextPanel", typeof(RectTransform));
        contextGo.transform.SetParent(canvasRect, false);
        var contextRect = contextGo.GetComponent<RectTransform>();
        contextRect.anchorMin = contextRect.anchorMax = new Vector2(0.5f, 0.5f);
        contextRect.pivot = new Vector2(0.5f, 0.5f);
        contextRect.sizeDelta = new Vector2(920f, 220f);
        contextRect.anchoredPosition = new Vector2(0f, 320f);

        var contextBorder = CreateImage(contextRect, "Border", new Color(1f, 1f, 1f, 0.4f));
        Stretch(contextBorder.rectTransform);
        var contextBg = CreateImage(contextRect, "Bg", new Color(0.1f, 0.1f, 0.13f, 1f));
        var contextBgRect = contextBg.rectTransform;
        contextBgRect.anchorMin = Vector2.zero;
        contextBgRect.anchorMax = Vector2.one;
        contextBgRect.offsetMin = new Vector2(3f, 3f);
        contextBgRect.offsetMax = new Vector2(-3f, -3f);

        var contextHeader = CreateLabel(contextRect, "Header", new Vector2(0f, 92f), new Vector2(880f, 32f), 22f);
        contextHeader.text = "Recovered Messages";
        contextHeaderLabel = contextHeader;
        contextHeader.fontStyle = FontStyles.Bold;
        contextHeader.alignment = TextAlignmentOptions.TopLeft;

        // Viewport + Content, the standard uGUI ScrollRect recipe: RectMask2D on the
        // viewport clips anything outside it, Content holds the actual text and grows
        // downward via ContentSizeFitter as text gets longer, and the ScrollRect wires
        // the two together with vertical-only dragging (thumbstick isn't used here -
        // see below for why that's fine for this feature's scope).
        var viewportGo = EditableSceneVisuals.Create("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(contextRect, false);
        var viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 1f);
        viewportRect.offsetMin = new Vector2(16f, 16f);
        viewportRect.offsetMax = new Vector2(-16f, -60f);

        var contentGo = EditableSceneVisuals.Create("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportRect, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        // A freshly-created RectTransform defaults sizeDelta to (100, 100). With these
        // stretch anchors that default WIDENS the box by 100 units beyond the
        // viewport's width (split around the pivot), pushing its left edge behind the
        // viewport's RectMask2D and clipping the first word(s) of every left-aligned
        // line. Zero it so the box matches the viewport's width exactly; height is
        // still driven by ContentSizeFitter below.
        contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);

        contextBodyLabel = EditableSceneVisuals.Component<TextMeshProUGUI>(contentGo);
        contextBodyLabel.fontSize = 22f;
        contextBodyLabel.color = new Color(0.92f, 0.92f, 0.88f, 1f);
        contextBodyLabel.alignment = TextAlignmentOptions.TopLeft;
        contextBodyLabel.enableWordWrapping = true;

        var contentFitter = EditableSceneVisuals.Component<ContentSizeFitter>(contentGo);
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // TMP's own auto-layout drives ContentSizeFitter here (no LayoutElement
        // needed) - horizontal stays fixed (anchored full-width of the viewport),
        // only height grows with text length.

        contextScrollRect = EditableSceneVisuals.Component<ScrollRect>(contextGo);
        contextScrollRect.content = contentRect;
        contextScrollRect.viewport = viewportRect;
        contextScrollRect.horizontal = false;
        contextScrollRect.vertical = true;
        contextScrollRect.movementType = ScrollRect.MovementType.Clamped;

        contextPanel = contextGo;
        contextPanel.SetActive(false);

        promptLabel = CreateLabel(canvasRect, "PromptLabel", PromptPosCollapsed, new Vector2(920f, 100f), 26f);

        // Two side-by-side columns: pool (left), notes (right) - the right thumbstick's
        // horizontal axis switches focus between them (left = Pool, right = Notes),
        // matching their physical layout.
        poolContainer = CreateLayoutColumn(canvasRect, "Pool", new Vector2(-240f, ColumnPosCollapsed.y), new Vector2(440f, 420f));
        var poolHeaderLabel = CreateLabel(canvasRect, "PoolHeader", new Vector2(-240f, HeaderPosCollapsed.y), new Vector2(440f, 36f), 22f);
        poolHeaderLabel.text = "Candidate lines";
        poolHeaderRect = poolHeaderLabel.rectTransform;

        var notesBorder = CreateImage(canvasRect, "NotesBorder", new Color(1f, 1f, 1f, 0.5f));
        notesBorder.rectTransform.anchorMin = notesBorder.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        notesBorder.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        notesBorder.rectTransform.sizeDelta = new Vector2(452f, 432f);
        notesBorder.rectTransform.anchoredPosition = new Vector2(240f, ColumnPosCollapsed.y);
        notesBorderRect = notesBorder.rectTransform;

        notesContainer = CreateLayoutColumn(notesBorder.rectTransform, "Notes", Vector2.zero, new Vector2(440f, 420f));
        notesContainer.GetComponent<Image>().color = new Color(0.1f, 0.14f, 0.1f, 1f);
        var notesHeaderLabel = CreateLabel(canvasRect, "NotesHeader", new Vector2(240f, HeaderPosCollapsed.y), new Vector2(440f, 36f), 22f);
        notesHeaderLabel.text = "Notes";
        notesHeaderRect = notesHeaderLabel.rectTransform;

        // Chip template: a small card, styled generically since this is greybox UI,
        // not final art. No CanvasGroup/raycastTarget concerns anymore - nothing
        // reads pointer events off it.
        var chipGo = EditableSceneVisuals.Create("ChipTemplate", typeof(RectTransform), typeof(Image));
        chipGo.transform.SetParent(poolContainer, false);
        var chipRect = chipGo.GetComponent<RectTransform>();
        chipRect.sizeDelta = new Vector2(0f, 64f);
        var chipLayout = EditableSceneVisuals.Component<LayoutElement>(chipGo);
        chipLayout.minHeight = 64f;
        chipLayout.preferredHeight = 64f;
        chipGo.GetComponent<Image>().color = new Color(0.85f, 0.83f, 0.7f, 1f);

        var chipLabelGo = EditableSceneVisuals.Create("Label", typeof(RectTransform));
        chipLabelGo.transform.SetParent(chipRect, false);
        var chipLabel = EditableSceneVisuals.Component<TextMeshProUGUI>(chipLabelGo);
        chipLabel.fontSize = 20f;
        chipLabel.color = Color.black;
        chipLabel.alignment = TextAlignmentOptions.Center;
        chipLabel.enableWordWrapping = true;
        Stretch(chipLabel.rectTransform);

        chipTemplate = EditableSceneVisuals.Component<ReportLineChip>(chipGo);
        chipTemplate.AssignRuntimeVisuals(chipLabel, chipGo.GetComponent<Image>());

        hintLabel = CreateLabel(canvasRect, "HintLabel", HintPosCollapsed, new Vector2(920f, 40f), 20f);
        hintLabel.color = new Color(0.75f, 0.75f, 0.8f, 1f);

        // Purely a visual readout of CanConfirm now (tints green/gray) - not a
        // clickable Button, since pressing A is what actually confirms.
        var indicatorGo = EditableSceneVisuals.Create("ConfirmIndicator", typeof(RectTransform), typeof(Image));
        indicatorGo.transform.SetParent(canvasRect, false);
        var indicatorRect = indicatorGo.GetComponent<RectTransform>();
        indicatorRect.anchorMin = indicatorRect.anchorMax = new Vector2(0.5f, 0f);
        indicatorRect.pivot = new Vector2(0.5f, 0f);
        indicatorRect.sizeDelta = new Vector2(240f, 60f);
        indicatorRect.anchoredPosition = new Vector2(0f, 30f);
        confirmIndicator = indicatorGo.GetComponent<Image>();
        confirmIndicator.color = confirmNotReadyColor;

        var indicatorLabel = CreateLabel(indicatorRect, "Label", Vector2.zero, new Vector2(240f, 60f), 26f);
        indicatorLabel.text = "CONFIRM (A)";
        indicatorLabel.color = Color.black;

        panelRoot = canvasGo;
        poolArea = ResponsivePanelLayout.Area.WrapFixed(poolContainer, "CandidateLines", 8);
        notesArea = ResponsivePanelLayout.Area.WrapFixed(notesContainer, "SelectedLines", 8);
        scrollPointer = new WorldPanelPointer(canvasRect, "ReportScroll", this);
        poolArea.RegisterControls(scrollPointer, "report-pool-"); notesArea.RegisterControls(scrollPointer, "report-notes-");
            }
    }

    private static RectTransform CreateLayoutColumn(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        go.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 1f);

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        return rect;
    }

    private static Image CreateImage(RectTransform parent, string name, Color color)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateLabel(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.color = Color.white;

        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
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
