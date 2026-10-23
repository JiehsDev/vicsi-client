// Assets/_Project/Scripts/STCS/STCSNotificationUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The upper world-space panel: the DIALOGUE channel - professor/teammate lines, narrative
/// beats, and system guidance, warnings, errors and major confirmations, all resolved from
/// ViCSIDialogueCatalog through GameDialogue. (Short "do this now" tooltips use the lower
/// panel, NotificationUI, so the two never block each other.)
///
/// QUEUE: a line that arrives while another is up waits instead of replacing it mid-read.
/// Waiting lines play highest priority first (Error > Warning > Guidance > Confirmation),
/// first-come within a priority. Lines from the same source (a narrative event / STCS
/// trigger sequence) stay together, so two conversations never interleave line by line.
/// A Confirmation on screen gives way early (after the skip protection) when something
/// more important is waiting; nothing else is cut short except by ReplaceCurrent or a skip.
///
/// TIMING: each line is held for its catalog duration (or the catalog default for its
/// priority); the countdown pauses while a menu/panel holds the UI gate.
///
/// SKIP: B (see DialogueSkipInput - only when no panel or [B] prompt owns the button) or
/// the right-thumbstick dismiss ends a skippable line at once and the next queued line
/// appears immediately. A skipped line stays in Dialogue History: it was recorded, once,
/// the moment it became visible.
/// </summary>
public class STCSNotificationUI : MonoBehaviour
{
    public static STCSNotificationUI Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text label;
    [SerializeField] private DialogueCountdownBar countdownBar;

    private readonly List<DialogueRequest> pending = new();
    private DialogueRequest current;
    private readonly DialogueCountdown countdown = new();
    private DialogueDismissInput dismissInput;
    private DialogueSkipInput skipInput;
    private Coroutine driveRoutine;
    private bool endRequested;
    private bool endInstantly;
    private float presentedAt;
    private bool awoken;         // lines posted before Awake (scene start-up order) wait in the queue   // unscaled time the current line appeared (skip protection counts from here, fade-in included)
    private RectTransform panel;
    private float authoredPanelHeight;
    private DialoguePanelLayout dialogueLayout;

    /// <summary>True from the moment a line is displayed until it has faded out.</summary>
    public bool IsShowing { get; private set; }

    /// <summary>The live countdown of the line on screen (meaningful only while IsShowing).</summary>
    public DialogueCountdown Countdown => countdown;

    /// <summary>Lines waiting behind the one on screen.</summary>
    public int PendingCount => pending.Count;

    /// <summary>True while any line posted with this sourceId is on screen or still queued.</summary>
    public bool HasLinesFrom(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return false;
        if (IsShowing && current.sourceId == sourceId) return true;
        foreach (var entry in pending)
        {
            if (entry.sourceId == sourceId) return true;
        }
        return false;
    }

    /// <summary>Spoken text on screen, without a speaker prefix, or null.</summary>
    public string CurrentText => IsShowing ? current.text : null;

    /// <summary>Priority of the line on screen, or null.</summary>
    public DialogueKind? CurrentKind => IsShowing ? current.priority : null;

    private void Awake()
    {
        Instance = this;
        HudFollowCamera.ConfigureShared(gameObject, this, PlayerUIGate.DialoguePriority, true,
            () => IsShowing && (canvasGroup == null || canvasGroup.alpha > .001f));
        awoken = true;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        panel = transform as RectTransform;
        authoredPanelHeight = panel != null ? panel.sizeDelta.y : 0f;
        dialogueLayout = new DialoguePanelLayout(panel, label, authoredPanelHeight, keepTopEdge: false);
        dismissInput = new DialogueDismissInput("DialogueLine_Dismiss");
        skipInput = new DialogueSkipInput("DialogueLine_SkipB");
    }

    private void OnEnable()
    {
        dismissInput.Enable();
        skipInput.Enable();
        // Lines posted while this was inactive waited for it.
        if (pending.Count > 0 && driveRoutine == null)
        {
            Present(TakeNext());
            driveRoutine = StartCoroutine(Drive());
        }
    }

