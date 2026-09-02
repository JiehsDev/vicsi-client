// Assets/_Project/Scripts/RoleSystem/ToolType.cs

/// <summary>
/// Identifies which equippable tool/capability the single player character is using
/// (or has ever last used, for evidence attribution) - not a separate playable
/// character or job title. TeamLeader and CaseAnalyst existed here historically for
/// the original multi-role design and were removed: neither is a tool-mediated action
/// in the unified single-investigator design. TeamLeader's job (deciding search
/// priority, what matters) is just the player's own judgment throughout Phases 2-4;
/// CaseAnalyst's job (reasoning about evidence, forming conclusions) is the Deduction
/// Board (EvidenceBoardController/DeductionScorer), not anything equippable from the
/// tool wheel. Explicit values are assigned so future additions/removals can never
/// silently shift an already-serialized value (e.g. ToolWheelController.roleIcons) out
/// from under existing scene data.
/// </summary>
public enum ToolType
{
    None = 0,
    Photographer = 1,
    IOC = 2,
    Sketcher = 3,
    EvidenceCollector = 4,
    Recorder = 5,
    EvidenceMarker = 6
}
