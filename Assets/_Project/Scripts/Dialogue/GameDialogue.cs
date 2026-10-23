// Assets/_Project/Scripts/Dialogue/GameDialogue.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One resolved line on its way to a channel: final text plus everything the display needs.</summary>
public struct DialogueRequest
{
    public string id;               // catalog id, or null for text that isn't in the catalog (authored scenario data)
    public string text;             // final text, placeholders already filled
    public string speaker;
    public DialogueEntryType type;
    public DialogueKind priority;
    public float duration;
    public bool skippable;
    public DialogueReplaceBehavior behavior;
    public string historyCategory;  // Dialogue History label: "System", "Team", "Narrative"
    public string sourceId;         // keeps a multi-line sequence together (STCS trigger / narrative event id)
}

/// <summary>
/// The one entry point for showing player-facing dialogue and tooltips. Game code passes
/// a catalog id (plus values for {0}/{1} placeholders); everything else - text, channel,
/// duration, priority, skip, replace behavior - comes from ViCSIDialogueCatalog:
///
///   GameDialogue.Say("fingerprint-load-powder-first");
///   GameDialogue.Say("tent-placed", tentNumber);
///   string reason = GameDialogue.Text("evidence-step-photograph-first");   // for APIs that return a reason string
///   GameDialogue.ShowText(reason, DialogueKind.Error);                      // shows it with its catalog settings
///
/// Dialogue goes to the upper panel (STCSNotificationUI: priority queue, B skips, Dialogue
/// History); tooltips to the lower panel (NotificationUI: latest wins, no backlog). The two
/// channels never block each other.
/// </summary>
public static class GameDialogue
{
    // Text resolved from the catalog -> its entry, so a reason string returned by a gate
    // API (GetBlockReason etc.) still shows with that entry's channel/timing at the Notify site.
    private static readonly Dictionary<string, DialogueCatalogEntry> resolvedText = new(StringComparer.Ordinal);
    private const int ResolvedTextCap = 512;

