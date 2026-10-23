using System;
using System.Collections.Generic;

/// <summary>Keep speaker attribution as metadata rather than part of the spoken sentence.</summary>
public static class DialoguePresentation
{
    private static readonly HashSet<string> SpeakerLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dispatch", "Dispatcher", "IOC", "SOCO", "Recorder", "Photographer", "Professor",
        "Narrator", "Investigator", "First Responder", "Vendor", "Witness", "Officer",
        "Paul", "Peter", "John", "Brother", "Analyst", "Forensic Analyst", "Evidence Collector"
    };

    public static string SpokenText(string text, ref string speaker)
    {
        if (CompetitiveEdition.Enabled && string.Equals(speaker, "Professor", StringComparison.OrdinalIgnoreCase)) speaker = "Dispatch";
        if (string.IsNullOrEmpty(text)) return text;
        string line = EvidenceDisplay.Text(text);
        while (true)
        {
            int colon = line.IndexOf(':');
            if (colon < 1 || colon > 64) return line;
            string prefix = line.Substring(0, colon).Trim();
            if (!SpeakerLabels.Contains(prefix) && !string.Equals(prefix, speaker, StringComparison.OrdinalIgnoreCase)) return line;
            string rest = line.Substring(colon + 1).TrimStart();
            if (rest.Length == 0) return line;
            if (string.IsNullOrEmpty(speaker)) speaker = CompetitiveEdition.Enabled && string.Equals(prefix, "Professor", StringComparison.OrdinalIgnoreCase) ? "Dispatch" : prefix;
            line = rest;
        }
    }
}
