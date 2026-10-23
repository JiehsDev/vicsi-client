// Assets/_Project/Scripts/Dialogue/ViCSIDialogueCatalog.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where an entry sits in the investigation - declared in the order a player meets the
/// content, so the catalog reads top to bottom like a play-through.
/// </summary>
public enum DialogueCategory
{
    Startup = 0,
    ScenePhotography = 1,
    Body = 2,
    Knife = 3,
    BloodSpatter = 4,
    Bottle = 5,
    MarkersChalk = 6,
    Measurements = 7,
    BloodSampling = 8,
    FingerprintProcessing = 9,
    Recorder = 10,
    CollectionBaggingSealing = 11,
    CrimeSceneSketch = 12,
    DeductionBoard = 13,
    Hypotheses = 14,
    Conclusion = 15,
    Ending = 16,
    GenericWarnings = 17,
    SystemErrors = 18,
}

/// <summary>Which on-screen channel an entry uses.</summary>
public enum DialogueEntryType
{
    /// <summary>Professor / teammate / instructional line: upper panel, queued by priority, B skips it, kept in Dialogue History.</summary>
    Dialogue = 0,
    /// <summary>Short "do this now" instruction: lower panel, latest wins (replaces whatever tooltip is up), never queued, not in history.</summary>
    Tooltip = 1,
}

/// <summary>What happens when an entry is posted while its channel is busy.</summary>
public enum DialogueReplaceBehavior
{
    /// <summary>Dialogue: wait in the priority queue. (Tooltips never queue - treated as ReplaceCurrent.)</summary>
    Queue = 0,
    /// <summary>Drop everything waiting on the same channel, then show after the current line.</summary>
    ReplaceSameChannel = 1,
    /// <summary>Cut the current line short and show this one next.</summary>
    ReplaceCurrent = 2,
    /// <summary>Do nothing if the identical text is already showing or waiting.</summary>
    IgnoreIfAlreadyShowing = 3,
}

/// <summary>One player-facing line. Text may contain {0}, {1}… placeholders filled by the game (evidence names, numbers).</summary>
[Serializable]
public class DialogueCatalogEntry
{
    [Tooltip("Stable id the game code asks for. Don't rename without updating the code that uses it.")]
    public string id;
    public DialogueCategory category;
    public DialogueEntryType type;
    [Tooltip("Optional speaker attribution for Dialogue History. The dialogue panel shows only the spoken text.")]
    public string speaker;
    [TextArea(2, 6)]
    public string text;
    [Tooltip("Seconds on screen. 0 = the catalog default for this type and priority.")]
    public float duration;
    [Tooltip("Dialogue order when several wait: Error > Warning > Guidance > Confirmation. For tooltips, Warning/Error use the longer 'important' default.")]
    public DialogueKind priority = DialogueKind.Guidance;
    [Tooltip("Dialogue only: the B button may dismiss it early.")]
    public bool skippable = true;
    public DialogueReplaceBehavior replaceBehavior = DialogueReplaceBehavior.Queue;
    [TextArea(1, 4), Tooltip("Editor notes only - when it appears, what the {0} placeholders are. Never shown in game.")]
    public string notes;
}

/// <summary>
/// THE single authoritative source of player-facing professor dialogue and gameplay
/// tooltips. Game code asks for an entry by id (GameDialogue.Say / GameDialogue.Text);
/// text, timing, priority and channel all come from here, so editing this asset in the
/// Inspector changes what the next Play run shows - no code change, no copy in a
/// manager. Loaded from Resources/ViCSIDialogueCatalog.
///
/// Entries are kept in play-through order (see DialogueCategory) for manual editing.
/// A missing id never crashes: it logs a warning naming the id and, in development
/// builds only, shows "[Missing dialogue: id]".
/// </summary>
[CreateAssetMenu(menuName = "ViCSI/Dialogue Catalog", fileName = "ViCSIDialogueCatalog")]
public class ViCSIDialogueCatalog : ScriptableObject
{
    public const string ResourcePath = "ViCSIDialogueCatalog";

    [Header("Default dialogue durations (seconds) - used when an entry's duration is 0")]
    public float dialogueConfirmationSeconds = 2.75f;
    public float dialogueGuidanceSeconds = 4f;
    public float dialogueWarningSeconds = 5f;
    public float dialogueErrorSeconds = 5f;

    [Header("Default tooltip durations (seconds)")]
    public float tooltipSeconds = 2f;
    [Tooltip("Tooltips whose priority is Warning or Error.")]
    public float tooltipImportantSeconds = 2.75f;

    [Header("Skipping")]
    [Tooltip("A dialogue line can't be skipped with B until it has been visible this long (prevents an accidental double press).")]
    public float skipProtectionSeconds = 0.25f;

    [Header("Entries - in play-through order")]
    public List<DialogueCatalogEntry> entries = new();

    private Dictionary<string, DialogueCatalogEntry> byId;
    private static ViCSIDialogueCatalog current;
    private static ViCSIDialogueCatalog testOverride;

    /// <summary>The project's catalog (Resources/ViCSIDialogueCatalog), or the test override while one is set.</summary>
    public static ViCSIDialogueCatalog Current
    {
        get
        {
            if (testOverride != null) return testOverride;
            if (current == null)
            {
                current = Resources.Load<ViCSIDialogueCatalog>(ResourcePath);
                if (current == null)
                {
                    Debug.LogWarning("[DialogueCatalog] Resources/ViCSIDialogueCatalog.asset not found - every dialogue id will be reported missing.");
                    current = CreateInstance<ViCSIDialogueCatalog>();
                    current.hideFlags = HideFlags.DontSave;
                }
            }
            return current;
        }
    }

    /// <summary>Test seam: use this catalog instead of the project asset (null restores it).</summary>
    public static void SetOverrideForTesting(ViCSIDialogueCatalog catalog) => testOverride = catalog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        testOverride = null;
        if (current != null) current.byId = null;   // re-read Inspector edits each Play session
    }

    private void OnValidate() => byId = null;   // Inspector edits apply immediately, even mid-Play

    public DialogueCatalogEntry Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (byId == null)
        {
            byId = new Dictionary<string, DialogueCatalogEntry>(StringComparer.Ordinal);
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (byId.ContainsKey(e.id))
                {
                    Debug.LogWarning($"[DialogueCatalog] Duplicate id '{e.id}' - the first entry is used.", this);
                    continue;
                }
                byId.Add(e.id, e);
            }
        }
        return byId.TryGetValue(id, out var entry) ? entry : null;
    }

    /// <summary>Seconds this entry stays up: its own duration, or the catalog default for its type and priority.</summary>
    public float DurationFor(DialogueCatalogEntry entry)
    {
        if (entry == null) return dialogueGuidanceSeconds;
        if (entry.duration > 0f) return entry.duration;
        return DefaultDuration(entry.type, entry.priority);
    }

    public float DefaultDuration(DialogueEntryType type, DialogueKind priority)
    {
        if (type == DialogueEntryType.Tooltip)
        {
            return priority >= DialogueKind.Warning ? tooltipImportantSeconds : tooltipSeconds;
        }
        return priority switch
        {
            DialogueKind.Confirmation => dialogueConfirmationSeconds,
            DialogueKind.Warning => dialogueWarningSeconds,
            DialogueKind.Error => dialogueErrorSeconds,
            _ => dialogueGuidanceSeconds,
        };
    }
}
