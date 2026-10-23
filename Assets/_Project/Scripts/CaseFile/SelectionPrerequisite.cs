// Assets/_Project/Scripts/CaseFile/SelectionPrerequisite.cs
using System.Collections.Generic;

/// <summary>
/// Which fact a SelectionPrerequisite checks. Added when a second kind of fact (the
/// gloves' lab-match reveal) needed the same cross-item-unlock treatment as the
/// original report-line-selection check, and the two are different enough facts
/// (a player choice vs. a runtime side-track outcome) that inlining a second set of
/// fields with the first ignored depending on type was cleaner than a parallel class -
/// same evidenceId-keyed shape, same registry-backed "vacuously false if unresolved"
/// semantics, just a different fact being asked about.
/// </summary>
public enum PrerequisiteType
{
    /// <summary>evidenceId must have lineId selected in its report - see EvidenceStateManager.HasSelectedReportLine. lineId is required; ignored for FingerprintMatched.</summary>
    ReportLineSelected,

    /// <summary>evidenceId's fingerprintLabStatus must be Matched - see EvidenceStateManager.IsFingerprintMatched/MarkMatched. lineId is ignored.</summary>
    FingerprintMatched
}

/// <summary>
/// One prerequisite for a cross-item unlock - e.g. { type: ReportLineSelected,
/// evidenceId: "BODY-A", lineId: "body-a-wound-sharp-object" } gating the knife's "may
/// be the murder weapon" option, or { type: FingerprintMatched, evidenceId: "EVD-019" }
/// gating HYP-03's correct reasoning option on the gloves' lab match actually
/// resolving. Default type (ReportLineSelected) is the pre-existing field's own
/// original behavior, so every prerequisite authored before this enum existed keeps
/// working unchanged with no data migration.
///
/// Deliberately its own small class rather than fields inlined on ReportLineOption:
/// the same shape is meant to sit on ReasoningOption too (now wired - see
/// HypothesisCheckpointManager.CorrectOptionPrerequisitesSatisfied). One class,
/// checked against the same runtime registries (EvidenceStateManager.
/// HasSelectedReportLine/IsFingerprintMatched) from both RecorderReportUI and
/// HypothesisCheckpointManager, keeps every call site reading the exact same rule
/// rather than copies that could drift.
/// </summary>
[System.Serializable]
public class SelectionPrerequisite
{
    public PrerequisiteType type = PrerequisiteType.ReportLineSelected;
    public string evidenceId;
    public string lineId;

    /// <summary>
    /// Whether every prerequisite in the list is currently met - a blank/malformed
    /// entry is treated as unsatisfiable rather than skipped, so a mis-authored
    /// prerequisite fails closed (the gated option/checkpoint simply never appears)
    /// instead of silently degrading to "no prerequisite at all." An empty or null
    /// list is always satisfied - the unchanged default for every option authored
    /// before this field existed. The single shared implementation of the rule this
    /// class's own comment already promises - originally lived only in
    /// RecorderReportUI.PrerequisitesSatisfied, duplicated (identically) into
    /// HypothesisCheckpointManager.CorrectOptionPrerequisitesSatisfied, and now also
    /// needed by HypothesisCheckpointUI for per-option reasoning-choice filtering;
    /// moved here once a third call site made "just copy it again" the wrong call.
    /// </summary>
    public static bool AllSatisfied(List<SelectionPrerequisite> prerequisites)
    {
        if (prerequisites == null || prerequisites.Count == 0)
        {
            return true;
        }

        if (EvidenceStateManager.Instance == null)
        {
            return false;
        }

        foreach (var prerequisite in prerequisites)
        {
            if (!EvaluateOne(prerequisite))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks a single prerequisite. Extracted from AllSatisfied's own loop body (pure
    /// refactor - AllSatisfied's behavior and public contract are unchanged) so
    /// RequirementEvaluator's RequirementType.LegacySelectionPrerequisite leaf can
    /// delegate to this exact logic instead of re-deriving it - see
    /// RequirementNode.FromLegacyList. This is the one authoritative place a single
    /// SelectionPrerequisite entry is evaluated; do not reimplement this switch
    /// elsewhere.
    /// </summary>
    public static bool EvaluateOne(SelectionPrerequisite prerequisite)
    {
        if (prerequisite == null || string.IsNullOrEmpty(prerequisite.evidenceId))
        {
            return false;
        }

        if (EvidenceStateManager.Instance == null)
        {
            return false;
        }

        switch (prerequisite.type)
        {
            case PrerequisiteType.FingerprintMatched:
                return EvidenceStateManager.Instance.IsFingerprintMatched(prerequisite.evidenceId);

            case PrerequisiteType.ReportLineSelected:
            default:
                return !string.IsNullOrEmpty(prerequisite.lineId) && EvidenceStateManager.Instance.HasSelectedReportLine(prerequisite.evidenceId, prerequisite.lineId);
        }
    }
}
