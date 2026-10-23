// Assets/_Project/Scripts/UI/NotificationUI.cs
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The lower world-space panel: the TOOLTIP channel. Two kinds of message share its label:
///
///  - Timed tooltips (ShowTooltip, fed by GameDialogue for catalog entries of type
///    Tooltip): short "do this now" instructions. LATEST WINS - a new tooltip replaces the
///    one on screen immediately and nothing is ever queued, so an instruction for a step
///    the player has already finished can never appear later ("Measure the west wall" is
///    simply gone once "Measure the north wall" is posted). Re-posting the tooltip that is
///    already up restarts its timer instead of stacking a copy.
///
///  - The ambient prompt (ShowPrompt / HidePrompt): a standing affordance such as
///    "[B] Pick Up Evidence Tent", true for as long as some context holds. A timed tooltip
///    takes the label over it; the prompt comes back by itself once the tooltip expires,
///    provided its context still holds.
///
/// Professor/teammate dialogue never comes here - that is the upper panel
/// (STCSNotificationUI), so the two channels never block or overwrite each other.
/// Tooltips are not written to Dialogue History.
/// </summary>
public class NotificationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text label;
    [SerializeField] private DialogueCountdownBar countdownBar;

    private string tooltipText;            // the timed tooltip on screen (null when none)
    private float tooltipRemaining;
    private float tooltipDuration;
    private string ambientPromptMessage;   // what the world wants the prompt to say (kept while a tooltip covers it)
    private string displayed;              // what the label currently shows
    private Coroutine fadeRoutine;
    private RectTransform panel;
    private float authoredPanelHeight;
    private DialoguePanelLayout dialogueLayout;
    private bool awoken;   // requests that arrive before Awake (scene start-up order) are shown by Awake
    private HudFollowCamera follow;
    private Vector3 authoredOffset;
    private Vector3? promptOffset;

    /// <summary>True while an ambient prompt is requested (whether or not a tooltip currently covers it).</summary>
    public bool HasAmbientPrompt => ambientPromptMessage != null;
    public string CurrentAmbientPrompt => ambientPromptMessage;

    /// <summary>The timed tooltip on screen, or null.</summary>
    public string CurrentTooltip => tooltipText;

    /// <summary>Seconds the current tooltip still has (0 when none).</summary>
    public float TooltipRemaining => tooltipText != null ? tooltipRemaining : 0f;

    /// <summary>Tooltips never queue - always 0. Kept for diagnostics that report panel load.</summary>
    public int PendingCount => 0;
    public bool IsPlacementDeferred => follow != null && follow.SharedLayout
        && (PlayerUIGate.IsBlocked || follow.IsLayoutSuppressed);

    private void Awake()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        panel = transform as RectTransform;
        authoredPanelHeight = panel != null ? panel.sizeDelta.y : 0f;
        dialogueLayout = new DialoguePanelLayout(panel, label, authoredPanelHeight, keepTopEdge: true);
        follow = GetComponent<HudFollowCamera>();
        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && GetComponentInParent<LobbyCanvasPlacement>() == null)
        {
            // Reuse the authored label/countdown/canvas; no new canvas or input owner.
            follow = HudFollowCamera.ConfigureTemporaryFeedback(gameObject, this,
                () => canvasGroup == null || canvasGroup.alpha > .001f);
            if (panel != null) panel.localScale = Vector3.one * .0011f;
            if (canvasGroup != null) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
        }
        if (follow != null) authoredOffset = follow.LocalOffset;
        awoken = true;
        Refresh();
    }

    private void OnDisable()
    {
        tooltipText = null;
        ambientPromptMessage = null;
        promptOffset = null;
        if (follow != null) follow.LocalOffset = authoredOffset;
        displayed = null;
        fadeRoutine = null;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (countdownBar != null) countdownBar.SetVisible(false);
    }

    /// <summary>
    /// Shows a timed tooltip, replacing any tooltip on screen at once (latest wins, no
    /// backlog). IgnoreIfAlreadyShowing leaves an identical tooltip untouched; any other
    /// behavior restarts its timer.
    /// </summary>
    public void ShowTooltip(string text, float durationSeconds, DialogueReplaceBehavior behavior = DialogueReplaceBehavior.ReplaceCurrent)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (text == tooltipText && behavior == DialogueReplaceBehavior.IgnoreIfAlreadyShowing) return;
        tooltipText = text;
        tooltipDuration = Mathf.Max(0.1f, durationSeconds);
        tooltipRemaining = tooltipDuration;
        Refresh();
    }

    /// <summary>Back-compatible shim: a timed tooltip with the catalog's default length for this priority.</summary>
    public void Show(string message, DialogueKind kind = DialogueKind.Guidance) =>
        ShowTooltip(message, ViCSIDialogueCatalog.Current.DefaultDuration(DialogueEntryType.Tooltip, kind));

    /// <summary>Clears the current tooltip now (the ambient prompt, if any, comes back).</summary>
    public void ClearTooltip()
    {
        tooltipText = null;
        Refresh();
    }

    /// <summary>Requests the ambient prompt; shown whenever no timed tooltip is up.</summary>
    public void ShowPrompt(string message) => ShowPrompt(message, null);

    public void ShowPrompt(string message, Vector3? cameraOffset)
    {
        if (string.IsNullOrEmpty(message)) return;
        ambientPromptMessage = message;
        promptOffset = cameraOffset;
        Refresh();
    }

    /// <summary>Withdraws the ambient prompt. Never affects a timed tooltip.</summary>
    public void HidePrompt()
    {
        ambientPromptMessage = null;
        promptOffset = null;
        Refresh();
    }

    private void Update()
    {
        if (tooltipText == null) return;
        // Preserve readable time while a modal or solid geometry withholds this latest-wins message.
        if (IsPlacementDeferred || (canvasGroup != null && canvasGroup.alpha <= .001f)) return;
        tooltipRemaining -= Time.deltaTime;
        if (countdownBar != null) countdownBar.SetProgress(Mathf.Clamp01(tooltipRemaining / tooltipDuration));
        if (tooltipRemaining <= 0f)
        {
            tooltipText = null;
            Refresh();
        }
    }

    // Single owner of the label: tooltip > ambient prompt > nothing.
    private void Refresh()
    {
        if (!awoken) return;
        string wanted = tooltipText ?? ambientPromptMessage;
        bool timed = tooltipText != null;
        // Context-specific offsets remain desired poses, validated by the shared solver.
        if (follow != null) follow.LocalOffset = !timed && ambientPromptMessage != null && promptOffset.HasValue
            ? promptOffset.Value : authoredOffset;
        if (follow != null && follow.SharedLayout)
            follow.PlacementPriority = timed ? PlayerUIGate.ProceduralFeedbackPriority : PlayerUIGate.AmbientPriority;
        if (countdownBar != null)
        {
            countdownBar.SetVisible(timed);
            if (timed) countdownBar.SetProgress(Mathf.Clamp01(tooltipRemaining / tooltipDuration));
        }
        if (wanted == null)
        {
            displayed = null;
            FadeTo(0f);
            return;
        }
        if (wanted != displayed)
        {
            displayed = wanted;
            if (label != null)
            {
                label.text = EvidenceDisplay.Text(wanted);
                // Grows downward only, away from the dialogue panel above it.
                dialogueLayout.Fit();
            }
        }
        FadeTo(1f);
    }

    private void FadeTo(float target)
    {
        if (canvasGroup == null) return;
        if (!isActiveAndEnabled) { canvasGroup.alpha = target; return; }
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(target, DialogueTimingSettings.Current.fadeSeconds));
    }

    private IEnumerator Fade(float to, float seconds)
    {
        float from = canvasGroup.alpha;
        if (seconds <= 0f || Mathf.Approximately(from, to))
        {
            canvasGroup.alpha = to;
            fadeRoutine = null;
            yield break;
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        canvasGroup.alpha = to;
        fadeRoutine = null;
    }
}
