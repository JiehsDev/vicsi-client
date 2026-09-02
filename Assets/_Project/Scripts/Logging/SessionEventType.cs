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
    DeductionSubmitted
}
