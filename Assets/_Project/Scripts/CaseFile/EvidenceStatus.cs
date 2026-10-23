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
///
/// Insertion log, so the next person can see what the check actually involves:
///   Marked = 2   shifted Photographed..Processed by one. Checkpoint asset needed
///                manual correction.
///   Sealed = 8   shifted only Processed (8 -> 9). The checkpoint asset persists
///                thresholdStatus 1, 7, 7 - none at or above 8 - so nothing needed
///                correcting. That was confirmed by reading the asset, not assumed
///                from the fact that the values are explicit.
///   Documentation-flow restructure (PhotographedRaw/ChalkMarked/PhotographedFinal
///   replace Photographed/Sketched; Marked moves from index 2 to index 4) - a full
///   reshuffle, not an insertion. HypothesisCheckpoints_CSI_Environment.asset
///   persisted thresholdStatus 1 (Found, unaffected), 7, 7 (both old Collected,
///   corrected to 8, the new Collected). Confirmed by reading the asset and
///   checking every value against the new positions, not by spot-check.
///   EvidenceStateManager.TryReclaimMarker also had to change: it used to revert a
///   pulled tent straight to Found, which was equivalent to "one step back" only
///   because Marked used to sit immediately after Found. With PhotographedRaw and
///   ChalkMarked now between them, a hardcoded revert-to-Found would have silently
///   erased those two completed steps. It now reverts to whatever
///   RequiredSequence's own entry immediately before Marked is, so it stays correct
///   regardless of future reshuffles.
/// </summary>
public enum EvidenceStatus
{
    NotFound = 0,             // exists in scene, not yet interacted with
    Found = 1,                // proximity trigger fired - the player noticed it (attention, not judgment)
    PhotographedRaw = 2,      // first photograph, taken before the tent is placed - the undisturbed scene
    ChalkMarked = 3,          // outline chalked around the item, before the numbered tent goes down
    Marked = 4,               // player deliberately tented it as evidence (judgment) - see EvidenceTentTool
    PhotographedFinal = 5,    // second photograph, taken with the numbered tent in frame - the documented scene
    Logged = 6,
    ReadyForCollection = 7,   // all of the above are done - Collector's gate opens here
    Collected = 8,
    Sealed = 9,               // tamper-evident seal applied at the scene - what actually protects custody between collection and the lab
    Processed = 10            // seal broken at the lab and analysis done; fingerprint dusting complete where requiresFingerprinting demands it
}
