using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

[Serializable]
public sealed class CompetitiveRunRecord
{
    public string runId, playerId, username, completedUtc, rulesVersion;
    public float score, recorderPoints;
    public double seconds;
    public bool isDemo;
}

[Serializable]
public sealed class CompetitiveRunHistory
{
    public int version = 1;
    public List<CompetitiveRunRecord> runs = new();
}

public static class CompetitiveRanking
{
    public static bool ValidUsername(string value) => value != null
        && Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9 _-]{1,19}\z");

    public static bool ValidRun(CompetitiveRunRecord run) => run != null
        && Guid.TryParse(run.runId, out _) && Guid.TryParse(run.playerId, out _)
        && ValidUsername(run.username) && !string.IsNullOrWhiteSpace(run.rulesVersion)
        && !float.IsNaN(run.score) && !float.IsInfinity(run.score) && run.score >= 0 && run.score <= 100
        && !double.IsNaN(run.seconds) && !double.IsInfinity(run.seconds) && run.seconds >= 0
        && run.recorderPoints >= 0 && run.recorderPoints <= 24 && !float.IsNaN(run.recorderPoints)
        && DateTimeOffset.TryParse(run.completedUtc, out _);

    public static bool Add(CompetitiveRunHistory history, CompetitiveRunRecord run)
    {
        if (!ValidRun(run) || run.isDemo || history.runs.Any(r => r.runId == run.runId)) return false;
        history.runs.Add(run);
        return true;
    }

    // Local profile ID is stable; display names are neither accounts nor credentials.
    public static List<CompetitiveRunRecord> Leaderboard(CompetitiveRunHistory history, string rules, bool includeDemo = false) => history.runs
        .Concat(includeDemo ? DemoRuns(rules) : Enumerable.Empty<CompetitiveRunRecord>())
        .Where(r => ValidRun(r) && r.rulesVersion == rules)
        .OrderByDescending(r => r.score).ThenBy(r => r.seconds).ThenBy(r => r.completedUtc, StringComparer.Ordinal)
        .GroupBy(r => r.playerId).Select(group => group.First()).ToList();

    // Presentation-only opponents. Never inserted into saved run history.
    public static IEnumerable<CompetitiveRunRecord> DemoRuns(string rules)
    {
        string[] names = { "Maya", "Alex", "Kai", "Sam", "Riley", "Jordan" };
        float[] scores = { 96, 90, 84, 76, 68, 55 };
        double[] times = { 1080, 1235, 1410, 1620, 1875, 2160 };
        for (int i = 0; i < names.Length; i++)
            yield return new CompetitiveRunRecord
            {
                runId = "d3000000-0000-0000-0000-" + (i + 1).ToString("D12"),
                playerId = "d4000000-0000-0000-0000-" + (i + 1).ToString("D12"),
                username = names[i], score = scores[i], seconds = times[i],
                completedUtc = "2026-01-01T00:00:00Z", rulesVersion = rules, isDemo = true
            };
    }

    public static string FormatTime(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss");
}
