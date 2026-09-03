// Assets/_Project/Scripts/Logging/SessionEventType.cs

/// <summary>
/// Every kind of event SessionLogger can record. This is the single place a new
/// phase registers a new event type - nothing else in the project should invent its
/// own event-type strings. Not every value here has something calling it yet (see
/// SessionLogger for what's actually wired up today: evidence status changes and
/// blocked transitions, subscribed automatically); the rest exist so the type is
/// ready before the system that fires them is built - Photography/Briefing/
/// Deduction Board scripts call SessionLogger.Instance.LogEvent(...) directly with
/// these once those systems exist or are being touched.
/// </summary>
public enum SessionEventType
{
    SessionStarted,
    SessionEnded,

    SceneEntered,
    PPEEquipped,
    SceneLogSigned,

    EvidenceStatusChanged,
    EvidenceTransitionBlocked,

    ToolEquipped,
    ToolUnequipped,

    PhotoTaken,

    BriefingStatementViewed,
    BriefingStatementFlagged,
    BriefingCompleted,

    HypothesisSubmitted,

    DeductionLinkCreated,
    DeductionLinkRemoved,
    DeductionSubmitted,

    /// <summary>
    /// The player placed an evidence tent on something that is not evidence at all -
    /// bare floor, scene dressing, untagged geometry. No status transition happens
    /// (there is nothing to transition), so this event is the ONLY record that the
    /// mis-identification occurred. It is the false-positive signal precision scoring
    /// will read against EvidenceRelevance.
    /// </summary>
    NonEvidenceMarked,

    /// <summary>
    /// The player pulled a placed tent back off, while the item was still only Marked -
    /// a legitimate pre-documentation correction. Appended alongside the original
    /// placement event, which is never deleted or rewritten; see
    /// EvidenceStateManager.TryReclaimMarker for why scoring must read the whole
    /// history rather than the final state.
    /// </summary>
    MarkerReclaimed,

    /// <summary>
    /// The player tried to reclaim a tent on an item already Photographed or later,
    /// and was refused. Logged rather than silently ignored, on the same reasoning as
    /// EvidenceTransitionBlocked: a blocked attempt is itself diagnostic.
    /// </summary>
    MarkerReclaimBlocked
}
