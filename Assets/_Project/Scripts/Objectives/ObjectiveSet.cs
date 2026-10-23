// Assets/_Project/Scripts/Objectives/ObjectiveSet.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every investigation objective for one scenario. Standalone for now, same reasoning as
/// HypothesisCheckpointSet/NarrativeEventSet: no ScenarioDefinition root asset exists yet,
/// so this carries its own scenarioId.
/// </summary>
[CreateAssetMenu(fileName = "Objectives_", menuName = "VR-CSI/Objectives/Objective Set")]
public class ObjectiveSet : ScriptableObject
{
    [Tooltip("Which scenario these objectives belong to - match it to the scene/scenario name, e.g. \"CSI_Environment\".")]
    public string scenarioId;

    public List<Objective> objectives = new();

#if UNITY_EDITOR
    /// <summary>Author-time validation - same warn-don't-block idiom as HypothesisCheckpointSet/NarrativeEventSet.OnValidate.</summary>
    private void OnValidate()
    {
        if (objectives == null)
        {
            return;
        }

        var seenIds = new HashSet<string>();

        foreach (var objective in objectives)
        {
            if (objective == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(objective.objectiveId))
            {
                Debug.LogWarning($"[{name}] An objective has no objectiveId. It will never be trackable, and RequirementType.ObjectiveCompleted could never reference it.", this);
            }
            else if (!seenIds.Add(objective.objectiveId))
            {
                Debug.LogWarning($"[{name}] Duplicate objective id '{objective.objectiveId}'. Only the first registered instance is tracked.", this);
            }

            ValidateTree(objective.unlockRequirement, $"{objective.objectiveId}/unlockRequirement");
            ValidateTree(objective.completionRequirement, $"{objective.objectiveId}/completionRequirement");
            ValidateTree(objective.failureRequirement, $"{objective.objectiveId}/failureRequirement");
        }
    }

    private void ValidateTree(RequirementNode node, string path)
    {
        if (node == null)
        {
            return;
        }

        var warnings = new List<string>();
        node.Validate(warnings, path);
        foreach (var warning in warnings)
        {
            Debug.LogWarning($"[{name}] {warning}", this);
        }
    }
#endif
}
