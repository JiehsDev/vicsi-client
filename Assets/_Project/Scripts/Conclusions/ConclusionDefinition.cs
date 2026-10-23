// Assets/_Project/Scripts/Conclusions/ConclusionDefinition.cs
using UnityEngine;

/// <summary>
/// One authored, data-driven possible case conclusion - the layer ABOVE Relationship in
/// the Evidence Procedure -> Finding -> Insight -> Relationship -> ConclusionDefinition ->
/// (later) Submit Conclusion -> Ending chain. Deliberately separate from the existing
/// EndingSet.ConclusionOption (a plain {conclusionId, text} pair that only feeds the
/// Submit Conclusion picker's button labels) and from EndingSet.EndingDefinition (which
/// resolves the FINAL narrative outcome) - neither carries an availability requirement or
/// player-facing reasoning, and this task does not touch either. A ConclusionDefinition
/// only tracks whether the player's current findings/relationships actually SUPPORT a
/// given conclusion; nothing here feeds the picker or ending resolution yet.
///
/// Mirrors FindingDefinition/InsightDefinition/RelationshipDefinition's own shape closely:
/// stable authored string id (never a C# enum - same enum-ordinal-drift reasoning those
/// three already document), same RequirementNode gate, evaluated by the same shared
/// RequirementEvaluator.
/// </summary>
[CreateAssetMenu(fileName = "Conclusion_", menuName = "VR-CSI/Conclusions/Conclusion Definition")]
public class ConclusionDefinition : ScriptableObject
{
    [Tooltip("Stable, author-set identifier (e.g. \"CONCLUSION-WIFE-INVOLVEMENT\"). Never derived from list position. Deliberately a different id space from EndingSet.ConclusionOption.conclusionId (e.g. \"conclusion-wife\") - this task does not wire the two together.")]
    public string conclusionId;

    public string displayName;

    [TextArea]
    [Tooltip("Player-facing reasoning text - always non-conclusory about guilt beyond what the underlying findings actually establish, same discipline every authored InsightDefinition.description already follows.")]
    public string description;

    [TextArea]
    [Tooltip("Optional player-facing summary of which evidence backs this conclusion. Leave blank if description alone is sufficient.")]
    public string supportingEvidenceSummary;

    [Tooltip("Must be satisfied before this conclusion becomes Available - see ConclusionStateManager.RefreshConclusions. Prefer FindingConfirmed/RelationshipCompleted leaves over raw evidence-state checks.")]
    public RequirementNode availabilityRequirement = new();

    [Tooltip("When enabled and this requirement is met, this case conclusion represents a sufficiently developed accusation and hides the fallback Inconclusive option. The availability requirement still controls whether the accusation may be considered at all.")]
    public bool hidesInconclusiveWhenSupported;

    [Tooltip("Optional evidence-quality requirement used only when hidesInconclusiveWhenSupported is enabled. Point this at the same reusable requirement asset used by the supported ending so the picker and resolver cannot drift.")]
    public RequirementNode inconclusiveSuppressionRequirement = new();

    [Tooltip("UI/authoring categorization only (Factual / Interpretive / Case) - never read by availability evaluation itself.")]
    public ConclusionLevel level = ConclusionLevel.Factual;

    [Tooltip("Optional authoring/UI-ordering hint - higher shows first in a future sorted list. Not read by availability logic.")]
    public int priority;
}
