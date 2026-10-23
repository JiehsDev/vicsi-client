// Assets/_Project/Scripts/UI/ReportLineChip.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One report-line card in RecorderReportUI's panel, shown in either the candidate
/// pool or the Notes list. Selection is driven entirely by RecorderReportUI's own
/// thumbstick+button navigation (same raw-InputAction pattern as
/// HypothesisCheckpointUI/ToolWheelController), not by uGUI pointer events - this
/// project's Meta ISDK ray interactors were confirmed to never reach world-space
/// canvases built by these runtime panels, so a chip only ever needs to render
/// whichever focus state RecorderReportUI tells it to via SetFocused, not detect
/// hover/drag itself.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ReportLineChip : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image background;

    [Tooltip("Background colour while this card is not the currently focused one.")]
    [SerializeField] private Color normalColor = new(0.85f, 0.83f, 0.7f, 1f);

    [Tooltip("Background colour while RecorderReportUI's cursor is on this card - the affordance that tells the player it's the one thumbstick/B will act on.")]
    [SerializeField] private Color focusedColor = new(0.98f, 0.93f, 0.55f, 1f);

    public string LineId { get; private set; }
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        if (background == null)
        {
            background = GetComponent<Image>();
        }
        SetFocused(false);
    }

    public void Initialize(ReportLineOption option)
    {
        LineId = option.id;
        if (label != null)
        {
            label.text = option.text;
            ResponsiveTextOption.Bind(GetComponent<RectTransform>(), label, 64);
        }
    }

    /// <summary>Wires the visual references this component needs when it's built at runtime rather than hand-authored - see RecorderReportUI.BuildRuntimeUI. No-op effect on an already-authored chip beyond overwriting the same references.</summary>
    public void AssignRuntimeVisuals(TMP_Text runtimeLabel, Image runtimeBackground)
    {
        label = runtimeLabel;
        background = runtimeBackground;
    }

    /// <summary>Called by RecorderReportUI whenever its navigation cursor moves onto or off of this card.</summary>
    public void SetFocused(bool focused)
    {
        if (background != null)
        {
            background.color = focused ? focusedColor : normalColor;
        }
    }
}