    private void OnDisable()
    {
        dismissInput.Disable();
        skipInput.Disable();
        HideImmediately();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        dismissInput?.Dispose();
        skipInput?.Dispose();
    }

    private void Update()
    {
        if (skipInput.WasPressedThisFrame && DialogueSkipInput.BelongsToDialogue())
        {
            Skip();
        }
        else if (dismissInput.WasPressedThisFrame)
        {
            Skip();
        }
    }

    /// <summary>Posts a catalog-resolved line (normally via GameDialogue).</summary>
    public void Show(DialogueRequest request)
    {
        request.text = DialoguePresentation.SpokenText(request.text, ref request.speaker);
        if (string.IsNullOrEmpty(request.text)) return;
        if (request.duration <= 0f) request.duration = ViCSIDialogueCatalog.Current.DefaultDuration(DialogueEntryType.Dialogue, request.priority);

        switch (request.behavior)
        {
            case DialogueReplaceBehavior.IgnoreIfAlreadyShowing:
                if (IsShowing && current.text == request.text) return;
                foreach (var p in pending) if (p.text == request.text) return;
                break;
            case DialogueReplaceBehavior.ReplaceSameChannel:
                pending.Clear();
                break;
        }

        bool replaceCurrent = request.behavior == DialogueReplaceBehavior.ReplaceCurrent;
        if (awoken && !IsShowing && driveRoutine == null && isActiveAndEnabled)
        {
            Present(request);
            driveRoutine = StartCoroutine(Drive());
            return;
        }

        if (replaceCurrent)
        {
            pending.Insert(0, request);
            if (IsShowing) { endRequested = true; endInstantly = true; }
            return;
        }
        Enqueue(request);
    }

    /// <summary>
    /// Back-compatible entry for un-migrated callers: a Guidance line with the catalog
    /// default timing. Catalog content should come through GameDialogue.SayLine instead.
    /// </summary>
    public void ShowLine(string line, string speaker = null, string category = null, string sourceId = null)
    {
        Show(new DialogueRequest
        {
            text = line,
            speaker = speaker,
            type = DialogueEntryType.Dialogue,
            priority = DialogueKind.Guidance,
            duration = ViCSIDialogueCatalog.Current.DefaultDuration(DialogueEntryType.Dialogue, DialogueKind.Guidance),
            skippable = true,
            behavior = DialogueReplaceBehavior.Queue,
            historyCategory = category,
            sourceId = sourceId,
        });
    }

    /// <summary>
    /// Ends the line on screen now (if skippable and past the short skip protection); the
    /// next queued line appears immediately. False when nothing skippable is showing.
    /// </summary>
    public bool Skip()
    {
        if (!IsShowing || !current.skippable) return false;
        if (Time.unscaledTime - presentedAt < ViCSIDialogueCatalog.Current.skipProtectionSeconds) return false;
        endRequested = true;
        endInstantly = true;
        return true;
    }

    /// <summary>Old name for Skip (right-thumbstick dismiss and older tests).</summary>
    public bool Dismiss() => Skip();

    /// <summary>
    /// Clears the panel and drops any queued lines at once, no fade. For resets (and
    /// test harnesses that need a clean panel); queued lines never shown are not recorded.
    /// </summary>
    public void HideImmediately()
    {
        if (driveRoutine != null)
        {
            StopCoroutine(driveRoutine);
            driveRoutine = null;
        }
        pending.Clear();
        IsShowing = false;
        endRequested = false;
        endInstantly = false;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (countdownBar != null) countdownBar.SetVisible(false);
    }

