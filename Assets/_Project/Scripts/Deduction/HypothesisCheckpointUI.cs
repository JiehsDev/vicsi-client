// Assets/_Project/Scripts/Deduction/HypothesisCheckpointUI.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The forced-commitment panel for one HypothesisCheckpoint, collected in up to two
/// stages: first a theory, then - when the checkpoint requires it - a justification
/// for that theory. Both are multiple choice, presented through the same list-select
/// UI; there is no free-text entry anywhere in this panel by design (see
/// ReasoningOption for why reasoning is keyed rather than typed).
///
/// Follows the UI conventions already established in this project rather than
/// inventing a new one: a World Space canvas pinned in front of the HMD via
/// HudFollowCamera (Screen Space - Overlay doesn't composite correctly in stereo -
/// see HudFollowCamera), options cloned from a template entry and highlighted with
/// selected/unselected tints (PhotoAlbumUI's thumbnailTemplate pattern), right
/// thumbstick to move the highlight and the right controller's A button to commit
/// (PhotoAlbumUI/ToolWheelController's raw-InputAction pattern), and
/// LocomotionSuspender + PlayerUIGate held for as long as the panel is up. Every
/// visual reference below is optional: assign them to use a hand-authored panel, or
/// leave them blank and a functional one is generated at runtime - the same
/// authored-or-generated arrangement ToolWheelController uses for its wheel.
///
/// Submission closes the panel; there is no post-submission back binding. The
/// checkpoint managers coordinate opening with existing UI and physical activity.
/// </summary>
public class HypothesisCheckpointUI : MonoBehaviour
{
    private const float NavDeadzone = 0.5f;
    private const int PanelSortingOrder = short.MaxValue - 1;

    [Header("Authored UI (optional - leave blank to auto-generate at runtime)")]
    [Tooltip("The GameObject shown/hidden as the panel. If left blank, a World Space canvas is built at runtime and used instead.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Displays the checkpoint's promptText.")]
    [SerializeField] private TMP_Text promptLabel;

    [Tooltip("Parent that one entry per option is cloned into - give it a VerticalLayoutGroup to control spacing.")]
    [SerializeField] private RectTransform optionsContainer;

    [Tooltip("An inactive entry used as the template for each option - style this one and every option matches. Needs a TMP_Text somewhere in its children.")]
    [SerializeField] private Image optionTemplate;

    [Tooltip("Small line under the options telling the player what to press and which step they're on.")]
    [SerializeField] private TMP_Text hintLabel;

    [Header("VR comfort")]
    [Tooltip("World-space width of the checkpoint panel in canvas units. At the default scale, 780 is about 0.78 m.")]
    [SerializeField, Min(500f)] private float questionPanelWidth = 780f;
    [Tooltip("Height used for ordinary checkpoints in canvas units.")]
    [SerializeField, Min(450f)] private float standardPanelHeight = 640f;
    [Tooltip("Height used when a contextual reasoning prompt needs additional room.")]
    [SerializeField, Min(600f)] private float contextualPanelHeight = 900f;
    [Tooltip("World-space canvas scale. 0.00085 with a 780-unit width is about 0.66 m wide.")]
    [SerializeField, Range(0.0005f, 0.002f)] private float questionPanelScale = 0.00085f;
    [SerializeField, Min(0.8f)] private float questionViewingDistance = 1.2f;
    [SerializeField, Min(0.55f)] private float questionMinimumDistance = 0.82f;
    [SerializeField] private float questionVerticalOffset = -0.12f;
    [SerializeField, Min(0.02f)] private float questionWallClearance = 0.18f;
    [SerializeField] private LayerMask environmentCollisionMask = 1 << 0;
    [SerializeField, Range(0f, 0.4f)] private float questionFollowSmoothing = 0.18f;
    [SerializeField, Range(0f, 20f)] private float questionRotationSmoothing = 8f;
    [SerializeField, Range(4f, 24f)] private float answerSpacing = 12f;

    [Header("Highlight")]
    [SerializeField] private Color unselectedTint = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    [SerializeField] private Color selectedTint = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color unselectedTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color selectedTextColor = new Color(0.05f, 0.05f, 0.06f, 1f);

