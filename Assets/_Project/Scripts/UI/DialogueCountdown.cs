// Assets/_Project/Scripts/UI/DialogueCountdown.cs
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Countdown for one timed message, shared by NotificationUI and STCSNotificationUI so
/// both pause, protect and dismiss the same way.
///
/// Pause rule: the countdown stops while a panel that opened AFTER the message appeared
/// is holding PlayerUIGate (Evidence/Procedure panel, Dialogue History, tool wheel,
/// viewfinder...), and resumes when it closes - the player never loses guidance by
/// reading another panel. A message that appears while a panel is already open (e.g. a
/// locked-row reason clicked inside the Evidence panel) counts down normally; otherwise
/// every refusal triggered inside a panel would freeze and stack until the panel closed.
/// Game pause (timeScale 0) also stops it, since it runs on scaled time.
/// </summary>
public class DialogueCountdown
{
    public float Duration { get; private set; }
    public float Elapsed { get; private set; }

    /// <summary>Real time on screen, including paused time - what dismiss protection is measured against.</summary>
    public float VisibleSeconds { get; private set; }

    public bool IsPaused { get; private set; }

    private int gateLocksAtStart;

    public bool IsDone => Elapsed >= Duration;
    public float Remaining01 => Duration <= 0f ? 0f : Mathf.Clamp01(1f - Elapsed / Duration);
    public bool PastProtection => VisibleSeconds >= DialogueTimingSettings.Current.dismissProtectionSeconds;

    public void Start(float duration)
    {
        Duration = Mathf.Max(0f, duration);
        Elapsed = 0f;
        VisibleSeconds = 0f;
        IsPaused = false;
        gateLocksAtStart = PlayerUIGate.LockCount;
    }

    public void Tick(float deltaTime)
    {
        VisibleSeconds += deltaTime;

        int locks = PlayerUIGate.LockCount;
        // A panel that was already open when this started and has since closed lowers the
        // baseline, so a panel opened later still pauses it.
        if (locks < gateLocksAtStart)
        {
            gateLocksAtStart = locks;
        }

        IsPaused = locks > gateLocksAtStart;
        if (!IsPaused)
        {
            Elapsed = Mathf.Min(Duration, Elapsed + deltaTime);
        }
    }
}

/// <summary>
/// Fits short and long messages at a readable font size. The authored width is the
/// maximum line length; height follows wrapped text, including explicit line breaks.
/// Keeps the edge facing the other dialogue channel fixed as the panel changes size.
/// </summary>
public sealed class DialoguePanelLayout
{
    private readonly RectTransform panel;
    private readonly TMP_Text label;
    private readonly float authoredHeight;
    private readonly float maximumWidth;
    private readonly bool keepTopEdge;
    private const float HorizontalPadding = 24f;
    private const float MinimumFontSize = 24f;

    public DialoguePanelLayout(RectTransform panel, TMP_Text label, float authoredHeight, bool keepTopEdge)
    {
        this.panel = panel;
        this.label = label;
        this.authoredHeight = authoredHeight;
        this.keepTopEdge = keepTopEdge;
        maximumWidth = panel != null ? panel.rect.width : 0f;
    }

    public void Fit()
    {
        if (panel == null || label == null || authoredHeight <= 0f || maximumWidth <= 0f) return;

        label.enableAutoSizing = false;
        label.fontSize = Mathf.Max(MinimumFontSize, label.fontSize);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.margin = Vector4.zero;

        float verticalPadding = Mathf.Max(32f, DialogueTimingSettings.Current.panelVerticalPadding);
        float minimumWidth = Mathf.Min(maximumWidth, 240f);
        float naturalWidth = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x;
        float width = Mathf.Clamp(Mathf.Ceil(naturalWidth + HorizontalPadding * 2f), minimumWidth, maximumWidth);
        float contentWidth = Mathf.Max(1f, width - HorizontalPadding * 2f);
        float textHeight = label.GetPreferredValues(label.text, contentWidth, Mathf.Infinity).y;
        // Do not cap the height: the old cap left the last lines outside the background.
        float height = Mathf.Ceil(Mathf.Max(label.fontSize * 1.3f, textHeight) + verticalPadding);

        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        float pivotY = keepTopEdge ? 1f - authoredHeight / (2f * height) : authoredHeight / (2f * height);
        panel.pivot = new Vector2(panel.pivot.x, pivotY);

        // The authored upper background has a separate Y scale; the lower one has
        // extra size offsets. Match the measured panel so both padding and the shared
        // obstacle/companion solver describe the actual visible rectangle.
        for (int i = 0; i < panel.childCount; i++)
        {
            var background = panel.GetChild(i) as RectTransform;
            if (background == null || background.GetComponent<UnityEngine.UI.Image>() == null) continue;
            background.localScale = Vector3.one;
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
        }

        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(HorizontalPadding, verticalPadding * .5f);
        rect.offsetMax = new Vector2(-HorizontalPadding, -verticalPadding * .5f);
        label.ForceMeshUpdate();
    }
}

/// <summary>Manual dismiss button for timed dialogue (binding from DialogueTimingSettings). Loose InputAction like every panel here.</summary>
public class DialogueDismissInput
{
    private readonly InputAction action;

    public DialogueDismissInput(string name)
    {
        string binding = DialogueTimingSettings.Current.dismissBinding;
        if (!string.IsNullOrEmpty(binding))
        {
            action = new InputAction(name, InputActionType.Button, binding);
        }
    }

    public bool WasPressedThisFrame => action != null && action.WasPressedThisFrame();

    public void Enable()
    {
        action?.Enable();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    public void Disable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        action?.Disable();
    }

    public void Dispose() => action?.Dispose();

    // Same Quest device-reconnect fix the other panels' loose InputActions apply.
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (action == null || (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected))
        {
            return;
        }
        action.Disable();
        action.Enable();
    }
}
