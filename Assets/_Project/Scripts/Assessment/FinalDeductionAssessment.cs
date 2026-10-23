using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Wire records contain stable choices and frozen facts. Neither server nor client
// trusts a submitted numerical weight: both resolve IDs through the versioned rules.
[Serializable] public class DeductionCheckpointState
{
    public string checkpointId, responseId;
    public bool wasEligible;
    public int responseOrder;
    public double eligibleSeconds, answeredSeconds;
}
[Serializable] public class DeductionBoardPair { public string sourceId, targetId; }
[Serializable] public class DeductionAssessmentInput
{
    public string scoringRulesVersion, contentVersion, selectedConclusionId, resolvedEndingId;
    public bool atConclusion, requiresSceneRelease, checkpointSystemReady, form1Received, victimObserved, sceneSecurityAcknowledged;
    public List<string> iocObservationIds = new(), confirmedFindingIds = new(), discoveredStatementIds = new(), observedEvidenceIds = new();
    public List<DeductionCheckpointState> checkpoints = new();
    public List<string> completedRelationshipIds = new();
    public List<DeductionBoardPair> boardConnections = new();
    public int relationshipReviewVersion; // 0/missing retains historical board scoring.
    public List<RelationshipInterpretationResponse> relationshipResponses = new();
    public int questionRecordVersion;
    public List<DeductionQuestionRecord> questionRecords = new();
}
[Serializable] public class DeductionAssessmentResult
{
    public string scoringRulesVersion, status;
    public bool hasFinalScore;
    public float totalScore, reasoningCheckpoints, deductionRelationships, finalConclusion;
    public float reasoningComponent, relationshipComponent, conclusionComponent, relationshipEarned, relationshipMaximum;
    public int checkpointEarned, checkpointMaximum, answeredCount, eligibleUnansweredCount, undiscoveredCount, unsupportedRelationships;
    public List<string> reviewReasons = new();
}
[Serializable] public class DeductionRelationshipRule
{
    public string id, sourceId, targetId, statementId, evidenceId;
    public float weight;
    public bool requiresForm1;
    public string reviewPrompt, correctInterpretationId;
    public List<RelationshipInterpretationChoice> interpretations = new();
    public List<string> findingIds = new();
}
[Serializable] public class DeductionCheckpointRule
{
    public string id, findingId, category;
    public ReasoningTrigger trigger;
    public List<ReasoningResponse> responses = new();
}
[Serializable] public class DeductionScoringDefinition
{
    public string scoringRulesVersion, scenarioId, scenarioVersion, groundTruthVersion, contentVersion, validationStatus;
    public float checkpointWeight, relationshipWeight, conclusionWeight, unsupportedRelationshipPenalty;
    public int maximumCheckpointWeight;
    public List<DeductionCheckpointRule> checkpoints = new();
    public List<DeductionRelationshipRule> relationships = new();
    public List<string> supportedFindingIds = new(), supportedRelationshipIds = new();
    public string supportedStatementId, supportedConclusionId, supportedEndingId, prematureEndingId;
    public string peterConclusionId, peterEndingId, inconclusiveConclusionId, inconclusiveEndingId;
    public float supportedConclusionCredit, prematureConclusionCredit, peterConclusionCredit, inconclusiveInsufficientCredit, inconclusiveSupportedCredit;
}

/// <summary>Pure versioned 60/25/15 deduction calculation plus a conclusion-time
/// fact snapshot. All numerical configuration lives in the Resources JSON, mirrored
/// byte-for-byte on the server. Historical DeductionScorer behavior is a separate path.</summary>
public static class FinalDeductionAssessment
{
    public const string SnapshotEvent = "DeductionAssessmentSnapshot";
    private static DeductionScoringDefinition rules;
    private static DeductionAssessmentInput frozen;
    private static string frozenSessionId;
    public static DeductionScoringDefinition Rules => rules ??= JsonUtility.FromJson<DeductionScoringDefinition>(Resources.Load<TextAsset>("DeductionScoring_1_7_0").text);
    // Scenario asset is outside Resources; the upload owner's configured scenario
    // supplies the real version. No change to role/scene ownership is required.
    public static bool UsesCurrentRules(string version) => version == Rules.scoringRulesVersion;
    public static bool ActiveInScene => AssessmentUploadService.Instance != null
        && UsesCurrentRules(AssessmentUploadService.Instance.ScoringRulesVersion);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { rules = null; frozen = null; frozenSessionId = null; }

