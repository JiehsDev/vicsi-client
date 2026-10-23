// Assets/_Project/Scripts/Insights/InsightDefinition.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One authored piece of reasoning generated FROM one or more confirmed findings - the
/// layer above Finding in the Finding -> Insight -> Relationship -> Conclusion chain
/// (see InsightStateManager's own class comment). An Insight never represents a player
/// deduction (that's a RelationshipDefinition) and never represents a case verdict
/// (that's a Conclusion/Ending) - it is investigative commentary the game offers once
/// the underlying findings genuinely support it, always non-conclusory by design (see
/// each authored instance's own description - "does not establish", "should not be
/// treated as proof", etc.).
///
/// Mirrors FindingDefinition/RelationshipDefinition's own shape closely - same stable
/// authored string id (never a C# enum, same enum-ordinal-drift reasoning those two
/// already document), same RequirementNode gate. unlockRequirement typically combines
/// one or more RequirementType.FindingConfirmed leaves under an ALL group, but nothing
/// about this class or InsightStateManager assumes that - any requirement tree the
/// shared RequirementEvaluator can evaluate works here, unchanged from every other
/// consumer.
/// </summary>
[CreateAssetMenu(fileName = "Insight_", menuName = "VR-CSI/Insights/Insight Definition")]
public class InsightDefinition : ScriptableObject
{
    [Tooltip("Stable, author-set identifier (e.g. \"INSIGHT-GLOVES-BLOOD-KNIFE-PRINT\"). Never derived from list position.")]
    public string insightId;

    public string displayName;

    [TextArea]
    [Tooltip("Player-facing reasoning text. Deliberately a single field, not per-order variants - see InsightRecord.TriggeringFindingId's own comment for why order-specific phrasing is a future extension point, not implemented yet (per this task's own explicit 'correctness over prose variation' guidance).")]
    public string description;

    [TextArea]
    [Tooltip("Optional player-facing suggestion of what to investigate next. Leave blank for an insight with no natural next step.")]
    public string suggestedAction;

    [Tooltip("Must be satisfied before this insight becomes Unlocked - see InsightStateManager.RefreshInsights. Typically an ALL group of RequirementType.FindingConfirmed leaves, but any requirement tree the shared RequirementEvaluator supports is valid here.")]
    public RequirementNode unlockRequirement = new();

    [Tooltip("Optional free-text category for future UI organization (e.g. \"physical\", \"motive\", \"methodology\") - editable per project, not a hardcoded enum, same reasoning as RelationshipDefinition.relationshipType/FindingDefinition.category.")]
    public string category;

    [Tooltip("Optional authoring/UI-ordering hint - higher shows first in a future sorted list. Not read by unlock logic itself.")]
    public int priority;

    [Tooltip("Authoring/debug convenience only - which findingIds this insight is conceptually built from, for the Inspector and for a future UI/debug view. Never read by unlockRequirement evaluation itself; the RequirementNode tree above is the only thing that actually gates unlocking. Free-typed strings, not object references, matching FindingDefinition.findingId's own plain-string convention.")]
    public List<string> sourceFindingIds = new();
}
