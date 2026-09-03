// Assets/_Project/Scripts/CaseFile/EvidenceRecord.cs
using System;

[Serializable]
public class EvidenceRecord
{
    public EvidenceDefinition definition;
    public EvidenceStatus status = EvidenceStatus.NotFound;
    public ToolType lastToolUsed = ToolType.None;
    public float statusChangedAtTime; // Time.time when last transitioned, for time-on-task logging later

    /// <summary>
    /// Whether the fingerprinting step has been performed on this item. Only
    /// meaningful when definition.requiresFingerprinting is true, where it becomes an
    /// extra precondition on Collected → Processed. Deliberately a flag on the record
    /// rather than a new EvidenceStatus: the lifecycle sequence is shared by every
    /// item, and only some items need fingerprinting, so a per-item branch belongs in
    /// the record and not in a sequence every item has to walk.
    /// </summary>
    public bool fingerprintingDone;
}