    public static DeductionAssessmentInput CaptureLive(string conclusionId = null, string endingId = null)
    {
        var manager = ReasoningCheckpointManager.Instance;
        var input = new DeductionAssessmentInput
        {
            scoringRulesVersion = Rules.scoringRulesVersion, contentVersion = manager?.Definitions?.contentVersion ?? "",
            selectedConclusionId = conclusionId ?? "", resolvedEndingId = endingId ?? "",
            atConclusion = !string.IsNullOrEmpty(conclusionId) && !string.IsNullOrEmpty(endingId),
            requiresSceneRelease = SceneTurnover.IsGated,
            checkpointSystemReady = SessionLogger.Instance != null && manager != null && manager.IsSystemReady && manager.Definitions.contentVersion == Rules.contentVersion
                && manager.Definitions.checkpoints.Count == Rules.checkpoints.Count && Rules.checkpoints.All(c=>manager.Definitions.checkpoints.Any(d=>
                    d.id == c.id && d.trigger == c.trigger && (d.findingId ?? "") == c.findingId && d.category == c.category
                    && d.responses.Count == c.responses.Count && c.responses.All(r=>d.responses.Any(a=>a.id == r.id && a.weight == r.weight)))),
            form1Received = SceneTurnover.Form1Received, victimObserved = SceneTurnover.VictimObserved,
            sceneSecurityAcknowledged = SceneTurnover.SceneSecurityAcknowledged
        };
        input.iocObservationIds.AddRange(SceneTurnover.IOCObservations);
        if (FindingStateManager.Instance != null) input.confirmedFindingIds.AddRange(FindingStateManager.Instance.GetConfirmedFindings());
        if (VendorFollowupInterview.Instance?.HasDiscovered(VendorFollowupInterview.StatementId) == true)
            input.discoveredStatementIds.Add(VendorFollowupInterview.StatementId);
        if (EvidenceStateManager.Instance != null)
            foreach (var r in EvidenceStateManager.Instance.AllRecords)
                if (r?.definition != null && r.status != EvidenceStatus.NotFound) input.observedEvidenceIds.Add(r.definition.evidenceId);
        foreach (var c in Rules.checkpoints)
        {
            var answer = manager?.Records.FirstOrDefault(r => r.CheckpointId == c.id);
            input.checkpoints.Add(new DeductionCheckpointState { checkpointId = c.id, wasEligible = manager?.IsEligible(c.id) == true,
                responseId = answer?.ResponseId ?? "", responseOrder = answer?.Order ?? 0,
                eligibleSeconds = answer?.EligibleSeconds ?? 0, answeredSeconds = answer?.AnsweredSeconds ?? 0 });
        }
        if (RelationshipStateManager.Instance != null)
            foreach (var r in RelationshipStateManager.Instance.AllRecords)
                if (r?.definition != null && r.state == RelationshipState.Completed) input.completedRelationshipIds.Add(r.definition.relationshipId);
        var board = UnityEngine.Object.FindAnyObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        if (board != null) input.boardConnections.AddRange(board.GetAssessmentConnections());
        if (EvidenceRelationshipsReview.Instance != null)
        {
            input.relationshipReviewVersion = 1;
            input.relationshipResponses = EvidenceRelationshipsReview.Instance.Snapshot();
        }
        return input;
    }

    public static void CaptureConclusion(string conclusionId, string endingId)
    {
        if (!ActiveInScene || frozenSessionId == SessionLogger.Instance?.SessionId && frozen != null) return;
        frozenSessionId = SessionLogger.Instance?.SessionId;
        frozen = CaptureLive(conclusionId, endingId);
        frozen.questionRecords = EducationalSessionRecord.CaptureQuestions(frozen);
        frozen.questionRecordVersion = 1;
        SessionLogger.Instance?.LogEvent(SessionEventType.DeductionAssessmentSnapshot, conclusionId,
            new Dictionary<string,string> { { "assessment", JsonUtility.ToJson(frozen) } });
        SessionLogger.Instance?.Flush();
    }

    public static DeductionAssessmentResult FinalResult(bool completed)
    {
        // A new logger/session must never inherit the previous conclusion snapshot.
        var input = frozenSessionId == SessionLogger.Instance?.SessionId ? frozen : null;
        return Score(input, completed, Rules);
    }

