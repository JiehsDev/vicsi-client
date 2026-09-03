// Assets/_Project/Scripts/CaseFile/EvidenceStatus.cs

/// <summary>
/// The procedural lifecycle of one evidence item. The canonical ORDER these must be
/// performed in lives in EvidenceStateManager.RequiredSequence, not in this
/// declaration - see the comment there for why the two are kept separate.
///
/// Values are explicit and must stay that way. These ordinals are persisted outside
/// the C# declaration - HypothesisCheckpoints_*.asset serializes HypothesisTrigger.
/// thresholdStatus as a raw int in scene/asset YAML - so an implicit ordinal would
/// silently repoint every authored checkpoint the moment a member is inserted. That
/// is the same bug class already fixed on ToolType (explicit values) and
/// SessionEvent.eventType (persisted as a string instead of an int).
///
/// Explicit values make an insertion VISIBLE, not automatically correct: adding
/// Marked = 2 here shifted Photographed..Processed up by one, and every asset that
/// had persisted an old ordinal had to be corrected by hand in the same change.
/// Prefer appending at the end; if you must insert mid-sequence, grep the asset
/// files for the affected ints before you do.
/// </summary>
public enum EvidenceStatus
{
    NotFound = 0,           // exists in scene, not yet interacted with
    Found = 1,              // proximity trigger fired - the player noticed it (attention, not judgment)
    Marked = 2,             // player deliberately tented it as evidence (judgment) - see EvidenceTentTool
    Photographed = 3,
    Sketched = 4,
    Logged = 5,
    ReadyForCollection = 6, // all of the above are done - Collector's gate opens here
    Collected = 7,
    Processed = 8           // fingerprint dusting complete, where requiresFingerprinting demands it
}
