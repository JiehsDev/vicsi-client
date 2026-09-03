// Tools/EvidenceScorer/Program.cs
//
// Offline evidence-identification scorer.
//
//   dotnet run --project Tools/EvidenceScorer -- <session.jsonl> <groundtruth.json> [out.json]
//
// Reads one exported session log (the JSONL LocalJsonLogWriter already produces) and
// one scenario ground-truth export (ScenarioGroundTruthExporter's output), and computes
// evidence-identification metrics. It touches no gameplay code, no Unity API and no
// backend - it consumes files that already exist, after the run is over.
//
// The metrics are deliberately NOT collapsed into a single score. Critical and Relevant
// recall stay separate because "found everything decisive but missed the corroborating
// items" is a different student from the reverse. Distractor fall-rate stays separate
// from precision because it answers a design question ("did the planted false lead
// work") rather than a performance one ("how sloppy was the identification"). No
// pass/fail threshold is applied anywhere: turning these numbers into a grade is a
// faculty decision this tool has no business pre-empting.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace VicsiScoring;

// --- Input shapes -----------------------------------------------------------------

internal sealed class PayloadEntry
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

internal sealed class SessionEvent
{
    public string SessionId { get; set; } = "";
    public int SequenceNumber { get; set; }
    public long TimestampMs { get; set; }
    public string EventType { get; set; } = "";
    public string? TargetId { get; set; }
    public List<PayloadEntry>? Payload { get; set; }

    public string? PayloadValue(string key) =>
        Payload?.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal))?.Value;
}

internal sealed class GroundTruthEvidence
{
    public string EvidenceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Relevance { get; set; } = "";
}

internal sealed class GroundTruth
{
    public string ScenarioId { get; set; } = "";
    public string GeneratedAt { get; set; } = "";

    /// <summary>
    /// The game's canonical evidence lifecycle order, exported from
    /// EvidenceStateManager.RequiredSequence. This scorer deliberately holds NO
    /// independent copy of the order: a hardcoded one here would drift the moment a
    /// status was inserted into the game's sequence, and would do so silently -
    /// unknown statuses would rank below Marked and quietly deflate recall rather
    /// than erroring.
    ///
    /// Null when reading an export generated before this field existed. That is a
    /// hard failure, never a fallback - see Program.Main.
    /// </summary>
    public List<string>? LifecycleSequence { get; set; }

    public List<GroundTruthEvidence> Evidence { get; set; } = new();
}

// --- Output shape -----------------------------------------------------------------

internal sealed class ScoreReport
{
    public string SessionId { get; set; } = "";
    public string ScenarioId { get; set; } = "";

    public double CriticalRecall { get; set; }
    public double RelevantRecall { get; set; }
    public double Precision { get; set; }
    public double DistractorFallRate { get; set; }

    public int NonEvidenceMarkCount { get; set; }
    public int ReclaimCount { get; set; }
    public int ReclaimBlockedCount { get; set; }

    // Supporting counts, so a reader can audit any ratio above without re-parsing.
    public int CriticalTotal { get; set; }
    public int CriticalRecalled { get; set; }
    public int RelevantTotal { get; set; }
    public int RelevantRecalled { get; set; }
    public int DistractorTotal { get; set; }
    public int DistractorEverMarked { get; set; }
    public int TruePositiveMarks { get; set; }
    public int FalsePositiveMarks { get; set; }

    /// <summary>
    /// Marking events for ids the ground truth doesn't contain. Excluded from precision
    /// rather than guessed at - an id the scorer can't classify must not be silently
    /// counted as either correct or incorrect. Non-zero means the session and the
    /// ground truth disagree about what is in the scenario, which is a data problem to
    /// fix, not a score to interpret.
    /// </summary>
    public int UnclassifiedMarks { get; set; }
}

