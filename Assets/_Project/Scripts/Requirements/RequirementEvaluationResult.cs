// Assets/_Project/Scripts/Requirements/RequirementEvaluationResult.cs
using System.Collections.Generic;

/// <summary>
/// What RequirementEvaluator.Evaluate returns - more than a bool so a submission-block
/// message or a dev-facing debug view can explain WHY, without any caller having to
/// re-walk the tree itself. FailureMessage/RelevantId/FailedNode are development and
/// procedural-block tooling only: they may explain that a required STEP hasn't happened
/// yet (e.g. "EVD-019 has not reached Logged yet"), matching the existing
/// ProceduralGateValidator.GetBlockReason convention, but nothing here is allowed to leak
/// which relationship/theory/ending is CORRECT - the same boundary
/// InteractionFeedback's class comment documents for Confirm/Blocked. Consumers building
/// player-facing copy must write their own neutral text, never surface FailureMessage
/// directly to the player.
/// </summary>
public class RequirementEvaluationResult
{
    public bool Passed;
    public RequirementFailureReason Reason = RequirementFailureReason.None;

    /// <summary>Human-readable, dev/debug-facing only. Never player-facing verbatim.</summary>
    public string FailureMessage;

    /// <summary>The evidenceId/lineId/asset name most relevant to the outcome, if any.</summary>
    public string RelevantId;

    /// <summary>Which node in the tree produced this result - the failing node on failure, the root on success.</summary>
    public RequirementNode Node;

    /// <summary>
    /// Per-child results, populated only when Evaluate was called with includeChildResults:
    /// true. Left null otherwise so a routine pass/fail check (the overwhelmingly common
    /// case - report-line availability, hypothesis gating, conclusion submission) doesn't
    /// allocate a result tree it will never read.
    /// </summary>
    public List<RequirementEvaluationResult> ChildResults;

    public static RequirementEvaluationResult Success(RequirementNode node, string relevantId = null)
    {
        return new RequirementEvaluationResult
        {
            Passed = true,
            Reason = RequirementFailureReason.Passed,
            Node = node,
            RelevantId = relevantId
        };
    }

    public static RequirementEvaluationResult Failure(RequirementNode node, RequirementFailureReason reason, string message, string relevantId = null)
    {
        return new RequirementEvaluationResult
        {
            Passed = false,
            Reason = reason,
            FailureMessage = message,
            Node = node,
            RelevantId = relevantId
        };
    }
}