    /// <summary>True while a checkpoint is on screen awaiting an answer.</summary>
    public bool IsShowing { get; private set; }

    private readonly List<Image> optionEntries = new();
    private readonly List<TMP_Text> optionLabels = new();

    /// <summary>Which of the two picks the panel is currently collecting.</summary>
    private enum Stage
    {
        Hypothesis,
        Reasoning
    }

    private HypothesisCheckpoint current;
    private ReasoningCheckpointDefinition snapshotDefinition;
    private bool selectionConfirmed;
    private int openedFrame;
    private bool runtimeLayout;
    private ResponsivePanelLayout responsiveLayout;
    private WorldPanelPointer pointer;
    private int visibleSelection = -1;
    public ResponsivePanelLayout ResponsiveLayout => responsiveLayout;
    public WorldPanelPointer Pointer => pointer;
    public string DisplayedPrompt => promptLabel != null ? promptLabel.text : string.Empty;
    public string DisplayedHint => hintLabel != null ? hintLabel.text : string.Empty;
    public IReadOnlyList<TMP_Text> DisplayedOptions => optionLabels.AsReadOnly();

    public void ShowReasoning(ReasoningCheckpointDefinition definition, Action<string> onSubmit)
    {
        if (ReasoningCheckpointManager.Instance?.IsAnswered(definition.id) == true) return;
        ShowCore(SnapshotProxy(definition), (id, text, reasonId, reasonText) => onSubmit?.Invoke(id), definition);
    }

    private static HypothesisCheckpoint SnapshotProxy(ReasoningCheckpointDefinition definition, HypothesisCheckpoint original = null)
    {
        var proxy = new HypothesisCheckpoint
        {
            id = original != null ? original.id : definition.id,
            promptText = definition.prompt,
            // Contextual responses already contain the student's interpretation and
            // justification. Do not ask the legacy justification question a second time.
            requiresReasoning = false,
            reasoningOptions = original != null ? original.reasoningOptions : new List<ReasoningOption>(),
            trigger = original != null ? original.trigger : new HypothesisTrigger()
        };
        foreach (var response in definition.responses) proxy.options.Add(new HypothesisOption { id = response.id, text = response.text });
        return proxy;
    }

    // Explicit select followed by a separate confirmation; also used by focused diagnostics.
    public void SelectResponse(int index)
    {
        if (!IsShowing || stage != Stage.Hypothesis || snapshotDefinition == null) return;
        selectedIndex = Mathf.Clamp(index, 0, current.options.Count - 1);
        selectionConfirmed = true;
        ApplyHighlight(); UpdateHint();
    }
    public void ConfirmSelection() => Commit();

    public void ShowRelationshipReview(ReasoningCheckpointDefinition definition, Action<string> onSubmit)
        => ShowCore(SnapshotProxy(definition), (id, text, reasonId, reasonText) => onSubmit?.Invoke(id), definition, "EVIDENCE RELATIONSHIPS REVIEW");

    /// <summary>
    /// The reasoning options actually offered this stage - current.reasoningOptions
    /// filtered by SelectionPrerequisite.AllSatisfied, rebuilt fresh each time
    /// EnterReasoningStage runs. An option whose prerequisites aren't met is left out
    /// entirely (not shown-but-disabled), same "never shown to exist at all until
    /// earned" convention RecorderReportUI already uses for gated report lines. Once
    /// built, this - not current.reasoningOptions - is what selectedIndex indexes
    /// into everywhere (Commit, AnswerForTest): the rendered list and the list
    /// selectedIndex resolves against must always be the same list, or a filtered-out
    /// option shifts every index after it and the wrong option gets submitted.
    /// </summary>
    private readonly List<ReasoningOption> visibleReasoningOptions = new();

    /// <summary>
    /// (selectedOptionId, selectedOptionText, reasoningOptionId, reasoningOptionText) -
    /// ids are what HypothesisCheckpointManager logs (stable across a text reword),
    /// text travels alongside for anyone reading the raw session log by eye. The
    /// reasoning pair is null/null when the checkpoint doesn't require one.
    /// </summary>
    private Action<string, string, string, string> submitCallback;
    private int selectedIndex;
    private bool stickWasNeutral = true;
    private Stage stage;
    private string chosenHypothesisId;
    private string chosenHypothesisText;