    // Same-source lines go straight after the last queued line of their sequence (or to
    // the front when their sequence is the one on screen); anything else to the back.
    private void Enqueue(DialogueRequest entry)
    {
        if (!string.IsNullOrEmpty(entry.sourceId))
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].sourceId == entry.sourceId)
                {
                    pending.Insert(i + 1, entry);
                    return;
                }
            }
            if (IsShowing && entry.sourceId == current.sourceId)
            {
                pending.Insert(0, entry);
                return;
            }
        }
        pending.Add(entry);
    }

    // Highest priority first; the earliest position wins ties (keeps sequences in order).
    private DialogueRequest TakeNext()
    {
        int best = 0;
        for (int i = 1; i < pending.Count; i++)
        {
            if (pending[i].priority > pending[best].priority) best = i;
        }
        var next = pending[best];
        pending.RemoveAt(best);
        return next;
    }

    private bool HasPendingAbove(DialogueKind kind)
    {
        foreach (var p in pending) if (p.priority > kind) return true;
        return false;
    }

    // Puts a line on screen: label, history record (the moment it becomes visible, once), countdown.
    private void Present(DialogueRequest entry)
    {
        current = entry;
        string text = entry.text;
        if (label != null)
        {
            label.text = EvidenceDisplay.Text(text);
            // Grows upward only, away from the tooltip panel below it.
            dialogueLayout.Fit();
        }
        DialogueHistoryManager.Record(entry.text, entry.speaker, string.IsNullOrEmpty(entry.historyCategory) ? "System" : entry.historyCategory, entry.sourceId ?? entry.id);
        IsShowing = true;
        endRequested = false;
        endInstantly = false;
        presentedAt = Time.unscaledTime;
        countdown.Start(entry.duration);
        if (countdownBar != null)
        {
            countdownBar.SetProgress(1f);
            countdownBar.SetVisible(true);
        }
    }

    private IEnumerator Drive()
    {
        var settings = DialogueTimingSettings.Current;
        float protection = ViCSIDialogueCatalog.Current.skipProtectionSeconds;
        while (true)
        {
            yield return Fade(CurrentAlpha(), 1f, settings.fadeSeconds);
            while (!countdown.IsDone && !endRequested)
            {
                // Training context yields visually to immediate action guidance and
                // menus, then resumes with its reading time intact. Other dialogue
                // retains its existing timing and priority behaviour.
                if (current.historyCategory == "Training")
                {
                    // Let immediate blocked-action feedback through. The lesson
                    // remains in history, without replaying it for every item.
                    if (Time.unscaledTime - presentedAt >= protection && HasPendingAbove(current.priority)) break;
                    var tooltip = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
                    bool actionHint = tooltip != null && (!string.IsNullOrEmpty(tooltip.CurrentTooltip)
                        || (tooltip.HasAmbientPrompt && current.id != "closeout-start" && current.id != "closeout-survey-guidance"
                            && !ProceduralTrainingGuidance.IsReadyVendorGuidance(current.id)));
                    if (PlayerUIGate.IsBlocked || actionHint)
                    {
                        if (canvasGroup != null) canvasGroup.alpha = 0f;
                        yield return null;
                        continue;
                    }
                    if (canvasGroup != null) canvasGroup.alpha = 1f;
                }
                countdown.Tick(Time.deltaTime);
                if (countdownBar != null) countdownBar.SetProgress(countdown.Remaining01);
                if (current.priority == DialogueKind.Confirmation && countdown.VisibleSeconds >= protection && HasPendingAbove(current.priority))
                {
                    break;
                }
                yield return null;
            }

            if (endInstantly)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0f;
            }
            else
            {
                yield return Fade(CurrentAlpha(), 0f, settings.fadeSeconds);
            }
            IsShowing = false;
            endRequested = false;
            endInstantly = false;
            if (countdownBar != null) countdownBar.SetVisible(false);

            if (pending.Count == 0)
            {
                driveRoutine = null;
                yield break;
            }
            Present(TakeNext());
        }
    }

    private float CurrentAlpha() => canvasGroup != null ? canvasGroup.alpha : 0f;

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (canvasGroup == null) yield break;
        if (seconds <= 0f)
        {
            canvasGroup.alpha = to;
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
    }
}
