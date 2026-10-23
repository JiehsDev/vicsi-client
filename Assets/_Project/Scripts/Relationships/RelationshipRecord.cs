// Assets/_Project/Scripts/Relationships/RelationshipRecord.cs

/// <summary>
/// Runtime tracking for one RelationshipDefinition, for one play session. Mirrors
/// EvidenceRecord's own shape (a definition reference plus mutable session state), kept
/// as a plain class rather than a struct for the same reason EvidenceRecord is - it's
/// held by reference in a Dictionary and mutated in place as the session progresses.
/// </summary>
public class RelationshipRecord
{
    public RelationshipDefinition definition;
    public RelationshipState state = RelationshipState.Locked;

    /// <summary>
    /// True the moment this relationship is ever marked Selected, even if the player
    /// later disconnects it. LOAD-BEARING FOR SCORING, same rule as
    /// EvidenceStateManager.TryReclaimMarker's own comment: state can revert (Selected
    /// back to Available on disconnect), but the fact that the player attempted this
    /// connection at some point must never be erased from what scoring can see. The
    /// RelationshipAttempted session-log event is the permanent record of when it
    /// happened; this flag is the in-memory mirror of "did that ever fire," read by
    /// DeductionScorer without needing to re-walk the session log.
    /// </summary>
    public bool wasEverSelected;
}
