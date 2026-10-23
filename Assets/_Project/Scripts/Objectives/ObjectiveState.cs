// Assets/_Project/Scripts/Objectives/ObjectiveState.cs

/// <summary>
/// The lifecycle of one Objective, for one play session. Explicit ints, appended-only
/// forever after - same discipline as EvidenceStatus/RelationshipState/ToolType.
///
///   Hidden (0) -> Active (1) -> Completed (2)
///                 Active (1) -> Failed (3)
///
/// Both Completed and Failed are terminal in this pass - once reached, ObjectiveStateManager
/// never transitions out of them again, matching the "once true, stays true" convention
/// already established for EvidenceRecord's own per-item flags. If a scenario later needs
/// a failed objective to become recoverable, that is a deliberate, separate extension, not
/// a default behavior to guess at now.
/// </summary>
public enum ObjectiveState
{
    Hidden = 0,
    Active = 1,
    Completed = 2,
    Failed = 3
}