    public static bool TriggerSatisfied(DeductionCheckpointRule c, DeductionAssessmentInput input, DeductionScoringDefinition config) => c.trigger switch
    {
        ReasoningTrigger.Form1Received => input.form1Received,
        ReasoningTrigger.InitialSceneObserved => input.form1Received && input.victimObserved && input.sceneSecurityAcknowledged
            && input.iocObservationIds.Contains("ioc-possible-weapon") && input.iocObservationIds.Contains("ioc-bloodstain-area"),
        ReasoningTrigger.FindingConfirmed => input.confirmedFindingIds.Contains(c.findingId),
        ReasoningTrigger.VendorStatementDiscovered => input.form1Received && input.discoveredStatementIds.Contains(config.supportedStatementId),
        _ => false
    };
    private static string Pair(string a, string b) => string.CompareOrdinal(a,b) <= 0 ? a + "|" + b : b + "|" + a;
    public static bool RelationshipSupported(DeductionRelationshipRule r, DeductionAssessmentInput input) =>
        r.findingIds.All(input.confirmedFindingIds.Contains)
        && (string.IsNullOrEmpty(r.statementId) || input.discoveredStatementIds.Contains(r.statementId))
        && (string.IsNullOrEmpty(r.evidenceId) || input.observedEvidenceIds.Contains(r.evidenceId))
        && (!r.requiresForm1 || input.form1Received);

