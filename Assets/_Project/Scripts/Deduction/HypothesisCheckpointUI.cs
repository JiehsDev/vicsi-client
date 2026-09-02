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
/// Unlike every other panel in the project this one is deliberately NOT dismissible:
/// there is no close binding at all, and it ignores PlayerUIGate.IsBlocked when
/// opening. A checkpoint is a hard gate - the player answers it or it stays up.
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
    private Action<string, string> submitCallback;
    private int selectedIndex;
    private bool stickWasNeutral = true;
    private Stage stage;
    private string chosenHypothesis;

    private InputAction navStickAction;
    private InputAction submitAction;

    private void Awake()
    {
        // Same raw-XR-binding pattern as PhotographTool/PhotoAlbumUI/ToolWheelController.
        // Note the deliberate absence of a close/cancel binding.
        navStickAction = new InputAction("HypothesisCheckpoint_NavStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        submitAction = new InputAction("HypothesisCheckpoint_Submit", InputActionType.Button, "<XRController>{RightHand}/primaryButton");

        if (panelRoot == null)
        {
            BuildRuntimeUI();
        }

        if (optionTemplate != null)
        {
            optionTemplate.gameObject.SetActive(false);
        }

        panelRoot.SetActive(false);
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

        // Defensive: never leave locomotion suspended or the UI gate held if this
        // panel is torn down mid-checkpoint.
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    private void OnDestroy()
    {
        navStickAction?.Dispose();
        submitAction?.Dispose();
    }

    /// <summary>
    /// Puts a checkpoint on screen. onSubmit receives (selectedOption,
    /// reasoningOption) once the player commits - reasoningOption is null when the
    /// checkpoint doesn't require one. It is never invoked without a hypothesis
    /// selection, nor without a reasoning selection when one is required.
    /// </summary>
    public void Show(HypothesisCheckpoint checkpoint, Action<string, string> onSubmit)
    {
        if (checkpoint == null || checkpoint.options == null || checkpoint.options.Count == 0)
        {
            Debug.LogWarning($"[HypothesisCheckpointUI] Checkpoint '{checkpoint?.id}' has no options; nothing to show, skipping.", this);
            onSubmit?.Invoke(null, null);
            return;
        }

        current = checkpoint;
        submitCallback = onSubmit;
        stage = Stage.Hypothesis;
        chosenHypothesis = null;

        if (promptLabel != null)
        {
            promptLabel.text = checkpoint.promptText;
        }

        BuildOptionEntries(checkpoint.options);
        selectedIndex = 0;
        ApplyHighlight();

        stickWasNeutral = true;

        if (!IsShowing)
        {
            LocomotionSuspender.Suspend();
            PlayerUIGate.Enter(this);
            IsShowing = true;
        }

        panelRoot.SetActive(true);
        UpdateHint();
    }

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }

        if (submitAction.WasPressedThisFrame())
        {
            Commit();
            return;
        }

        Vector2 stick = navStickAction.ReadValue<Vector2>();
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

            chosenHypothesis = current.options[selectedIndex];

            if (RequiresReasoningPick())
            {
                EnterReasoningStage();
                return;
            }

            Submit(chosenHypothesis, null);
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= current.reasoningOptions.Count)
        {
            return;
        }

        var reasoning = current.reasoningOptions[selectedIndex];
        Submit(chosenHypothesis, reasoning != null ? reasoning.text : null);
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
            promptLabel.text = $"You chose: {chosenHypothesis}\n\nWhat most supports that conclusion?";
        }

        var reasoningTexts = new List<string>(current.reasoningOptions.Count);
        foreach (var option in current.reasoningOptions)
        {
            reasoningTexts.Add(option != null ? option.text : string.Empty);
        }

        BuildOptionEntries(reasoningTexts);
        selectedIndex = 0;
        ApplyHighlight();
        stickWasNeutral = true;
        UpdateHint();
    }

    private void Submit(string selectedOption, string reasoningOption)
    {
        var callback = submitCallback;

        Hide();

        callback?.Invoke(selectedOption, reasoningOption);
    }

    private void UpdateHint()
    {
        if (hintLabel == null)
        {
            return;
        }

        if (stage == Stage.Hypothesis && RequiresReasoningPick())
        {
            hintLabel.text = "Thumbstick to choose - press A to continue. You'll be asked why next.";
            return;
        }

        hintLabel.text = stage == Stage.Reasoning
            ? "Thumbstick to choose - press A to submit. You must answer to continue."
            : "Thumbstick to choose - press A to commit. You must answer to continue.";
    }

    private void Hide()
    {
        panelRoot.SetActive(false);

        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }

        current = null;
        submitCallback = null;
        chosenHypothesis = null;
        stage = Stage.Hypothesis;
    }

    private void ReleaseHolds()
    {
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);
    }

    private void BuildOptionEntries(List<string> options)
    {
        foreach (var entry in optionEntries)
        {
            if (entry != null)
            {
                Destroy(entry.gameObject);
            }
        }
        optionEntries.Clear();
        optionLabels.Clear();

        if (optionTemplate == null || optionsContainer == null)
        {
            return;
        }

        foreach (var option in options)
        {
            var clone = Instantiate(optionTemplate, optionsContainer);
            clone.gameObject.SetActive(true);

            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = option;
            }

            optionEntries.Add(clone);
            optionLabels.Add(label);
        }
    }

    // --- Runtime fallback UI -------------------------------------------------
    // Only runs when no panelRoot was authored. Mirrors ToolWheelController's
    // "author it by hand, or let the script scaffold something functional"
    // arrangement so this system can be dropped into a scene and tested without
    // hand-building a canvas first.

    private void BuildRuntimeUI()
    {
        var canvasGo = new GameObject("HypothesisCheckpointCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudFollowCamera));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Above every other canvas in the scene - a hard gate shouldn't end up
        // behind the notification HUD or an open album.
        canvas.sortingOrder = PanelSortingOrder;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 620f);
        canvasRect.localScale = Vector3.one * 0.001f;

        var follow = canvasGo.GetComponent<HudFollowCamera>();
        follow.Distance = 0.75f;

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

        var optionsGo = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup));
        optionsGo.transform.SetParent(canvasRect, false);
        optionsContainer = optionsGo.GetComponent<RectTransform>();
        optionsContainer.anchorMin = new Vector2(0f, 0f);
        optionsContainer.anchorMax = new Vector2(1f, 1f);
        optionsContainer.offsetMin = new Vector2(60f, 150f);
        optionsContainer.offsetMax = new Vector2(-60f, -200f);

        var layout = optionsGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        optionTemplate = CreateChildImage(optionsContainer, "OptionTemplate", unselectedTint);
        var templateRect = optionTemplate.rectTransform;
        templateRect.sizeDelta = new Vector2(0f, 72f);
        var templateLayout = optionTemplate.gameObject.AddComponent<LayoutElement>();
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

    private static Image CreateChildImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateChildText(RectTransform parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
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
