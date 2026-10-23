// Assets/_Project/Scripts/Deduction/DeductionScorer.cs
using UnityEngine;

/// <summary>
/// One node-id pair, used by EvidenceBoardController to track which links currently
/// exist on the board (and, before the RelationshipDefinition asset-driven answer key
/// replaced it, by DeductionScorer's own now-removed hardcoded requiredLinks/
/// contradictoryLinks fields - see DeductionScorer's class comment).
/// </summary>
[System.Serializable]
public struct NodePairId
{
    public string nodeIdA;
    public string nodeIdB;

    public NodePairId(string a, string b)
    {
        nodeIdA = a;
        nodeIdB = b;
    }
}

[System.Serializable]
public struct DeductionScoreResult
{
    public float requiredLinksScore;
    public float contradictionScore;
    public float timeScore;
    public float total;
    public float elapsedSeconds;
}

/// <summary>
/// Compares the player's board connections against the editable RelationshipDefinition
/// roster tracked by RelationshipStateManager and produces a 0-1 score. Weights: 0.5
/// required (isCorrect) links present, 0.3 absence of contradictory (!isCorrect) links,
/// 0.2 time efficiency (time spent reviewing the deduction board). Procedural
/// training time is excluded; assessment weights and relationship math are unchanged.
///
/// Previously read a hardcoded [SerializeField] answer key (requiredLinks/
/// contradictoryLinks) baked directly into this component. That was safe to remove
/// outright rather than migrate when that replacement was introduced. The current
/// CSI scene uses this asset-driven scorer; RelationshipStateManager remains its
/// source of truth. See RelationshipDefinition for the answer-key replacement.
/// </summary>
public class DeductionScorer : MonoBehaviour
{
    [Tooltip("Elapsed time (seconds) at or below which the player earns full time-efficiency credit.")]
    [SerializeField] private float targetTimeSeconds = 120f;

    [Tooltip("Elapsed time (seconds) at or above which time-efficiency credit is zero.")]
    [SerializeField] private float maxTimeSeconds = 300f;

    private float reasoningSeconds;
    private float reasoningStartedAt;
    private bool isReasoning;

    public void BeginReasoning()
    {
        if (isReasoning) return;
        reasoningStartedAt = Time.time;
        isReasoning = true;
    }

    public void EndReasoning()
    {
        if (!isReasoning) return;
        reasoningSeconds += Time.time - reasoningStartedAt;
        isReasoning = false;
    }

    private void OnDisable() => EndReasoning();

    /// <summary>
    /// Scores the current board state. board is accepted for signature compatibility
    /// with EvidenceBoardController.SubmitTheory (which already holds the connection
    /// data as RelationshipStateManager.Selected/Completed state by the time this is
    /// called - see SubmitTheory's own call order) but is not read directly here;
    /// scoring reads RelationshipStateManager, never the board's own connection
    /// dictionary, so there is exactly one source of truth for "what got connected."
    /// </summary>
    public DeductionScoreResult Score(EvidenceBoardController board)
    {
        if (FinalDeductionAssessment.ActiveInScene)
        {
            var result = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
            return new DeductionScoreResult { requiredLinksScore = result.deductionRelationships,
                contradictionScore = result.unsupportedRelationships == 0 ? 1 : 0, timeScore = 0,
                total = result.deductionRelationships, elapsedSeconds = reasoningSeconds + (isReasoning ? Time.time - reasoningStartedAt : 0f) };
        }
        float correctWeightAchieved = 0f;
        float correctWeightTotal = 0f;
        int incorrectSelectedCount = 0;
        int incorrectTotalCount = 0;

        if (RelationshipStateManager.Instance != null)
        {
            foreach (var record in RelationshipStateManager.Instance.AllRecords)
            {
                if (record?.definition == null)
                {
                    continue;
                }

                bool wasCompleted = record.state == RelationshipState.Completed;

                if (record.definition.isCorrect)
                {
                    correctWeightTotal += record.definition.scoreWeight;
                    if (wasCompleted)
                    {
                        correctWeightAchieved += record.definition.scoreWeight;
                    }
                }
                else
                {
                    incorrectTotalCount++;
                    if (wasCompleted)
                    {
                        incorrectSelectedCount++;
                    }
                }
            }
        }

        float requiredScore = correctWeightTotal <= 0f ? 1f : Mathf.Clamp01(correctWeightAchieved / correctWeightTotal);
        float contradictionScore = incorrectTotalCount == 0 ? 1f : 1f - ((float)incorrectSelectedCount / incorrectTotalCount);

        float elapsed = reasoningSeconds + (isReasoning ? Time.time - reasoningStartedAt : 0f);
        float timeScore = Mathf.Clamp01(1f - Mathf.InverseLerp(targetTimeSeconds, maxTimeSeconds, elapsed));

        return new DeductionScoreResult
        {
            requiredLinksScore = requiredScore,
            contradictionScore = contradictionScore,
            timeScore = timeScore,
            total = requiredScore * 0.5f + contradictionScore * 0.3f + timeScore * 0.2f,
            elapsedSeconds = elapsed
        };
    }
}
