// Assets/_Project/Scripts/Measurement/MeasurementRequirementSet.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The active MeasurementRequirements for one scenario - assigned to the scene's
/// MeasurementStateManager. Same container shape as HypothesisCheckpointSet.
/// </summary>
[CreateAssetMenu(menuName = "ViCSI/Measurement Requirement Set", fileName = "Measurements_")]
public class MeasurementRequirementSet : ScriptableObject
{
    public string scenarioId;

    public List<MeasurementRequirement> requirements = new();

    [Header("Evidence-to-evidence distances")]
    [Tooltip("Accepted error for a recorded pair distance (metres). Recorded for later assessment; pair distances are not scored yet.")]
    [Min(0f)] public float pairTolerance = 0.1f;

    [Tooltip("Pair distances on the floor plan (height ignored), like the fixed references.")]
    public bool pairsMeasureOnFloorPlan = true;

    /// <summary>
    /// The scene roster for pair measurement: the evidence of every required requirement
    /// that participates, in requirement order (the stable order used for suggestions).
    /// </summary>
    public List<string> ScenePairRoster()
    {
        var roster = new List<string>();
        foreach (var r in requirements)
        {
            if (r != null && r.required && r.participatesInScenePairMeasurement && !string.IsNullOrEmpty(r.targetEvidenceId)
                && !roster.Contains(r.targetEvidenceId))
            {
                roster.Add(r.targetEvidenceId);
            }
        }
        return roster;
    }

    /// <summary>Every unique unordered pair of the roster, in roster order - n(n-1)/2 of them, never a reverse duplicate.</summary>
    public List<EvidencePairKey> RequiredScenePairs()
    {
        var roster = ScenePairRoster();
        var pairs = new List<EvidencePairKey>();
        for (int i = 0; i < roster.Count; i++)
        {
            for (int j = i + 1; j < roster.Count; j++)
            {
                pairs.Add(EvidencePairKey.Of(roster[i], roster[j]));
            }
        }
        return pairs;
    }

    public MeasurementRequirement Find(string requirementId)
    {
        if (string.IsNullOrEmpty(requirementId))
        {
            return null;
        }
        return requirements.Find(r => r != null && r.requirementId == requirementId);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var seen = new HashSet<string>();
        foreach (var r in requirements)
        {
            if (r == null || string.IsNullOrEmpty(r.requirementId))
            {
                Debug.LogWarning($"[{name}] A measurement requirement has no requirementId.", this);
                continue;
            }
            if (!seen.Add(r.requirementId))
            {
                Debug.LogWarning($"[{name}] Duplicate measurement requirementId '{r.requirementId}'.", this);
            }
            if (!string.IsNullOrEmpty(r.referenceAId) && r.referenceAId == r.referenceBId)
            {
                Debug.LogWarning($"[{name}] '{r.requirementId}' uses the same reference for A and B - its position can't be reconstructed.", this);
            }
        }
    }
#endif
}
