// Assets/_Project/Scripts/Conclusions/ConclusionRecord.cs

/// <summary>
/// Runtime tracking for one ConclusionDefinition, for one play session. Mirrors
/// InsightRecord/FindingRecord/RelationshipRecord's own shape - a definition reference
/// plus mutable session state, held by reference in a Dictionary.
/// </summary>
public class ConclusionRecord
{
    public ConclusionDefinition definition;

    /// <summary>
    /// Once true, stays true for the rest of the session - same monotonic "once true,
    /// stays true" rule every other *Record.isX/isUnlocked field in this project already
    /// documents. A conclusion the evidence has already come to support doesn't stop being
    /// supported because some other, unrelated live state later changes.
    /// </summary>
    public bool isAvailable;
}
