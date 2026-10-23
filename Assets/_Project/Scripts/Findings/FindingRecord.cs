// Assets/_Project/Scripts/Findings/FindingRecord.cs

/// <summary>
/// Runtime tracking for one FindingDefinition, for one play session. Mirrors
/// RelationshipRecord/EvidenceRecord's own shape (a definition reference plus mutable
/// session state), kept as a plain class rather than a struct for the same reason those
/// are - it's held by reference in a Dictionary and mutated in place as the session
/// progresses.
/// </summary>
public class FindingRecord
{
    public FindingDefinition definition;

    /// <summary>
    /// Once true, stays true for the rest of the session - see
    /// FindingStateManager.RefreshFindings's own comment on why a finding is
    /// deliberately monotonic (representing knowledge already established by the
    /// investigator, not a live readout of the requirement's current truth value).
    /// </summary>
    public bool isConfirmed;
}
