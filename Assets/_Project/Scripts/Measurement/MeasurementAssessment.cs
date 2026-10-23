// Assets/_Project/Scripts/Measurement/MeasurementAssessment.cs
using System;
using System.Collections.Generic;

/// <summary>
/// One measurement requirement's state at upload time, read from MeasurementStateManager's
/// authoritative MeasurementRecord. A plain JsonUtility-safe POCO so it can ride in
/// AssessmentSessionUploadDto unchanged. Distances in metres; a side never measured has
/// measuredDistance -1. Timestamps are Time.time seconds, -1 when not reached.
/// The component scores are the client's display copy only - the server recomputes them
/// from the logged Measurement* events against its own expected distances
/// (vicsi-dashboard src/lib/scoring/measurementScorer.ts, same formula).
/// </summary>
[Serializable]
public class MeasurementItemResult
{
    public string requirementId;
    public string evidenceId;
    public string referenceAId;
    public string referenceBId;

    public float expectedDistanceA;
    public float expectedDistanceB;
    public float measuredDistanceA = -1f;
    public float measuredDistanceB = -1f;
    public float tolerance;

    public bool completedA;          // reference A measured
    public bool completedB;          // reference B measured
    public bool completedBoth;
    public bool withinToleranceA;
    public bool withinToleranceB;
    public bool late;                // any side measured after the item was disturbed
    public bool isComplete;          // MeasurementStateManager's own completion (both sides, in tolerance)

    public float measuredAtSecondsA = -1f;
    public float measuredAtSecondsB = -1f;
    public float completedAtSeconds = -1f;

    // 0-1 components and the 0-100 item score (display only).
    public float completionScore;
    public float accuracyScore;
    public float timingScore;
    public float scorePercent;
}

/// <summary>
/// One required evidence-to-evidence distance at upload time (MeasurementStateManager's
/// EvidencePairRecord). Analytics only - no scoring rules version scores pairs yet.
/// measuredDistance/expectedDistance are -1 until measured.
/// </summary>
[Serializable]
public class EvidencePairResult
{
    public string pairKey;           // normalised "A|B" (ordinal sort)
    public string evidenceA;
    public string evidenceB;
    public bool isComplete;
    public float measuredDistance = -1f;
    public float expectedDistance = -1f;
    public float tolerance;
    public bool withinTolerance;
    public bool late;                // either item disturbed before the pair was measured
    public bool missed;              // never measured, and an item already left the scene - no longer measurable
    public float measuredAtSeconds = -1f;
    public int order;
}

/// <summary>Every required measurement plus the equal-weight aggregate. Percentages are 0-100.</summary>
[Serializable]
public class MeasurementAssessmentResult
{
    public int requirementCount;
    public int completedCount;       // items with both sides measured
    public int accurateCount;        // items with both sides within tolerance
    public int lateCount;
    public float completionPercent;
    public float accuracyPercent;
    public float timingPercent;
    public float scorePercent;
    public List<MeasurementItemResult> items = new();

    // Scene measurement (fixed-reference sides + evidence pairs) - additive fields, ignored
    // by the server's measurement scoring, which reads only the fixed-reference events.
    // The aggregate/score fields above describe the fixed references only, as before.
    public int fixedSidesRequired;
    public int fixedSidesCompleted;
    public int pairRequiredCount;
    public int pairCompletedCount;
    public int pairLateCount;
    public bool sceneMeasurementsComplete;
    public List<EvidencePairResult> pairs = new();
}

/// <summary>
/// The measurement scoring formula, kept in one place (mirrors the server's
/// measurementScorer.ts):
///   completion = both references measured 1, one 0.5, none 0
///   accuracy   = (A within tolerance + B within tolerance) / 2 - an unmeasured side is not accurate
///   timing     = 1 when something was measured and no side was taken after disturbance, else 0
///   item       = 0.3 completion + 0.5 accuracy + 0.2 timing
///   score      = equal-weight mean over the required items.
/// </summary>
public static class MeasurementScoring
{
    public const float CompletionWeight = 0.3f;
    public const float AccuracyWeight = 0.5f;
    public const float TimingWeight = 0.2f;

    public static MeasurementItemResult BuildItem(MeasurementRecord record)
    {
        var requirement = record.requirement;
        var item = new MeasurementItemResult
        {
            requirementId = requirement.requirementId,
            evidenceId = requirement.targetEvidenceId,
            referenceAId = requirement.referenceAId,
            referenceBId = requirement.referenceBId,
            expectedDistanceA = requirement.expectedDistanceA,
            expectedDistanceB = requirement.expectedDistanceB,
            measuredDistanceA = record.hasA ? record.measuredA : -1f,
            measuredDistanceB = record.hasB ? record.measuredB : -1f,
            tolerance = requirement.tolerance,
            completedA = record.hasA,
            completedB = record.hasB,
            completedBoth = record.hasA && record.hasB,
            withinToleranceA = record.hasA && record.withinToleranceA,
            withinToleranceB = record.hasB && record.withinToleranceB,
            late = record.takenAfterDisturbance,
            isComplete = record.IsComplete,
            measuredAtSecondsA = record.measuredAtA,
            measuredAtSecondsB = record.measuredAtB,
            completedAtSeconds = record.completedAtSessionTime
        };
        Score(item);
        return item;
    }

    public static void Score(MeasurementItemResult item)
    {
        item.completionScore = item.completedBoth ? 1f : (item.completedA || item.completedB ? 0.5f : 0f);
        item.accuracyScore = ((item.withinToleranceA ? 1f : 0f) + (item.withinToleranceB ? 1f : 0f)) / 2f;
        item.timingScore = (item.completedA || item.completedB) && !item.late ? 1f : 0f;
        item.scorePercent = Round1(100f * (CompletionWeight * item.completionScore
            + AccuracyWeight * item.accuracyScore + TimingWeight * item.timingScore));
    }

    public static MeasurementAssessmentResult Aggregate(List<MeasurementItemResult> items)
    {
        var result = new MeasurementAssessmentResult { items = items, requirementCount = items.Count };
        if (items.Count == 0)
        {
            return result;
        }

        float completion = 0f, accuracy = 0f, timing = 0f;
        foreach (var item in items)
        {
            completion += item.completionScore;
            accuracy += item.accuracyScore;
            timing += item.timingScore;
            if (item.completedBoth)
            {
                result.completedCount++;
            }
            if (item.withinToleranceA && item.withinToleranceB)
            {
                result.accurateCount++;
            }
            if (item.late)
            {
                result.lateCount++;
            }
        }

        int n = items.Count;
        result.completionPercent = Round1(100f * completion / n);
        result.accuracyPercent = Round1(100f * accuracy / n);
        result.timingPercent = Round1(100f * timing / n);
        result.scorePercent = Round1(100f * (CompletionWeight * completion + AccuracyWeight * accuracy + TimingWeight * timing) / n);
        return result;
    }

    private static float Round1(float v) => (float)Math.Round(v, 1, MidpointRounding.AwayFromZero);
}
