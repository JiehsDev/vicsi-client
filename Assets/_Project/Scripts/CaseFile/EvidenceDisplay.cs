using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Presentation only. Never use these labels as keys, requirements or event targets.</summary>
public static class EvidenceDisplay
{
    [Serializable] public class Entry { public string id; public int number; public string name; public string label; }
    [Serializable] private class Definition { public int version; public Entry[] entries; public Entry[] legacyEntries; }
    private static Definition definition;
    private static Definition Names => definition ??= JsonUtility.FromJson<Definition>(Resources.Load<TextAsset>("EvidenceDisplayNames_1").text);
    private static readonly Regex Ids = new(@"(?<![A-Za-z0-9])(?:fingerprint-sample:|blood-sample:)?(?:EVD-\d+|BODY-A)(?![A-Za-z0-9])");

    public static int Number(string id) => Find(id)?.number ?? 0;
    private static Entry Find(string id) => Array.Find(Names.entries, e => e.id == id)
        ?? Array.Find(Names.legacyEntries ?? Array.Empty<Entry>(), e => e.id == id);

    public static string Label(string id, string fallback = null)
    {
        if (string.IsNullOrEmpty(id)) return fallback ?? "Evidence";
        if (id.StartsWith("fingerprint-sample:", StringComparison.Ordinal))
            return "Fingerprint Sample – " + Label(id.Substring("fingerprint-sample:".Length));
        if (id.StartsWith("blood-sample:", StringComparison.Ordinal))
            return "Blood Sample – " + Label(id.Substring("blood-sample:".Length));
        var entry = Find(id);
        if (entry != null && fallback != null && fallback.StartsWith("Blood Sample", StringComparison.Ordinal))
            return "Blood Sample – " + entry.label;
        if (entry != null) return entry.label;
        if (Regex.IsMatch(id, @"^EVD-\d+$") && (string.IsNullOrEmpty(fallback) || fallback == id)) return "Unrecognized evidence";
        return Text(fallback ?? id);
    }

    /// <summary>Resolve legacy IDs embedded in visible prose. Does not mutate saved text or payloads.</summary>
    public static string Text(string text) => string.IsNullOrEmpty(text) ? text : QuestionDisplay.Text(Ids.Replace(text, m => Label(m.Value)));
}
