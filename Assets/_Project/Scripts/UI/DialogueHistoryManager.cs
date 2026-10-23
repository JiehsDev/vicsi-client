// Assets/_Project/Scripts/UI/DialogueHistoryManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One line exactly as it was shown to the player.</summary>
[Serializable]
public class DialogueHistoryEntry
{
    /// <summary>Order of presentation within the session (1, 2, 3, ...).</summary>
    public int sequence;

    /// <summary>The displayed line, without any speaker prefix.</summary>
    public string text;

    /// <summary>The authored speaker, or empty when the line has none - never invented.</summary>
    public string speaker;

    /// <summary>Which channel showed it: "Narrative", "Team" (STCS) or "System" (toast).</summary>
    public string category;

    /// <summary>The narrative event / STCS trigger id that produced it, when known.</summary>
    public string sourceId;

    /// <summary>Seconds since the investigation scene loaded.</summary>
    public float sessionTimeSeconds;

    [NonSerialized] public int frame;

    /// <summary>What the history list shows above the line: the speaker if authored, else the channel.</summary>
    public string SourceLabel => !string.IsNullOrEmpty(speaker) ? speaker : category;
}

/// <summary>
/// Session-local record of every narrative, team (STCS) and system line the player has
/// actually been shown, for the Dialogue History panel. Review only: it never replays a
/// line, fires an event, touches evidence/objectives/findings or feeds scoring, and it is
/// never uploaded or persisted.
///
/// Fed from the two places a line becomes visible - STCSNotificationUI.ShowLine (narrative
/// beats and teammate lines) and NotificationUI's queue at the moment a toast is put on
/// screen - so a line is recorded once per presentation, however long it stays up. Lives
/// on a scene object, so a new investigation (a fresh scene load) always starts empty; it
/// also clears itself if the SessionLogger's session id changes underneath it.
/// </summary>
public class DialogueHistoryManager : MonoBehaviour
{
    public static DialogueHistoryManager Instance { get; private set; }

    [Tooltip("Oldest entries are dropped beyond this. A normal session never gets close.")]
    [SerializeField, Min(1)] private int maxEntries = 100;

    private readonly List<DialogueHistoryEntry> entries = new();
    private int nextSequence = 1;
    private string sessionId;

    /// <summary>Fired after an entry is added (the panel refreshes live if open).</summary>
    public event Action<DialogueHistoryEntry> OnEntryAdded;

    /// <summary>Fired when the history is cleared (new session).</summary>
    public event Action OnCleared;

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<DialogueHistoryEntry> Entries => entries;

    public int MaxEntries
    {
        get => maxEntries;
        set => maxEntries = Mathf.Max(1, value);
    }

    private void Awake()
    {
        Instance = this;
        sessionId = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Records a line that has just been shown. No-op when no manager exists (e.g. other scenes).</summary>
    public static void Record(string text, string speaker, string category, string sourceId)
    {
        if (Instance != null)
        {
            Instance.Add(text, speaker, category, sourceId);
        }
    }

    /// <summary>
    /// Adds an entry, or returns null for empty text or a same-frame repeat of the entry
    /// just added (one presentation reaching a recorder twice). The same line shown again
    /// later, as its own presentation, is a new entry.
    /// </summary>
    public DialogueHistoryEntry Add(string text, string speaker, string category, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        SyncSession();

        // Use the same spoken sentence as the live panel, preserving attribution separately.
        // Ordinary instructional colons and timestamps are part of the sentence.
        text = DialoguePresentation.SpokenText(text, ref speaker);

        if (entries.Count > 0)
        {
            var last = entries[entries.Count - 1];
            if (last.frame == Time.frameCount && last.text == text && last.sourceId == sourceId && last.speaker == (speaker ?? string.Empty))
            {
                return null;
            }
        }

        var entry = new DialogueHistoryEntry
        {
            sequence = nextSequence++,
            text = text,
            speaker = speaker ?? string.Empty,
            category = category ?? string.Empty,
            sourceId = sourceId ?? string.Empty,
            sessionTimeSeconds = Time.timeSinceLevelLoad,
            frame = Time.frameCount
        };
        entries.Add(entry);

        while (entries.Count > maxEntries)
        {
            entries.RemoveAt(0);
        }

        OnEntryAdded?.Invoke(entry);
        return entry;
    }

    /// <summary>
    /// "Name: line" -> ("Name", "line") when the prefix looks like a speaker: starts with a
    /// capital letter, 1-4 words, no digits (so "11:42 ..." or a sentence with a colon
    /// later on is left alone).
    /// </summary>
    public static bool TrySplitInlineSpeaker(string text, out string speaker, out string line)
    {
        speaker = null;
        line = text;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        int colon = text.IndexOf(':');
        if (colon < 2 || colon > 32 || colon >= text.Length - 2 || text[colon + 1] != ' ')
        {
            return false;
        }
        string prefix = text.Substring(0, colon).Trim();
        if (prefix.Length == 0 || !char.IsUpper(prefix[0]) || prefix.Split(' ').Length > 4)
        {
            return false;
        }
        foreach (char c in prefix)
        {
            if (!(char.IsLetter(c) || c == ' ' || c == '\'' || c == '-' || c == '.'))
            {
                return false;
            }
        }
        speaker = prefix;
        line = text.Substring(colon + 1).Trim();
        return line.Length > 0;
    }

    public void Clear()
    {
        entries.Clear();
        nextSequence = 1;
        OnCleared?.Invoke();
    }

    // A different SessionLogger session than the one this history belongs to means a new
    // investigation started without a scene reload - start the history over.
    private void SyncSession()
    {
        string current = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
        if (current != null && sessionId != null && current != sessionId)
        {
            Clear();
        }
        if (current != null)
        {
            sessionId = current;
        }
    }
}
