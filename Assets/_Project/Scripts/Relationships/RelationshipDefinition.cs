// Assets/_Project/Scripts/Relationships/RelationshipDefinition.cs
using UnityEngine;

/// <summary>
/// One authored candidate relationship between two board nodes (evidence items or
/// conclusion/red-herring cards - same nodeId space EvidenceNode.nodeId already uses).
/// Replaces DeductionScorer's old hardcoded requiredLinks/contradictoryLinks answer key -
/// see DeductionScorer's own comment for why that removal was safe (the board has never
/// been wired into any scene, so no serialized override of those fields exists anywhere
/// to preserve).
///
/// Deliberately holds no CSI_Environment-specific data in this CLASS - every field is
/// generic (plain id strings, a free-text type tag, a RequirementNode). Whatever gets
/// typed into an actual asset instance (e.g. sourceId "EVD-014", targetId
/// "conclusion_murder_weapon", relationshipType "means") is authored content, not
/// something this file hardcodes.
///
/// relationshipType is a free string rather than a C# enum on purpose - "editable rather
/// than hardcoded", so an instructor can introduce a new category of relationship by
/// typing a new word into the Inspector, not by waiting for a code change and risking the
/// exact enum-ordinal-drift bug class this project has already hit twice (EvidenceStatus,
/// ToolType).
///
/// Multiple RelationshipDefinition assets MAY share the same (sourceId, targetId) pair -
/// see RelationshipStateManager.FindByPair. Connecting that pair on the board marks every
/// matching definition Selected at once; nothing about the interaction reveals which
/// definition (if any) is isCorrect, which is what lets the player draw a wrong
/// connection without being told immediately - see the class's own scoring flow in
/// DeductionScorer.
/// </summary>
[CreateAssetMenu(fileName = "Relationship_", menuName = "VR-CSI/Relationships/Relationship Definition")]
public class RelationshipDefinition : ScriptableObject
{
    [Tooltip("Stable, author-set identifier (e.g. \"REL-knife-motive\"). Used as the logged/targetId value for relationship session events - must not be derived from list position.")]
    public string relationshipId;

    public string displayName;

    [Tooltip("The board node ids this relationship connects - matches EvidenceNode.nodeId (an EvidenceDefinition.evidenceId for an evidence card, or a made-up id like \"conclusion_murder_weapon\" for a conclusion/red-herring card). Order doesn't matter - a connection between A and B always matches a definition authored as (A,B) or (B,A); see RelationshipStateManager.FindByPair.")]
    public string sourceId;
    public string targetId;

    [Tooltip("Free-text category, e.g. \"supports\", \"contradicts\", \"motive\", \"means\", \"opportunity\" - editable per project, not a hardcoded enum. Purely classification/scoring metadata; the player never picks a type when connecting two nodes.")]
    public string relationshipType;

    [Tooltip("Must be satisfied before this relationship becomes Visible/Available - see RelationshipStateManager.RefreshUnlocks. Covers both a general unlock condition and a report-line-selected condition (RequirementType.ReportLineSelected is one of RequirementNode's leaf kinds), so this is the one gating field, not two. Empty ALL group (the default) means always unlocked.")]
    public RequirementNode unlockRequirement = new();

    [Tooltip("Scoring classification: is this the/a correct relationship to draw. Never read to give the player live feedback - only used by DeductionScorer after Submit Theory, and by session logging for post-hoc analysis.")]
    public bool isCorrect;

    [Tooltip("How much this relationship contributes to DeductionScorer's required-links score, relative to other RelationshipDefinitions marked isCorrect. Same role as EvidenceRelevance, applied to a link instead of an item.")]
    public float scoreWeight = 1f;

    [TextArea]
    [Tooltip("Dev/instructor-facing explanation of why this relationship is (or isn't) correct. Never shown to the player during a session - see InteractionFeedback's class comment on why identification correctness must stay hidden during play.")]
    public string explanation;

    [Tooltip("Optional forward-reference by id to a narrative event this relationship should trigger once Completed. Nothing consumes this yet - the narrative-event system (a later sub-project) will look it up by this id. Left blank, this does nothing.")]
    public string narrativeEventTriggerId;
}
