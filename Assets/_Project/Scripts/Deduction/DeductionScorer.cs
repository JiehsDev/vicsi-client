// Assets/_Project/Scripts/Deduction/DeductionScorer.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One node-id pair, used both for the answer key (required/contradictory
/// links) and for tracking which links exist on the board.
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
/// Compares the player's board connections against a hardcoded MVP answer
/// key for the kitchen-homicide scenario and produces a 0-1 score.
/// Weights: 0.5 required links present, 0.3 absence of contradictory
/// links, 0.2 time efficiency (elapsed time since this component woke up).
/// </summary>
public class DeductionScorer : MonoBehaviour
{
    [Tooltip("Connections the player SHOULD make to reach the correct theory.")]
    [SerializeField]
    private List<NodePairId> requiredLinks = new()
    {
        new NodePairId("EVD-014", "conclusion_murder_weapon")
    };

    [Tooltip("Connections that indicate a wrong theory if the player makes them.")]
    [SerializeField]
    private List<NodePairId> contradictoryLinks = new()
    {
        new NodePairId("evd_red_herring", "conclusion_murder_weapon")
    };

    [Tooltip("Elapsed time (seconds) at or below which the player earns full time-efficiency credit.")]
    [SerializeField] private float targetTimeSeconds = 120f;

    [Tooltip("Elapsed time (seconds) at or above which time-efficiency credit is zero.")]
    [SerializeField] private float maxTimeSeconds = 300f;

    private float startTime;

    private void Awake()
    {
        startTime = Time.time;
    }

    public DeductionScoreResult Score(EvidenceBoardController board)
    {
        int requiredHit = 0;
        foreach (var link in requiredLinks)
        {
            if (board.IsConnected(link.nodeIdA, link.nodeIdB))
            {
                requiredHit++;
            }
        }
        float requiredScore = requiredLinks.Count == 0 ? 1f : (float)requiredHit / requiredLinks.Count;

        int contradictionsMade = 0;
        foreach (var link in contradictoryLinks)
        {
            if (board.IsConnected(link.nodeIdA, link.nodeIdB))
            {
                contradictionsMade++;
            }
        }
        float contradictionScore = contradictoryLinks.Count == 0
            ? 1f
            : 1f - ((float)contradictionsMade / contradictoryLinks.Count);

        float elapsed = Time.time - startTime;
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
