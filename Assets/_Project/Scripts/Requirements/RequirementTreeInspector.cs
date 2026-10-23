// Assets/_Project/Scripts/Requirements/RequirementTreeInspector.cs
using System.Collections.Generic;

/// <summary>
/// Read-only, generic recursive extraction over a RequirementNode tree - "which
/// findingIds/relationshipIds does this tree reference at all", regardless of how deeply
/// nested under ALL/ANY/NOT groups. Purely structural: never evaluates truth (see
/// RequirementEvaluator for that), never reads any scenario-specific id, never hardcodes
/// a suspect/finding/relationship name. Added for the post-case review screen, which
/// needs to say "these are the findings/relationships THIS conclusion's own tree
/// referenced" without CompletionScreenUI duplicating tree-walking logic or hardcoding
/// per-conclusion content - see CompletionScreenUI's own comment on why supporting
/// findings/relationships must be data-derived, not authored text.
/// </summary>
public static class RequirementTreeInspector
{
    /// <summary>Every distinct findingId referenced by a FindingConfirmed leaf anywhere in the tree, in first-encountered order.</summary>
    public static List<string> CollectFindingIds(RequirementNode root)
    {
        var results = new List<string>();
        var seen = new HashSet<string>();
        Walk(root, node =>
        {
            if (node.requirementType == RequirementType.FindingConfirmed && !string.IsNullOrEmpty(node.findingId) && seen.Add(node.findingId))
            {
                results.Add(node.findingId);
            }
        });
        return results;
    }

    /// <summary>Every distinct relationshipId referenced by a RelationshipCompleted leaf anywhere in the tree, in first-encountered order.</summary>
    public static List<string> CollectRelationshipIds(RequirementNode root)
    {
        var results = new List<string>();
        var seen = new HashSet<string>();
        Walk(root, node =>
        {
            if (node.requirementType == RequirementType.RelationshipCompleted && !string.IsNullOrEmpty(node.relationshipId) && seen.Add(node.relationshipId))
            {
                results.Add(node.relationshipId);
            }
        });
        return results;
    }

    private static void Walk(RequirementNode node, System.Action<RequirementNode> visitLeaf)
    {
        if (node == null)
        {
            return;
        }

        if (node.kind == RequirementNodeKind.Group)
        {
            if (node.children == null)
            {
                return;
            }
            foreach (var child in node.children)
            {
                Walk(child, visitLeaf);
            }
            return;
        }

        visitLeaf(node);
    }
}