internal static class Program
{
    // NO hardcoded lifecycle order lives here by design. The sequence arrives with the
    // ground-truth export, straight from the game's own RequiredSequence, so the two
    // cannot drift apart. See GroundTruth.LifecycleSequence.

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static int Rank(IReadOnlyList<string> sequence, string? status)
    {
        if (string.IsNullOrEmpty(status)) return -1;
        for (int i = 0; i < sequence.Count; i++)
        {
            if (string.Equals(sequence[i], status, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: EvidenceScorer <session.jsonl> <groundtruth.json> [out.json]");
            return 2;
        }

        string sessionPath = args[0];
        string groundTruthPath = args[1];
        string? outPath = args.Length > 2 ? args[2] : null;

        if (!File.Exists(sessionPath)) { Console.Error.WriteLine($"session log not found: {sessionPath}"); return 2; }
        if (!File.Exists(groundTruthPath)) { Console.Error.WriteLine($"ground truth not found: {groundTruthPath}"); return 2; }

        var groundTruth = JsonSerializer.Deserialize<GroundTruth>(File.ReadAllText(groundTruthPath), ReadOptions);
        if (groundTruth is null || groundTruth.Evidence.Count == 0)
        {
            Console.Error.WriteLine("ground truth is empty or unreadable.");
            return 2;
        }

        // Hard failure, never a hardcoded fallback. Silently assuming an order is
        // precisely the bug this field was added to eliminate: a stale export paired
        // with a newer game would score against the wrong sequence and report a
        // confidently wrong number instead of refusing.
        if (groundTruth.LifecycleSequence is not { Count: > 0 })
        {
            Console.Error.WriteLine(
                "ground truth predates lifecycle export (no 'lifecycleSequence' field): regenerate it via " +
                "Unity menu Tools/VICSI/Export Scenario Ground Truth. Refusing to score against an assumed order.");
            return 2;
        }

        var sequence = groundTruth.LifecycleSequence;

        if (Rank(sequence, "Marked") < 0)
        {
            Console.Error.WriteLine(
                "ground truth's lifecycleSequence contains no 'Marked' status, which every recall metric is defined against. " +
                "Refusing to score.");
            return 2;
        }

        var relevanceById = groundTruth.Evidence.ToDictionary(e => e.EvidenceId, e => e.Relevance, StringComparer.Ordinal);

        var events = ReadEvents(sessionPath);
        if (events.Count == 0)
        {
            Console.Error.WriteLine("session log contained no readable events.");
            return 2;
        }

        var report = Score(events, groundTruth, relevanceById, sequence);

        string json = JsonSerializer.Serialize(report, WriteOptions);
        if (outPath is not null)
        {
            File.WriteAllText(outPath, json);
            Console.WriteLine($"wrote {outPath}");
        }

        PrintSummary(report);
        Console.WriteLine();
        Console.WriteLine(json);
        return 0;
    }

    private static List<SessionEvent> ReadEvents(string path)
    {
        var events = new List<SessionEvent>();
        int lineNumber = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            try
            {
                var ev = JsonSerializer.Deserialize<SessionEvent>(line, ReadOptions);
                if (ev is not null) events.Add(ev);
            }
            catch (JsonException ex)
            {
                // A partial trailing line is expected if a session was killed mid-write -
                // JSONL exists precisely so that costs one event, not the whole file.
                Console.Error.WriteLine($"warning: skipping unparseable line {lineNumber}: {ex.Message}");
            }
        }

        // Replay in recorded order regardless of file order.
        events.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));
        return events;
    }

    private static ScoreReport Score(
        List<SessionEvent> events,
        GroundTruth groundTruth,
        Dictionary<string, string> relevanceById,
        IReadOnlyList<string> sequence)
    {
        var finalStatus = new Dictionary<string, string>(StringComparer.Ordinal);
        var everMarked = new HashSet<string>(StringComparer.Ordinal);

        int truePositiveMarks = 0, falsePositiveMarks = 0, unclassifiedMarks = 0;
        int nonEvidenceMarkCount = 0, reclaimCount = 0, reclaimBlockedCount = 0;

        foreach (var ev in events)
        {
            switch (ev.EventType)
            {
                case "EvidenceStatusChanged":
                {
                    var id = ev.TargetId;
                    var status = ev.PayloadValue("status");
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(status)) break;

                    // Straight ordered replay gives the TRUE final state, including
                    // reversions: TryReclaimMarker emits its own EvidenceStatusChanged
                    // back to Found, so a reclaimed item is walked back here rather
                    // than needing MarkerReclaimed to be interpreted separately.
                    finalStatus[id] = status;

                    if (status == "Marked")
                    {
                        // Every arrival at Marked is one identification ATTEMPT, counted
                        // even if it is later reclaimed and even if the same item is
                        // marked again afterwards. A wrong guess that was walked back
                        // still happened; that is the whole reason the log is append-only.
                        everMarked.Add(id);

                        if (!relevanceById.TryGetValue(id, out var relevance))
                        {
                            unclassifiedMarks++;
                        }
                        else if (relevance is "Critical" or "Relevant")
                        {
                            truePositiveMarks++;
                        }
                        else if (relevance == "Distractor")
                        {
                            falsePositiveMarks++;
                        }
                        // Neutral is real evidence and correctly collectible, but does
                        // not discriminate between theories - deliberately neither a
                        // true nor a false positive.
                    }
                    break;
                }

                case "NonEvidenceMarked":
                    nonEvidenceMarkCount++;
                    falsePositiveMarks++;
                    break;

                case "MarkerReclaimed":
                    reclaimCount++;
                    break;

                case "MarkerReclaimBlocked":
                    reclaimBlockedCount++;
                    break;
            }
        }

        int markedRank = Rank(sequence, "Marked");

        int criticalTotal = 0, criticalRecalled = 0;
        int relevantTotal = 0, relevantRecalled = 0;
        int distractorTotal = 0, distractorEverMarked = 0;

        foreach (var item in groundTruth.Evidence)
        {
            finalStatus.TryGetValue(item.EvidenceId, out var status);
            bool reachedMarked = Rank(sequence, status) >= markedRank;

            switch (item.Relevance)
            {
                case "Critical":
                    criticalTotal++;
                    if (reachedMarked) criticalRecalled++;
                    break;
                case "Relevant":
                    relevantTotal++;
                    if (reachedMarked) relevantRecalled++;
                    break;
                case "Distractor":
                    distractorTotal++;
                    // Fall-rate is "ever marked", NOT final state: being taken in by the
                    // false lead and then recovering is still having been taken in.
                    if (everMarked.Contains(item.EvidenceId)) distractorEverMarked++;
                    break;
            }
        }

        int precisionDenominator = truePositiveMarks + falsePositiveMarks;

        return new ScoreReport
        {
            SessionId = events[0].SessionId,
            ScenarioId = groundTruth.ScenarioId,

            CriticalRecall = Ratio(criticalRecalled, criticalTotal),
            RelevantRecall = Ratio(relevantRecalled, relevantTotal),
            Precision = Ratio(truePositiveMarks, precisionDenominator),
            DistractorFallRate = Ratio(distractorEverMarked, distractorTotal),

            NonEvidenceMarkCount = nonEvidenceMarkCount,
            ReclaimCount = reclaimCount,
            ReclaimBlockedCount = reclaimBlockedCount,

            CriticalTotal = criticalTotal,
            CriticalRecalled = criticalRecalled,
            RelevantTotal = relevantTotal,
            RelevantRecalled = relevantRecalled,
            DistractorTotal = distractorTotal,
            DistractorEverMarked = distractorEverMarked,
            TruePositiveMarks = truePositiveMarks,
            FalsePositiveMarks = falsePositiveMarks,
            UnclassifiedMarks = unclassifiedMarks
        };
    }

    /// <summary>
    /// A ratio with no denominator is NaN, not 0. A scenario with no Distractor items
    /// has an undefined fall-rate; reporting 0.0 would read as "nobody fell for it",
    /// which is a claim the data cannot support.
    /// </summary>
    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? double.NaN : Math.Round((double)numerator / denominator, 4);

    private static void PrintSummary(ScoreReport r)
    {
        static string Pct(double v) => double.IsNaN(v) ? "n/a (no items)" : $"{v:P1}".Replace(" ", "");

        Console.WriteLine($"session   {r.SessionId}");
        Console.WriteLine($"scenario  {r.ScenarioId}");
        Console.WriteLine();
        Console.WriteLine($"  critical recall      {Pct(r.CriticalRecall),-16} ({r.CriticalRecalled}/{r.CriticalTotal} reached Marked or later)");
        Console.WriteLine($"  relevant recall      {Pct(r.RelevantRecall),-16} ({r.RelevantRecalled}/{r.RelevantTotal})");
        Console.WriteLine($"  precision            {Pct(r.Precision),-16} ({r.TruePositiveMarks} TP / {r.TruePositiveMarks + r.FalsePositiveMarks} marking attempts)");
        Console.WriteLine($"  distractor fall-rate {Pct(r.DistractorFallRate),-16} ({r.DistractorEverMarked}/{r.DistractorTotal} ever marked)");
        Console.WriteLine();
        Console.WriteLine($"  non-evidence marks   {r.NonEvidenceMarkCount}");
        Console.WriteLine($"  reclaims             {r.ReclaimCount} ({r.ReclaimBlockedCount} blocked)   [reported, not scored]");

        if (r.UnclassifiedMarks > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  WARNING: {r.UnclassifiedMarks} marking event(s) referenced ids absent from the ground truth.");
            Console.WriteLine("           Excluded from precision. Regenerate the ground-truth export for this scenario.");
        }
    }
}