    private InputAction navStickAction;
    private InputAction submitAction;

    private void Awake()
    {
        // Same raw-XR-binding pattern as PhotographTool/PhotoAlbumUI/ToolWheelController.
        // Note the deliberate absence of a close/cancel binding.
        navStickAction = new InputAction("HypothesisCheckpoint_NavStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        submitAction = PlayerUIGate.CreateConfirmAction(this, "HypothesisCheckpoint_Submit", () => IsShowing, 500);

        if (panelRoot == null || GetComponent<EditableSceneVisuals>() != null)
        {
            BuildRuntimeUI();
        }

        if (optionTemplate != null)
        {
            optionTemplate.gameObject.SetActive(false);
        }

        BuildInteractionVisuals();
        panelRoot.SetActive(false);
    }

    [EditableSceneBuilder]
    private void BuildInteractionVisuals()
    {
        using (EditableSceneVisuals.Begin(this,"BuildInteractionVisuals"))
        {
        responsiveLayout = ResponsivePanelLayout.Create((RectTransform)panelRoot.transform, promptLabel, optionsContainer,
            hintLabel, standardPanelHeight, contextualPanelHeight, 52, answerSpacing);
        pointer = new WorldPanelPointer((RectTransform)panelRoot.transform, "ReasoningOptions", this);
        responsiveLayout.RegisterControls(pointer, "reasoning-scroll-");

        }
    }

    private void OnEnable()
    {
        navStickAction.Enable();
        submitAction.Enable();
    }

    private void OnDisable()
    {
        navStickAction.Disable();
        submitAction.Disable();
        pointer?.Disable();

        // Defensive: never leave locomotion suspended or the UI gate held if this
        // panel is torn down mid-checkpoint.
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        navStickAction?.Dispose();
        submitAction?.Dispose();
        pointer?.Dispose();
    }

    /// <summary>
    /// Puts a checkpoint on screen. onSubmit receives (selectedOptionId,
    /// selectedOptionText, reasoningOptionId, reasoningOptionText) once the player
    /// commits - the reasoning pair is null/null when the checkpoint doesn't require
    /// one. It is never invoked without a hypothesis selection, nor without a
    /// reasoning selection when one is required.
    /// </summary>
    public void Show(HypothesisCheckpoint checkpoint, Action<string, string, string, string> onSubmit)
    {
        var definition = ReasoningCheckpointManager.Instance?.IntegratedDefinition(checkpoint?.id);
        if (definition != null && ReasoningCheckpointManager.Instance.IsAnswered(definition.id)) return;
        ShowCore(definition != null ? SnapshotProxy(definition, checkpoint) : checkpoint, onSubmit, definition);
    }

    private void ShowCore(HypothesisCheckpoint checkpoint, Action<string, string, string, string> onSubmit, ReasoningCheckpointDefinition definition, string heading = "Reasoning Checkpoint")
    {
        if (IsShowing) return;
        snapshotDefinition = definition;
        selectionConfirmed = false;
        openedFrame = Time.frameCount;
        ConfigureLayout(definition != null);
        if (checkpoint == null || checkpoint.options == null || checkpoint.options.Count == 0)
        {
            Debug.LogWarning($"[HypothesisCheckpointUI] Checkpoint '{checkpoint?.id}' has no options; nothing to show, skipping.", this);
            onSubmit?.Invoke(null, null, null, null);
            return;
        }

        current = checkpoint;
        submitCallback = onSubmit;
        stage = Stage.Hypothesis;
        chosenHypothesisId = null;
        chosenHypothesisText = null;

        if (promptLabel != null)
        {
            string context = definition != null && !string.IsNullOrEmpty(definition.contextCatalogId)
                ? GameDialogue.Text(definition.contextCatalogId) + "\n\n" : string.Empty;
            string title = heading == "EVIDENCE RELATIONSHIPS REVIEW" ? heading
                : "Reasoning Checkpoint\n" + QuestionDisplay.Label(definition?.id ?? checkpoint.id);
            promptLabel.text = title + "\n\n" + EvidenceDisplay.Text(context + checkpoint.promptText);
        }

        BuildOptionEntries(checkpoint.options);
        selectedIndex = 0;
        ApplyHighlight();

        stickWasNeutral = true;

        if (!IsShowing)
        {
            LocomotionSuspender.Suspend();
            PlayerUIGate.Enter(this);
            ToolInputLock.Enter(this);
            IsShowing = true;
        }

        HudFollowCamera.ConfigureShared(panelRoot, this, 500);
        panelRoot.SetActive(true);
        pointer.Enable();
        UpdateHint();

        // The panel used to just exist between one frame and the next, with nothing
        // to mark the transition. In a headset that reads as a glitch rather than as
        // the scenario asking you a question.
        InteractionFeedback.Confirm(panelRoot.transform);
    }

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }

        pointer.Tick();
        if (!IsShowing) return;

        if (Time.frameCount > openedFrame && PlayerUIGate.TryConsumeConfirm(this, submitAction))
        {
            Commit();
            return;
        }

        Vector2 stick = navStickAction.ReadValue<Vector2>();
        if (Mathf.Abs(stick.x) > .5f) responsiveLayout.ScrollOptions(-stick.x * 240 * Time.unscaledDeltaTime);
        if (Mathf.Abs(stick.y) < NavDeadzone)
        {
            stickWasNeutral = true;
            return;
        }

        // One flick = one step, same as PhotoAlbumUI - the highlight shouldn't race
        // down the list while the stick is held over.
        if (!stickWasNeutral)
        {
            return;
        }
        stickWasNeutral = false;

        MoveSelection(stick.y > 0f ? -1 : 1);
    }

    private void MoveSelection(int delta)
    {
        if (optionEntries.Count == 0)
        {
            return;
        }

        selectedIndex = Mathf.Clamp(selectedIndex + delta, 0, optionEntries.Count - 1);
        selectionConfirmed = false;
        ApplyHighlight();
        UpdateHint();
    }

    private void ApplyHighlight()
    {
        for (int i = 0; i < optionEntries.Count; i++)
        {
            bool isSelected = i == selectedIndex;

            if (optionEntries[i] != null)
            {
                optionEntries[i].color = isSelected ? selectedTint : unselectedTint;
            }

            if (i < optionLabels.Count && optionLabels[i] != null)
            {
                optionLabels[i].color = isSelected ? selectedTextColor : unselectedTextColor;
            }
        }
        if (visibleSelection != selectedIndex && selectedIndex >= 0 && selectedIndex < optionEntries.Count)
            responsiveLayout?.EnsureVisible(optionEntries[selectedIndex].rectTransform);
        visibleSelection = selectedIndex;
    }

    /// <summary>
    /// Picks hypothesisIndex (and reasoningIndex, if this checkpoint requires a
    /// reasoning step) and commits both - the exact end-state a real
    /// thumbstick-navigate-then-A-press sequence produces, without driving the
    /// InputActions. Exists for the same reason RecorderReportUI.SelectLineForTest
    /// does: GreyboxFlowTest drives state directly rather than simulating VR input
    /// (see its own class comment), but still needs to answer a REAL checkpoint that
    /// fires mid-flow - a CountThreshold trigger (e.g. HYP-01's "3 items at Found")
    /// can genuinely pop this panel open while the test is walking an unrelated
    /// item's lifecycle, and it holds PlayerUIGate until answered, which silently
    /// blocks RecorderReportUI.Show() for any item logged while it's up. No-ops if
    /// nothing is currently showing. Public for that reason only; the real
    /// player-facing path is always this panel's own thumbstick/A navigation.
    /// </summary>
    public void AnswerForTest(int hypothesisIndex = 0, int reasoningIndex = 0)
    {
        if (!IsShowing || current == null)
        {
            return;
        }

        selectedIndex = Mathf.Clamp(hypothesisIndex, 0, current.options.Count - 1);
        if (snapshotDefinition != null) SelectResponse(selectedIndex);
        Commit();

        if (IsShowing && stage == Stage.Reasoning)
        {
            // Clamp against visibleReasoningOptions, not current.reasoningOptions -
            // same reasoning as Commit()'s reasoning branch. GreyboxFlowTest's default
            // reasoningIndex of 0 therefore picks whichever option is FIRST AMONG THE
            // VISIBLE ones for the current evidence state, not necessarily
            // current.reasoningOptions[0] - which is correct: index 0 is exactly what
            // a real thumbstick-at-rest player would have highlighted too.
            selectedIndex = Mathf.Clamp(reasoningIndex, 0, visibleReasoningOptions.Count - 1);
            Commit();
        }
    }

    /// <summary>
    /// Advances the panel: commits the hypothesis pick and moves to the reasoning
    /// pick when the checkpoint requires one, otherwise submits. Submission is
    /// therefore structurally impossible before both picks exist - there is no path
    /// that reaches the callback from the Hypothesis stage while reasoning is
    /// required, rather than a flag checked at the end.
    /// </summary>
    private void Commit()
    {
        if (current == null)
        {
            return;
        }

        if (stage == Stage.Hypothesis)
        {
            if (selectedIndex < 0 || selectedIndex >= current.options.Count)
            {
                return;
            }

            if (snapshotDefinition != null && !selectionConfirmed)
            {
                SelectResponse(selectedIndex);
                return;
            }
            var chosen = current.options[selectedIndex];
            if (snapshotDefinition != null && current.id != snapshotDefinition.id
                && ReasoningCheckpointManager.Instance?.RecordResponse(snapshotDefinition.id, chosen.id) != true) return;
            chosenHypothesisId = chosen?.id;
            chosenHypothesisText = chosen?.text;

            if (RequiresReasoningPick())
            {
                EnterReasoningStage();
                return;
            }

            Submit(chosenHypothesisId, chosenHypothesisText, null, null);
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= visibleReasoningOptions.Count)
        {
            return;
        }

        var reasoning = visibleReasoningOptions[selectedIndex];
        Submit(chosenHypothesisId, chosenHypothesisText, reasoning?.id, reasoning?.text);
    }

    /// <summary>
    /// A reasoning pick is required only when the checkpoint asks for one AND has
    /// options to offer. Authoring that sets requiresReasoning with an empty list is
    /// caught by HypothesisCheckpointSet.OnValidate; this guard means that mistake
    /// degrades to "no reasoning step" rather than trapping the player at a panel
    /// with nothing to select.
    /// </summary>
    private bool RequiresReasoningPick()
    {
        return current.requiresReasoning
            && current.reasoningOptions != null
            && current.reasoningOptions.Count > 0;
    }

    private void EnterReasoningStage()
    {
        stage = Stage.Reasoning;

        if (promptLabel != null)
        {
            promptLabel.text = EvidenceDisplay.Text($"You chose: {chosenHypothesisText}\n\nWhat most supports that conclusion?");
        }

        visibleReasoningOptions.Clear();
        if (current.reasoningOptions != null)
        {
            foreach (var option in current.reasoningOptions)
            {
                if (option != null && SelectionPrerequisite.AllSatisfied(option.prerequisites))
                {
                    visibleReasoningOptions.Add(option);
                }
            }
        }

        BuildOptionEntries(visibleReasoningOptions);
        selectedIndex = 0;
        ApplyHighlight();
        stickWasNeutral = true;
        UpdateHint();
    }

    private void Submit(string selectedOptionId, string selectedOptionText, string reasoningOptionId, string reasoningOptionText)
    {
        var callback = submitCallback;

        Hide();

        callback?.Invoke(selectedOptionId, selectedOptionText, reasoningOptionId, reasoningOptionText);
    }

    private void UpdateHint()
    {
        // Refresh in the assignment frame; LateUpdate also catches external text changes.
        if (hintLabel == null)
        {
            return;
        }

        if (snapshotDefinition != null && stage == Stage.Hypothesis)
        {
            hintLabel.text = selectionConfirmed
                ? "A: Confirm Answer. Your response will be recorded for this session."
                : "Thumbstick: highlight a response. A: Select. Then A: Confirm Answer.";
            responsiveLayout.Refresh();
            return;
        }
        if (stage == Stage.Hypothesis && RequiresReasoningPick())
        {
            hintLabel.text = "Thumbstick to choose - press A to continue. You'll be asked why next.";
            responsiveLayout.Refresh();
            return;
        }

        hintLabel.text = stage == Stage.Reasoning
            ? "Thumbstick to choose - press A to Confirm Answer. You must answer to continue."
            : "Thumbstick to choose - press A to Confirm Answer. You must answer to continue.";
        responsiveLayout.Refresh();
    }

    private void Hide()
    {
        pointer.Disable();
        panelRoot.SetActive(false);

        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }

        current = null;
        snapshotDefinition = null;
        selectionConfirmed = false;
        submitCallback = null;
        chosenHypothesisId = null;
        chosenHypothesisText = null;
        stage = Stage.Hypothesis;
        visibleReasoningOptions.Clear();
    }

    private void ReleaseHolds()
    {
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);
        ToolInputLock.Exit(this);
    }

    /// <summary>Hypothesis-stage overload - builds entries from the theory list.</summary>
    private void BuildOptionEntries(List<HypothesisOption> options)
    {
        BuildOptionEntriesCore(options.ConvertAll(o => o?.text ?? string.Empty));
    }

    /// <summary>Reasoning-stage overload - builds entries from the justification list.</summary>
    private void BuildOptionEntries(List<ReasoningOption> options)
    {
        BuildOptionEntriesCore(options.ConvertAll(o => o?.text ?? string.Empty));
    }

    /// <summary>
    /// Shared entry-building logic for both option kinds - visual/selection state only
    /// (labels, highlight entries). Commit reads the id for whichever index is
    /// selected straight back out of current.options/current.reasoningOptions rather
    /// than this class keeping its own parallel copy - one source of truth for id/text
    /// pairs, this just needs the display text.
    /// </summary>
    private void BuildOptionEntriesCore(List<string> texts)
    {
        pointer.Unregister("reasoning-option-");
        visibleSelection = -1;
        foreach (var entry in optionEntries)
        {
            if (entry != null)
            {
                entry.gameObject.SetActive(false); Destroy(entry.gameObject);
            }
        }
        optionEntries.Clear();
        optionLabels.Clear();

        if (optionTemplate == null || optionsContainer == null)
        {
            return;
        }

        foreach (var text in texts)
        {
            var clone = Instantiate(optionTemplate, optionsContainer);
            clone.gameObject.SetActive(true);

            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = EvidenceDisplay.Text(text);
            }

            optionEntries.Add(clone);
            optionLabels.Add(label);
            responsiveLayout.AddOption(clone.rectTransform, label, 78);
            int index = optionEntries.Count - 1;
            pointer.Register("reasoning-option-" + index, clone.rectTransform, Vector2.zero, () => {
                if (Time.frameCount <= openedFrame) return;
                bool same = selectedIndex == index;
                selectedIndex = index;
                if (snapshotDefinition != null && stage == Stage.Hypothesis && (!same || !selectionConfirmed))
                { SelectResponse(index); openedFrame = Time.frameCount; }
                else { ApplyHighlight(); Commit(); openedFrame = Time.frameCount; }
            });
        }
        responsiveLayout.Refresh();
    }

    // --- Runtime fallback UI -------------------------------------------------
    // Only runs when no panelRoot was authored. Mirrors ToolWheelController's
    // "author it by hand, or let the script scaffold something functional"
    // arrangement so this system can be dropped into a scene and tested without
    // hand-building a canvas first.

    [EditableSceneBuilder]
    private void BuildRuntimeUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildRuntimeUI"))
        {
        runtimeLayout = true;
        var canvasGo = EditableSceneVisuals.Create("HypothesisCheckpointCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudFollowCamera));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Above every other canvas in the scene - a hard gate shouldn't end up
        // behind the notification HUD or an open album.
        canvas.sortingOrder = PanelSortingOrder;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(questionPanelWidth, standardPanelHeight);
        canvasRect.localScale = Vector3.one * questionPanelScale;

        var follow = canvasGo.GetComponent<HudFollowCamera>();
        ConfigureFollowComfort(follow);

        var background = CreateChildImage(canvasRect, "Background", new Color(0.06f, 0.06f, 0.08f, 0.96f));
        Stretch(background.rectTransform);

        promptLabel = CreateChildText(canvasRect, "PromptLabel", 34f, TextAlignmentOptions.Top);
        var promptRect = promptLabel.rectTransform;
        promptRect.anchorMin = new Vector2(0f, 1f);
        promptRect.anchorMax = new Vector2(1f, 1f);
        promptRect.pivot = new Vector2(0.5f, 1f);
        promptRect.offsetMin = new Vector2(40f, 0f);
        promptRect.offsetMax = new Vector2(-40f, -30f);
        promptRect.sizeDelta = new Vector2(promptRect.sizeDelta.x, 160f);

        var optionsGo = EditableSceneVisuals.Create("Options", typeof(RectTransform), typeof(VerticalLayoutGroup));
        optionsGo.transform.SetParent(canvasRect, false);
        optionsContainer = optionsGo.GetComponent<RectTransform>();
        optionsContainer.anchorMin = new Vector2(0f, 0f);
        optionsContainer.anchorMax = new Vector2(1f, 1f);
        optionsContainer.offsetMin = new Vector2(60f, 150f);
        optionsContainer.offsetMax = new Vector2(-60f, -200f);

        var layout = optionsGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = answerSpacing;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        optionTemplate = CreateChildImage(optionsContainer, "OptionTemplate", unselectedTint);
        var templateRect = optionTemplate.rectTransform;
        templateRect.sizeDelta = new Vector2(0f, 72f);
        var templateLayout = EditableSceneVisuals.Component<LayoutElement>(optionTemplate.gameObject);
        templateLayout.minHeight = 72f;
        templateLayout.preferredHeight = 72f;

        var optionLabel = CreateChildText(templateRect, "Label", 30f, TextAlignmentOptions.Center);
        Stretch(optionLabel.rectTransform);
        optionLabel.color = unselectedTextColor;
        optionTemplate.gameObject.SetActive(false);

        hintLabel = CreateChildText(canvasRect, "HintLabel", 22f, TextAlignmentOptions.Center);
        var hintRect = hintLabel.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.offsetMin = new Vector2(40f, 30f);
        hintRect.offsetMax = new Vector2(-40f, 0f);
        hintRect.sizeDelta = new Vector2(hintRect.sizeDelta.x, 90f);
        hintLabel.color = new Color(0.7f, 0.7f, 0.75f, 1f);

        panelRoot = canvasGo;
            }
    }

    private void ConfigureLayout(bool contextual)
    {
        if (panelRoot == null) return;
        var panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(questionPanelWidth, contextual ? contextualPanelHeight : standardPanelHeight);
            panelRect.localScale = Vector3.one * questionPanelScale;
        }

        HudFollowCamera follow = panelRoot.GetComponent<HudFollowCamera>();
        if (follow == null) follow = panelRoot.GetComponentInChildren<HudFollowCamera>(true);
        ConfigureFollowComfort(follow);

        if (!runtimeLayout) return;
        promptLabel.fontSize = contextual ? 28f : 34f;
        float height = 78f;
        optionTemplate.rectTransform.sizeDelta = new Vector2(0f, height);
        var layoutElement = optionTemplate.GetComponent<LayoutElement>();
        layoutElement.minHeight = height; layoutElement.preferredHeight = height;
        var answerLayout = optionsContainer.GetComponent<VerticalLayoutGroup>();
        if (answerLayout != null) answerLayout.spacing = answerSpacing;
        var label = optionTemplate.GetComponentInChildren<TMP_Text>(true);
        label.fontSize = contextual ? 26f : 30f;
        label.margin = Vector4.zero;
    }

    private void ConfigureFollowComfort(HudFollowCamera follow)
    {
        if (follow == null) return;
        follow.Distance = questionViewingDistance;
        follow.MinimumDistance = questionMinimumDistance;
        follow.LocalOffset = new Vector3(0f, questionVerticalOffset, 0f);
        follow.WallClearance = questionWallClearance;
        follow.EnvironmentMask = environmentCollisionMask;
        follow.ObstacleAwarePlacement = true;
        follow.SmoothTime = questionFollowSmoothing;
        follow.RotationSmoothing = questionRotationSmoothing;
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
