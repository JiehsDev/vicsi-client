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
    /// extra precondition on Sealed → Processed. Deliberately a flag on the record
    /// rather than a new EvidenceStatus: the lifecycle sequence is shared by every
    /// item, and only some items need fingerprinting, so a per-item branch belongs in
    /// the record and not in a sequence every item has to walk.
    /// </summary>
    public bool fingerprintingDone;

    /// <summary>
    /// The tent number this item was marked with (1-based), or null if it has never
    /// been Marked, or was Marked and then reclaimed. Set by EvidenceStateManager.
    /// MarkTented and cleared by TryReclaimMarker on a genuine revert - not touched by
    /// anything else. Exists so a later step (the master sketch annotation) can label
    /// an item by the same number the player already sees on its physical tent,
    /// instead of inventing a second numbering scheme.
    /// </summary>
    public int? tentNumber;
}
