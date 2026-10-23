// Assets/_Project/Scripts/Conclusions/ConclusionLevel.cs

/// <summary>
/// UI/authoring categorization only - never branched on by ConclusionStateManager or
/// RequirementEvaluator, which both only ever read availabilityRequirement. Safe to
/// append a new value at the end later; nothing currently depends on these ordinals
/// surviving a reorder, but append-only is still the convention this project follows
/// for every persisted enum (EvidenceStatus, ToolType, SessionEventType, ...).
/// </summary>
public enum ConclusionLevel
{
    Factual,
    Interpretive,
    Case
}
