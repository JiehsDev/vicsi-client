using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Display only. Never pass these labels to scoring, events or upload keys.</summary>
public static class QuestionDisplay
{
    [Serializable] private class Relationship { public string id; public string label; }
    [Serializable] private class Definition { public int version; public string[] current; public string[] legacy; public Relationship[] relationships; }
    private static Definition definition;
    private static Definition Names => definition ??= JsonUtility.FromJson<Definition>(Resources.Load<TextAsset>("QuestionDisplayNames_1").text);
    private static readonly Regex Ids = new(@"(?<![A-Za-z0-9_])(?:HYP-[A-Za-z0-9-]+|RC-[A-Za-z0-9-]+|REL-[A-Za-z0-9-]+)(?![A-Za-z0-9_])");

    public static string Label(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Reasoning Checkpoint";
        int index = Array.IndexOf(Names.current, id);
        // Use the live authored order for current questions when available.
        var live = ReasoningCheckpointManager.Instance?.Definitions?.checkpoints;
        if (live != null) { int actual = live.FindIndex(c => c.id == id); if (actual >= 0) index = actual; }
        if (index < 0) index = Array.IndexOf(Names.legacy, id);
        if (index >= 0) return "Reasoning Question " + (index + 1);
        var source = Array.Find(Names.current, prefix => id.StartsWith(prefix + "-", StringComparison.Ordinal));
        if (source != null) return Label(source) + " – Answer " + id.Substring(source.Length + 1).Replace('-', ' ');
        var relationship = Array.Find(Names.relationships, r => r.id == id);
        if (relationship != null) return relationship.label;
        if (id.StartsWith("REL-", StringComparison.Ordinal)) return "Evidence Relationship: " + id.Substring(4).Replace('-', ' ');
        var number = Regex.Match(id, @"^(?:HYP|RC)-(\d+)(?:-|$)");
        return number.Success && int.TryParse(number.Groups[1].Value, out int n) && n > 0
            ? "Reasoning Question " + n : "Reasoning Checkpoint";
    }

    public static string Text(string text) => string.IsNullOrEmpty(text) ? text : Ids.Replace(text, m => Label(m.Value))
        .Replace("Hypothesis Checkpoint", "Reasoning Checkpoint")
        .Replace("Submit Hypothesis & Reasoning", "Confirm Answer");
}