    /// <summary>Raised for every request actually sent to a channel (tests, diagnostics).</summary>
    public static event Action<DialogueRequest> OnRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        resolvedText.Clear();
        OnRequested = null;
    }

    /// <summary>Shows a catalog entry on its channel. Missing id: warning (+ "[Missing dialogue: id]" in development builds), never an exception.</summary>
    public static void Say(string id, params object[] args)
    {
        var entry = ViCSIDialogueCatalog.Current.Find(id);
        if (entry == null)
        {
            ReportMissing(id);
            if (IsDevelopment) Route(MissingRequest(id));
            return;
        }
        Route(FromEntry(entry, Format(entry, args), "System", null));
    }

    /// <summary>
    /// A catalog line belonging to a team/narrative sequence (kept together in the queue,
    /// labelled in history). panel: the caller's own wired dialogue panel, used when given -
    /// these lines can fire during scene start-up, before the panel's Awake registers Instance.
    /// </summary>
    public static void SayLine(string id, string historyCategory, string sourceId, STCSNotificationUI panel = null)
    {
        var entry = ViCSIDialogueCatalog.Current.Find(id);
        if (entry == null)
        {
            ReportMissing(id);
            if (IsDevelopment) Route(MissingRequest(id));
            return;
        }
        Route(FromEntry(entry, entry.text, historyCategory, sourceId), panel);
    }

    /// <summary>
    /// Resolves an entry's text without showing it - for APIs that return a player-facing
    /// reason string. When that string is later shown via ShowText, it keeps this entry's
    /// channel and timing. Missing id: warning, and the dev placeholder (empty in release).
    /// </summary>
    public static string Text(string id, params object[] args)
    {
        var entry = ViCSIDialogueCatalog.Current.Find(id);
        if (entry == null)
        {
            ReportMissing(id);
            return IsDevelopment ? MissingText(id) : string.Empty;
        }
        string text = EvidenceDisplay.Text(Format(entry, args));
        if (resolvedText.Count >= ResolvedTextCap) resolvedText.Clear();
        resolvedText[text] = entry;
        return text;
    }

    /// <summary>
    /// Shows text already resolved through Text() with its catalog entry's settings. Text
    /// that never came from the catalog (scenario-authored data such as an evidence item's
    /// own block message) is shown as Dialogue with the given priority (a Confirmation as a
    /// Tooltip), so it is never silently dropped.
    /// </summary>
    public static void ShowText(string text, DialogueKind priorityIfNotInCatalog = DialogueKind.Guidance)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (resolvedText.TryGetValue(text, out var entry))
        {
            Route(FromEntry(entry, text, "System", null));
            return;
        }
        var catalog = ViCSIDialogueCatalog.Current;
        var type = priorityIfNotInCatalog == DialogueKind.Confirmation ? DialogueEntryType.Tooltip : DialogueEntryType.Dialogue;
        Route(new DialogueRequest
        {
            text = text,
            type = type,
            priority = priorityIfNotInCatalog,
            duration = catalog.DefaultDuration(type, priorityIfNotInCatalog),
            skippable = true,
            behavior = DialogueReplaceBehavior.IgnoreIfAlreadyShowing,
            historyCategory = "System",
        });
    }

    /// <summary>
    /// Joins several catalog-resolved pieces with spaces into one message (e.g. a
    /// measurement result plus "Evidence distances complete."). The joined text keeps
    /// the FIRST piece's catalog entry, so it shows on that entry's channel and timing.
    /// </summary>
    public static string Join(params string[] resolvedParts)
    {
        if (resolvedParts == null || resolvedParts.Length == 0) return string.Empty;
        var parts = new List<string>();
        foreach (var p in resolvedParts) if (!string.IsNullOrEmpty(p)) parts.Add(p);
        string joined = string.Join(" ", parts);
        if (parts.Count > 0 && resolvedText.TryGetValue(parts[0], out var entry))
        {
            if (resolvedText.Count >= ResolvedTextCap) resolvedText.Clear();
            resolvedText[joined] = entry;
        }
        return joined;
    }

    /// <summary>True when the id exists in the catalog (validation and tests).</summary>
    public static bool Exists(string id) => ViCSIDialogueCatalog.Current.Find(id) != null;

    private static DialogueRequest FromEntry(DialogueCatalogEntry entry, string text, string historyCategory, string sourceId) => new()
    {
        id = entry.id,
        text = text,
        speaker = entry.speaker,
        type = entry.type,
        priority = entry.priority,
        duration = ViCSIDialogueCatalog.Current.DurationFor(entry),
        skippable = entry.skippable,
        behavior = entry.replaceBehavior,
        historyCategory = historyCategory,
        sourceId = sourceId,
    };

    private static string Format(DialogueCatalogEntry entry, object[] args)
    {
        string text = entry.text ?? string.Empty;
        if (args == null || args.Length == 0) return text;
        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[DialogueCatalog] Entry '{entry.id}' has placeholders that don't match the {args.Length} value(s) supplied - showing the text unformatted.");
            return text;
        }
    }

    internal static void ShowResolved(DialogueRequest request) => Route(request);

    private static void Route(DialogueRequest request, STCSNotificationUI dialoguePanel = null)
    {
        if (SceneTestStart.IsPreparing) return;
        if (string.IsNullOrEmpty(request.text)) return;
        if (ReasoningCheckpointManager.Instance?.DeferDialogue(request) == true) return;
        request.text = EvidenceDisplay.Text(request.text);
        OnRequested?.Invoke(request);
        if (request.type == DialogueEntryType.Tooltip)
        {
            // Instance is registered in Awake; a tooltip posted during scene start-up (before
            // that Awake ran) finds the manager directly - its UI reference is serialized.
            var manager = NotificationManager.Instance != null ? NotificationManager.Instance : UnityEngine.Object.FindFirstObjectByType<NotificationManager>(FindObjectsInactive.Include);
            var tooltip = manager != null ? manager.UI : null;
            if (tooltip != null) tooltip.ShowTooltip(request.text, request.duration, request.behavior);
            else Debug.LogWarning($"[GameDialogue] No tooltip panel (NotificationUI) in this scene; dropped \"{request.id ?? request.text}\".");
            return;
        }
        // Same start-up ordering guard for the dialogue panel. Show() queues a line that
        // arrives before the panel is enabled and presents it on OnEnable.
        var dialogue = dialoguePanel != null ? dialoguePanel
            : STCSNotificationUI.Instance != null ? STCSNotificationUI.Instance
            : UnityEngine.Object.FindFirstObjectByType<STCSNotificationUI>(FindObjectsInactive.Include);
        if (dialogue != null) dialogue.Show(request);
        else Debug.LogWarning($"[GameDialogue] No dialogue panel (STCSNotificationUI) in this scene; dropped \"{request.id ?? request.text}\".");
    }

    private static bool IsDevelopment => Application.isEditor || Debug.isDebugBuild;
    private static string MissingText(string id) => $"[Missing dialogue: {id}]";

    private static void ReportMissing(string id) =>
        Debug.LogWarning($"[DialogueCatalog] Missing dialogue id '{id}' - add it to Resources/ViCSIDialogueCatalog.asset.");

    private static DialogueRequest MissingRequest(string id) => new()
    {
        id = id,
        text = MissingText(id),
        type = DialogueEntryType.Tooltip,
        priority = DialogueKind.Warning,
        duration = ViCSIDialogueCatalog.Current.tooltipImportantSeconds,
        behavior = DialogueReplaceBehavior.ReplaceCurrent,
        historyCategory = "System",
    };
}

/// <summary>
/// Right-controller B as "skip the current dialogue line". B already closes every panel
/// and performs in-world actions behind "[B] ..." prompts, so a press only counts as a
/// skip when neither is in play: no panel holds PlayerUIGate and no [B] ambient prompt is
/// requested. That way one press never both skips a line and closes a panel / picks up a
/// tent. Never required for normal play - lines also expire on their own.
/// </summary>
public class DialogueSkipInput
{
    public const string Binding = "<XRController>{RightHand}/secondaryButton";
    private readonly InputAction action;

    public DialogueSkipInput(string name)
    {
        action = new InputAction(name, InputActionType.Button, Binding);
    }

    public bool WasPressedThisFrame => action != null && action.WasPressedThisFrame();

    /// <summary>Whether a B press right now belongs to dialogue (not to a panel or an in-world [B] action).</summary>
    public static bool BelongsToDialogue()
    {
        if (PlayerUIGate.IsBlocked) return false;
        var ui = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
        return ui == null || !ui.HasAmbientPrompt;
    }

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

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (action == null || (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)) return;
        action.Disable();
        action.Enable();
    }
}
