// Assets/_Project/Scripts/Requirements/RequirementDefinition.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A requirement tree authored once as its own asset, so multiple consumers (a narrative
/// event's trigger and an ending's resolution requirement, say) can reference the same
/// named condition instead of each re-authoring an identical tree inline. Entirely
/// optional - a RequirementNode can always be embedded directly in a consumer instead;
/// nothing requires wrapping every condition in an asset. Reference it from a leaf via
/// RequirementType.RequirementAssetReference.
///
/// Deliberately holds no case-specific data of its own beyond whatever the author types
/// into its root tree's fields (evidenceId strings, statuses, line ids) - same boundary as
/// RequirementNode itself.
/// </summary>
[CreateAssetMenu(fileName = "Requirement_", menuName = "VR-CSI/Requirements/Requirement Definition")]
public class RequirementDefinition : ScriptableObject
{
    [Tooltip("The requirement tree this asset represents.")]
    public RequirementNode root = new();

#if UNITY_EDITOR
    /// <summary>
    /// Author-time validation, same warn-don't-block idiom as HypothesisCheckpointSet.
    /// OnValidate. Does not detect cross-asset cycles by itself (a cycle can only be
    /// discovered by walking actual RequirementAssetReference edges, which requires
    /// visiting other assets) - RequirementEvaluator.Evaluate is the authority for that at
    /// runtime, guarded so a cycle fails closed instead of hanging or overflowing the
    /// stack. This pass only flags structural issues local to this asset's own tree.
    /// </summary>
    private void OnValidate()
    {
        if (root == null)
        {
            return;
        }

        var warnings = new List<string>();
        root.Validate(warnings, name);

        foreach (var warning in warnings)
        {
            Debug.LogWarning($"[{name}] {warning}", this);
        }
    }
#endif
}
