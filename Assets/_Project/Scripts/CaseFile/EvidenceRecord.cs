// Assets/_Project/Scripts/CaseFile/EvidenceRecord.cs
using System;
using System.Collections.Generic;

[Serializable]
public class EvidenceRecord
{
    /// <summary>Frozen Recorder-only assessment, separate from physical and deduction scores.</summary>
    public RecorderSubjectResult recorderDocumentation;
    // Owned by EvidenceStateManager; independent of the shared evidence/grade lifecycle.
    public bool bodyFindingsRecorded;
    public BodyRemovalStatus bodyRemovalStatus;
    public EvidenceDefinition definition;
    public EvidenceStatus status = EvidenceStatus.NotFound;
    public ToolType lastToolUsed = ToolType.None;
    public float statusChangedAtTime; // Time.time when last transitioned, for time-on-task logging later

    /// <summary>
    /// WHERE this item physically is - completely independent of status above (see
    /// EvidenceCustodyLocation's own class comment for why the two axes must never be
    /// conflated). Only ever assigned by EvidenceInventory; nothing else should set
    /// this field directly, the same "one owner" discipline fingerprintLabStatus
    /// already follows for MarkSentToLab/MarkMatched.
    /// </summary>
    public EvidenceCustodyLocation custodyLocation = EvidenceCustodyLocation.Scene;

    /// <summary>
    /// Whether the fingerprinting step has been performed on this item. Only
    /// meaningful when definition.requiresFingerprinting is true, where it becomes an
    /// extra precondition on Sealed → Processed. Deliberately a flag on the record
    /// rather than a new EvidenceStatus: the lifecycle sequence is shared by every
    /// item, and only some items need fingerprinting, so a per-item branch belongs in
    /// the record and not in a sequence every item has to walk.
    /// </summary>
    public bool fingerprintingDone;
    // Physical transfer to a backing card precedes explicit B/storage. The parent
    // fingerprint procedure remains incomplete until the derived card is stored.
    public bool fingerprintLifted;

    /// <summary>
    /// Whether this item's required biological trace has been swabbed. Only
    /// meaningful when definition.requiresSwabbing is true, where it becomes an
    /// extra precondition on PhotographedFinal → Logged. Same reasoning as
    /// fingerprintingDone - a per-item flag on the record rather than a shared
    /// status, since only some items (a knife with blood on it) carry a trace that
    /// needs sampling before they themselves can be logged.
    /// </summary>
    public bool swabbingDone;

    /// <summary>The on-scene swab sample's container has been sealed - see EvidenceStateManager.MarkSwabSampleSealed. Only meaningful for collectedBySwab + requiresSwabbing items.</summary>
    public bool swabSampleSealed;

    /// <summary>
    /// Whether this item has been physically flipped over. Only meaningful when
    /// definition.requiresFlipBeforeFingerprinting is true (the gloves, EVD-019, so
    /// far), where it becomes an extra precondition on top of
    /// fingerprintBeforeCollection's own PhotographedFinal requirement - a surface
    /// only reachable once flipped can't be printed before it's actually been turned
    /// over. Same "per-item flag beside status" shape as fingerprintingDone/
    /// swabbingDone above. Set by EvidenceStateManager.MarkFlipped.
    /// </summary>
    public bool isFlipped;

    /// <summary>
    /// The tent number this item was marked with (1-based), or null if it has never
    /// been Marked, or was Marked and then reclaimed. Set by EvidenceStateManager.
    /// MarkTented and cleared by TryReclaimMarker on a genuine revert - not touched by
    /// anything else. Exists so a later step (the master sketch annotation) can label
    /// an item by the same number the player already sees on its physical tent,
    /// instead of inventing a second numbering scheme.
    /// </summary>
    public int? tentNumber;

    /// <summary>
    /// The tent LETTER this item was marked with, or null if never marked with one.
    /// Mirrors tentNumber's own field exactly, but for items where
    /// definition.examinedInPlace is true (the body, so far the only one) - these use
    /// a letter tent instead of a numbered one, and a single item is never marked with
    /// both. Set by EvidenceStateManager.MarkTentedWithLetter.
    /// </summary>
    public string tentLetter;

    /// <summary>
    /// The number of the SECOND tent, placed on this item's trace (the blood on the knife
    /// blade), or null if not placed / picked back up. Only for definition.requiresTraceMarker
    /// items. Set by EvidenceStateManager.MarkTraceTented, cleared by TryReclaimTraceMarker.
    /// </summary>
    public int? traceTentNumber;

    /// <summary>
    /// Every report-line id confirmed as selected for THIS item so far this session -
    /// the runtime side of the cross-item unlock mechanism (see
    /// SelectionPrerequisite/ReportLineOption.prerequisites). Populated by
    /// EvidenceStateManager.RecordReportLinesSelected, called from RecorderTool right
    /// alongside the existing ReportLinesSelected session-log event - same data, same
    /// moment, just also kept queryable in memory instead of only written to disk.
    /// Never cleared once set: a line selected earlier in the session stays "selected"
    /// for prerequisite purposes even if a later report on the SAME item somehow
    /// re-opened (not a real flow today, but this is the same "once true, stays true"
    /// shape as fingerprintingDone/swabbingDone above).
    /// </summary>
    public readonly HashSet<string> selectedReportLineIds = new();

    /// <summary>
    /// The print's lab-identification status - see FingerprintLabStatus's own class
    /// comment for why this is deliberately decoupled from `status` above. Set by
    /// EvidenceStateManager.MarkSentToLab/MarkMatched only; nothing in the main chain
    /// (SetStatus, CanTransition, CheckProcessed) ever reads or writes this field.
    /// </summary>
    public FingerprintLabStatus fingerprintLabStatus = FingerprintLabStatus.NotSent;

    /// <summary>
    /// Null until fingerprintLabStatus reaches Matched, then holds the (currently
    /// hardcoded placeholder) match result - see EvidenceStateManager.MarkMatched's own
    /// comment for why. Not read by anything yet; exists as the eventual payload for a
    /// future STCS reveal line once real matching content is authored.
    /// </summary>
    public string fingerprintLabMatchResult;
    public List<string> fingerprintLabMatchIdentities = new();
}

public enum BodyRemovalStatus { NotPrepared = 0, PreparedForTransport = 1, Removed = 2 }
