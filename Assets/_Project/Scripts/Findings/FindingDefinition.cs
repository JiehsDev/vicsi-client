// Assets/_Project/Scripts/Findings/FindingDefinition.cs
using UnityEngine;

/// <summary>
/// One authored fact the investigation can establish - what was LEARNED, as distinct
/// from EvidenceStatus (what the player physically DID) and from a RelationshipDefinition
/// (the player's own deduction on the board). A finding is confirmed purely by its own
/// unlockRequirement becoming satisfied; nothing about confirming one requires - or
/// implies - any board interaction. See FindingStateManager's own class comment for
/// where this sits in the wider Finding -> Insight -> Relationship -> Conclusion chain
/// this task is only the first layer of.
///
/// Mirrors RelationshipDefinition's own shape closely (stable authored string id,
/// displayName, a RequirementNode gate) - same "editable rather than hardcoded"
/// reasoning throughout: findingId is a plain string, never a C# enum, for the exact
/// reason RelationshipDefinition.relationshipId is - see that class's own comment on
/// the enum-ordinal-drift bug class this project has already hit twice.
///
/// Deliberately holds no CSI_Environment-specific data in this CLASS - every field is
/// generic. Whatever gets typed into an actual asset instance (e.g. findingId
/// "FIND-KNIFE-PRINT-BROTHER") is authored content, not something this file hardcodes.
/// This task authors exactly one harmless test instance for validation; real
/// case-specific findings (fingerprint-to-suspect mappings, etc.) are a later step -
/// see this class's own header comment in the task that created it.
/// </summary>
[CreateAssetMenu(fileName = "Finding_", menuName = "VR-CSI/Findings/Finding Definition")]
public class FindingDefinition : ScriptableObject
{
    [Tooltip("Stable, author-set identifier (e.g. \"FIND-KNIFE-PRINT-BROTHER\"). Used as the logged/targetId value for finding session events, and as the id RequirementNode.FindingConfirmed leaves reference from other systems - must not be derived from list position.")]
    public string findingId;

    public string displayName;

    [TextArea]
    [Tooltip("Player-facing explanation of what this finding actually established. Not shown anywhere yet by this foundation task - a future Findings UI is what will read this.")]
    public string description;

    [Tooltip("Optional - the evidence item this finding is derived from, for authoring traceability only. Never read by evaluation logic; unlockRequirement is what actually gates confirmation.")]
    public string sourceEvidenceId;

    [Tooltip("Authoring convenience only - drag an EvidenceDefinition here to auto-fill sourceEvidenceId above from its evidenceId field. Evaluation never reads this reference itself.")]
    public EvidenceDefinition sourceEvidenceReference;

    [Tooltip("Must be satisfied before this finding becomes Confirmed - see FindingStateManager.RefreshFindings. Uses the same shared RequirementNode/RequirementEvaluator every other gated system in this project already uses (evidence status, report lines, swab/fingerprint/flip flags, narrative events, objectives, relationships, or a shared RequirementDefinition asset). Empty ALL group (the default) means always confirmed the moment this finding is first evaluated.")]
    public RequirementNode unlockRequirement = new();

    [Tooltip("Optional free-text category for future UI organization (e.g. \"physical\", \"testimonial\", \"forensic\") - editable per project, not a hardcoded enum, same reasoning as RelationshipDefinition.relationshipType.")]
    public string category;
}
