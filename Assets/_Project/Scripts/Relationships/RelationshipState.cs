// Assets/_Project/Scripts/Relationships/RelationshipState.cs

/// <summary>
/// The lifecycle of one authored RelationshipDefinition, for one play session. Explicit
/// ints, appended-only forever after - same discipline as EvidenceStatus/ToolType/
/// SessionEventType, applied here before any real content persists an ordinal.
///
/// Visible and Available are distinct values but currently ADVANCE TOGETHER - nothing in
/// this pass distinguishes "conceptually unlocked" (Visible) from "concretely attemptable
/// right now" (Available), because EvidenceBoardController's nodes are all statically
/// pre-placed rather than spawned as they become relevant. The distinction is kept ready
/// rather than collapsed into one value, the same "ready before the system that uses it
/// exists" idiom SessionEventType's own class comment documents for HypothesisTriggerType.
/// SubmissionInitiated: if the board later spawns nodes dynamically as evidence is
/// discovered, a real gap between "the connection is a legitimate possibility" and "both
/// endpoints currently exist on the board" would need exactly this second value, with no
/// enum change required to add the logic that separates them.
/// </summary>
public enum RelationshipState
{
    /// <summary>unlockRequirement not yet satisfied - hidden from the player and from scoring.</summary>
    Locked = 0,

    /// <summary>unlockRequirement satisfied - see the class comment on why this and Available currently advance together.</summary>
    Visible = 1,

    /// <summary>Concretely attemptable right now - see the class comment.</summary>
    Available = 2,

    /// <summary>The player has drawn this exact connection on the board - a claim, not yet submitted.</summary>
    Selected = 3,

    /// <summary>Submission happened; this relationship has been evaluated and logged.</summary>
    Completed = 4
}