    public static DeductionAssessmentResult Score(DeductionAssessmentInput input, bool completed, DeductionScoringDefinition config)
    {
        var result = new DeductionAssessmentResult { scoringRulesVersion = config.scoringRulesVersion, status = "incomplete",
            checkpointMaximum = config.checkpoints.Count * config.maximumCheckpointWeight,
            relationshipMaximum = config.relationships.Sum(r=>r.weight) };
        if (input == null) { result.status = completed ? "requires_review" : "incomplete"; result.reviewReasons.Add("Conclusion-time assessment snapshot missing."); return result; }
        if (input.checkpoints == null || input.completedRelationshipIds == null || input.boardConnections == null
            || input.confirmedFindingIds == null || input.discoveredStatementIds == null || input.observedEvidenceIds == null || input.iocObservationIds == null
            || input.checkpoints.Any(s=>s == null) || input.boardConnections.Any(p=>p == null))
        { result.status="requires_review"; result.reviewReasons.Add("Malformed assessment snapshot."); return result; }
        if (!(config.checkpointWeight >= 0 && config.checkpointWeight <= 100) || !(config.relationshipWeight >= 0 && config.relationshipWeight <= 100)
            || !(config.conclusionWeight >= 0 && config.conclusionWeight <= 100)
            || Mathf.Abs(config.checkpointWeight + config.relationshipWeight + config.conclusionWeight - 100) > .001f
            || result.checkpointMaximum <= 0 || result.relationshipMaximum <= 0 || config.relationships.Any(r=>r.weight <= 0 || float.IsNaN(r.weight) || float.IsInfinity(r.weight))
            || !(config.unsupportedRelationshipPenalty >= 0 && !float.IsInfinity(config.unsupportedRelationshipPenalty))
            || new[]{config.supportedConclusionCredit,config.prematureConclusionCredit,config.peterConclusionCredit,config.inconclusiveInsufficientCredit,config.inconclusiveSupportedCredit}.Any(v=>!(v >= 0 && v <= 1))
            || config.checkpoints.Any(c=>c.responses.Any(r=>r.weight < 1 || r.weight > config.maximumCheckpointWeight)))
        { result.status="requires_review"; result.reviewReasons.Add("Invalid scoring definition; weights or denominators are inconsistent."); return result; }
        if (input.scoringRulesVersion != config.scoringRulesVersion || input.contentVersion != config.contentVersion || !input.checkpointSystemReady)
            result.reviewReasons.Add("Checkpoint system/content version unavailable or inconsistent.");
        if (input.checkpoints.GroupBy(c=>c.checkpointId).Any(g=>g.Count()!=1)
            || input.checkpoints.Any(c=>!config.checkpoints.Any(d=>d.id==c.checkpointId))) result.reviewReasons.Add("Duplicate or unknown checkpoint state.");
        foreach (var c in config.checkpoints)
        {
            var state = input.checkpoints.Find(s=>s.checkpointId==c.id);
            bool ready = TriggerSatisfied(c,input,config);
            if (state == null) { result.reviewReasons.Add(c.id + ": checkpoint state missing."); continue; }
            if (ready != state.wasEligible) result.reviewReasons.Add(c.id + ": observed facts and recorded eligibility disagree.");
            if (string.IsNullOrEmpty(state.responseId))
            { if (state.wasEligible) result.eligibleUnansweredCount++; else result.undiscoveredCount++; continue; }
            var response = c.responses.Find(r=>r.id==state.responseId);
            if (!state.wasEligible || response == null || state.responseOrder <= 0 || state.answeredSeconds < state.eligibleSeconds)
            { result.reviewReasons.Add(c.id + ": invalid locked response record."); continue; }
            result.checkpointEarned += response.weight; result.answeredCount++;
        }
        if (input.checkpoints.Where(c=>!string.IsNullOrEmpty(c.responseId)).GroupBy(c=>c.responseOrder).Any(g=>g.Count()>1))
            result.reviewReasons.Add("Duplicate chronological response order.");
        var completedIds = new HashSet<string>(input.completedRelationshipIds);
        if (input.relationshipReviewVersion != 0)
        {
            completedIds.Clear();
            if (input.relationshipReviewVersion != 1 || input.relationshipResponses == null
                || input.relationshipResponses.Any(a => a == null)
                || input.relationshipResponses.GroupBy(a => a.relationshipId).Any(g => g.Count() != 1)
                || input.relationshipResponses.GroupBy(a => a.responseOrder).Any(g => g.Count() != 1))
            { result.status = "requires_review"; result.reviewReasons.Add("Malformed relationship review."); return result; }
            foreach (var answer in input.relationshipResponses)
            {
                var rule = config.relationships.Find(r => r.id == answer.relationshipId);
                if (rule == null || !rule.interpretations.Any(c => c.id == answer.responseId)
                    || answer.responseOrder <= 0 || answer.answeredSeconds < 0 || double.IsNaN(answer.answeredSeconds)
                    || double.IsInfinity(answer.answeredSeconds) || !RelationshipSupported(rule,input))
                { result.reviewReasons.Add("Invalid locked relationship response."); continue; }
                if (answer.responseId == rule.correctInterpretationId) completedIds.Add(rule.id);
                else result.unsupportedRelationships++;
            }
            if (input.atConclusion && (input.relationshipResponses.Count == 0 || config.relationships.Any(r =>
                RelationshipSupported(r,input) && !input.relationshipResponses.Any(a => a.relationshipId == r.id))))
                result.reviewReasons.Add("Available relationship review is incomplete.");
        }
        var supportedIds = new HashSet<string>();
        foreach (var r in config.relationships)
            if (completedIds.Contains(r.id))
            {
                if (RelationshipSupported(r,input)) { result.relationshipEarned += r.weight; supportedIds.Add(r.id); }
                else result.unsupportedRelationships++;
            }
        result.unsupportedRelationships += completedIds.Count(id=>!config.relationships.Any(r=>r.id==id));
        var authoredPairs = new HashSet<string>(config.relationships.Select(r=>Pair(r.sourceId,r.targetId)));
        if (input.relationshipReviewVersion == 0)
            result.unsupportedRelationships += input.boardConnections.Select(p=>Pair(p.sourceId,p.targetId)).Distinct().Count(p=>!authoredPairs.Contains(p));
        result.reasoningCheckpoints = result.checkpointMaximum > 0 ? (float)result.checkpointEarned/result.checkpointMaximum : 0;
        result.deductionRelationships = result.relationshipMaximum > 0 ? Mathf.Max(0,
            result.relationshipEarned - result.unsupportedRelationships * config.unsupportedRelationshipPenalty)/result.relationshipMaximum : 0;
        bool fullChain = config.supportedFindingIds.All(input.confirmedFindingIds.Contains)
            && config.supportedRelationshipIds.All(supportedIds.Contains) && input.discoveredStatementIds.Contains(config.supportedStatementId);
        if (input.selectedConclusionId == config.supportedConclusionId && input.resolvedEndingId == config.supportedEndingId)
        { if (fullChain) result.finalConclusion = config.supportedConclusionCredit; else result.reviewReasons.Add("Supported ending lacks the authored evidence chain."); }
        else if (input.selectedConclusionId == config.supportedConclusionId && input.resolvedEndingId == config.prematureEndingId)
        { if (!fullChain) result.finalConclusion = config.prematureConclusionCredit; else result.reviewReasons.Add("Premature ending conflicts with the supported evidence chain."); }
        else if (input.selectedConclusionId == config.peterConclusionId && input.resolvedEndingId == config.peterEndingId) result.finalConclusion = config.peterConclusionCredit;
        else if (input.selectedConclusionId == config.inconclusiveConclusionId && input.resolvedEndingId == config.inconclusiveEndingId)
            result.finalConclusion = fullChain ? config.inconclusiveSupportedCredit : config.inconclusiveInsufficientCredit;
        else if (input.atConclusion) result.reviewReasons.Add("Unknown conclusion/ending pair.");
        result.reasoningComponent = result.reasoningCheckpoints * config.checkpointWeight;
        result.relationshipComponent = result.deductionRelationships * config.relationshipWeight;
        result.conclusionComponent = result.finalConclusion * config.conclusionWeight;
        result.hasFinalScore = completed && input.atConclusion && result.reviewReasons.Count == 0;
        result.status = result.reviewReasons.Count > 0 ? "requires_review" : result.hasFinalScore ? "complete" : "incomplete";
        if (result.hasFinalScore) result.totalScore = Mathf.Clamp(result.reasoningComponent + result.relationshipComponent + result.conclusionComponent, 0, 100);
        return result;
    }
}
