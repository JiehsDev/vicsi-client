// Assets/_Project/Scripts/Assessment/SessionLogReader.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads a LocalJsonLogWriter-produced .jsonl file back into SessionEvent objects - the
/// missing piece this project's own SessionEvent.cs doc comment calls out ("No reader
/// parses these files back yet ... should Enum.Parse&lt;SessionEventType&gt; this
/// field, not cast an int"). Lives outside the Logging folder on purpose: SessionLogger/
/// LocalJsonLogWriter/SessionEvent are the write-only source of truth for the local log
/// and stay untouched by this feature (see this feature's own ground rules); this is a
/// separate, purely additive consumer of files they already produce.
///
/// One line per SessionEvent (JsonUtility.ToJson, no wrapping array) - mirrors
/// Tools/EvidenceScorer/Program.ReadEvents exactly, including its tolerance for a
/// partial trailing line (a session killed mid-write costs one event, not the whole
/// file - that's the entire reason JSONL was chosen over a single JSON array) and its
/// "replay in recorded order regardless of file order" sort by sequenceNumber.
/// </summary>
public static class SessionLogReader
{
    /// <summary>
    /// Reads and parses every well-formed line in path, sorted by sequenceNumber.
    /// Never throws for a missing file (returns an empty list) or an unparsable line
    /// (that line is skipped and logged) - a partial/corrupt local log must degrade the
    /// upload, not crash the app that's trying to recover from exactly that situation.
    /// </summary>
    public static List<SessionEvent> ReadEvents(string path)
    {
        var events = new List<SessionEvent>();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return events;
        }

        int lineNumber = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                var evt = JsonUtility.FromJson<SessionEvent>(line);
                if (evt != null && !string.IsNullOrEmpty(evt.eventType))
                {
                    events.Add(evt);
                }
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"[SessionLogReader] Skipping unparseable line {lineNumber} in {path}: {ex.Message}");
            }
        }

        events.Sort((a, b) => a.sequenceNumber.CompareTo(b.sequenceNumber));
        return events;
    }

    /// <summary>Reads one payload value by key from an event, or null if absent - mirrors SessionEvent.PayloadValue's shape in Tools/EvidenceScorer/Program.cs.</summary>
    public static string PayloadValue(SessionEvent evt, string key)
    {
        if (evt?.payload == null)
        {
            return null;
        }

        foreach (var entry in evt.payload)
        {
            if (string.Equals(entry.key, key, StringComparison.Ordinal))
            {
                return entry.value;
            }
        }
        return null;
    }
}
