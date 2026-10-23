// Assets/_Project/Scripts/ScenePhotography/ScenePhotographyAssessment.cs
using System;

/// <summary>
/// Scene-photography performance for one session - counts plus the derived percentages.
/// A plain JsonUtility-safe POCO (no UnityEngine types, no references) so it can ride in
/// AssessmentSessionUploadDto unchanged. Display/analytics only on the client: the
/// server recomputes the authoritative category from the logged ScenePhoto* events
/// (vicsi-dashboard src/lib/scoring/scenePhotographyScorer.ts, same formula).
/// Percentages are 0-100.
/// </summary>
[Serializable]
public class ScenePhotographyAssessmentResult
{
    public int requiredPointCount;
    public int completedPointCount;          // required points completed
    public float coveragePercent;

    public int correctSequenceCount;         // completions taken when they were the expected point
    public float sequenceAccuracyPercent;

    public int wrongOrderAttempts;
    public int wrongPositionAttempts;
    public int wrongAngleAttempts;
    public int totalAttempts;
    public float procedureAccuracyPercent;   // position + angle accuracy

    public bool isComplete;
    public float completionSeconds;          // -1 when not completed

    public float scorePercent;
}

/// <summary>Relative weight of each component. Normalized by their sum, so they need not add to exactly 1.</summary>
[Serializable]
public struct ScenePhotographyWeights
{
    public float coverage;
    public float sequence;
    public float accuracy;

    public static ScenePhotographyWeights Default => new() { coverage = 0.5f, sequence = 0.3f, accuracy = 0.2f };
}

/// <summary>
/// The scoring formula, kept in one place. Each component is a bounded ratio, so a single
/// mistake costs a fraction of one component, never the whole score:
///   coverage = completed required / required
///   sequence = in-order completions / (all completions + wrong-order attempts)
///   accuracy = well-placed shots / (well-placed shots + wrong-position + wrong-angle)
///              where well-placed = completions + wrong-order attempts (those were taken
///              from the right spot, facing the right way - just at the wrong time)
///   score    = weighted mean of the three, 0-100.
/// With no completions and no attempts, sequence and accuracy are 0: nothing was
/// documented, so nothing earns credit.
/// </summary>
public static class ScenePhotographyScoring
{
    public static ScenePhotographyAssessmentResult Compute(
        int requiredPointCount,
        int completedRequiredCount,
        int completedTotalCount,
        int correctSequenceCount,
        int wrongOrderAttempts,
        int wrongPositionAttempts,
        int wrongAngleAttempts,
        bool isComplete,
        float completionSeconds,
        ScenePhotographyWeights weights)
    {
        float coverage = requiredPointCount > 0 ? Clamp01((float)completedRequiredCount / requiredPointCount) : 0f;

        int sequenceDenominator = completedTotalCount + wrongOrderAttempts;
        float sequence = sequenceDenominator > 0 ? Clamp01((float)correctSequenceCount / sequenceDenominator) : 0f;

        int wellPlaced = completedTotalCount + wrongOrderAttempts;
        int accuracyDenominator = wellPlaced + wrongPositionAttempts + wrongAngleAttempts;
        float accuracy = accuracyDenominator > 0 ? Clamp01((float)wellPlaced / accuracyDenominator) : 0f;

        float wc = Math.Max(0f, weights.coverage);
        float ws = Math.Max(0f, weights.sequence);
        float wa = Math.Max(0f, weights.accuracy);
        float weightSum = wc + ws + wa;
        if (weightSum <= 0f)
        {
            var d = ScenePhotographyWeights.Default;
            wc = d.coverage; ws = d.sequence; wa = d.accuracy;
            weightSum = wc + ws + wa;
        }

        float score = Clamp01((coverage * wc + sequence * ws + accuracy * wa) / weightSum);

        return new ScenePhotographyAssessmentResult
        {
            requiredPointCount = requiredPointCount,
            completedPointCount = completedRequiredCount,
            coveragePercent = Percent(coverage),
            correctSequenceCount = correctSequenceCount,
            sequenceAccuracyPercent = Percent(sequence),
            wrongOrderAttempts = wrongOrderAttempts,
            wrongPositionAttempts = wrongPositionAttempts,
            wrongAngleAttempts = wrongAngleAttempts,
            totalAttempts = completedTotalCount + wrongOrderAttempts + wrongPositionAttempts + wrongAngleAttempts,
            procedureAccuracyPercent = Percent(accuracy),
            isComplete = isComplete,
            completionSeconds = isComplete ? (float)Math.Round(completionSeconds, 1) : -1f,
            scorePercent = Percent(score)
        };
    }

    /// <summary>Short descriptive post-case feedback. Never names a specific point, view or piece of evidence.</summary>
    public static System.Collections.Generic.List<string> BuildFeedback(ScenePhotographyAssessmentResult r)
    {
        var lines = new System.Collections.Generic.List<string>();
        if (r == null || r.requiredPointCount == 0)
        {
            return lines;
        }

        if (r.completedPointCount == 0)
        {
            lines.Add("The scene was not documented with overall photographs before the investigation.");
            return lines;
        }

        if (r.completedPointCount < r.requiredPointCount)
        {
            lines.Add("Some required scene views were not documented.");
        }
        if (r.wrongOrderAttempts > 0)
        {
            lines.Add("Some photographs were taken out of the expected scene-documentation sequence.");
        }
        if (r.wrongPositionAttempts + r.wrongAngleAttempts > 0)
        {
            lines.Add("Some scene photographs were attempted from an incorrect position or direction.");
        }
        if (lines.Count == 0)
        {
            lines.Add("Scene documentation was completed with good coverage and sequence.");
        }
        return lines;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    private static float Percent(float ratio) => (float)Math.Round(ratio * 100f, 1);
}
