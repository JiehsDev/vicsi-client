using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public class DeductionAnswerRecord
{
    public string id, text, endingId;
    public float weight;
}
[Serializable] public class DeductionQuestionRecord
{
    public string id, category, dimension, question, selectedAnswerId;
    public bool wasAvailable;
    public double answeredSeconds = -1;
    public float selectedAnswerWeight, maximumWeight;
    public List<DeductionAnswerRecord> options = new();
}
[Serializable] public class ProcedureTimelineEntry { public string id, description; }
[Serializable] public class ProcedureTimelineDefinition
{
    public int version;
    public List<ProcedureTimelineEntry> events = new(), statuses = new();
}

/// <summary>Captures presentation from the actual game at submission. Numerical values
/// are resolved by the existing scorer and versioned rules; this is not another scorer.</summary>
public static class EducationalSessionRecord
{
    [Serializable] sealed class MinimalUpload
    {
        public int payloadVersion;
        public string sessionId, studentId, classId, scenarioId, scenarioVersion, scoringRulesVersion, groundTruthVersion, clientVersion;
        public bool isTestSession, includesRawEventLog, hasDeductionAssessment;
        public string startedAtUtc, completedAtUtc, selectedConclusionId, resolvedEndingId;
        public double durationSeconds;
        public DeductionAssessmentResult deductionAssessment;
        public List<UploadSessionEventDto> orderedEvents;
    }
    public static string Serialize(AssessmentSessionUploadDto dto,bool pretty=false)
    {
        if(!FinalDeductionAssessment.UsesCurrentRules(dto.scoringRulesVersion))return JsonUtility.ToJson(dto,pretty);
        return JsonUtility.ToJson(new MinimalUpload {payloadVersion=dto.payloadVersion,sessionId=dto.sessionId,studentId=dto.studentId,classId=dto.classId,
            scenarioId=dto.scenarioId,scenarioVersion=dto.scenarioVersion,scoringRulesVersion=dto.scoringRulesVersion,groundTruthVersion=dto.groundTruthVersion,
            clientVersion=dto.clientVersion,isTestSession=dto.isTestSession,includesRawEventLog=false,hasDeductionAssessment=dto.hasDeductionAssessment,
            startedAtUtc=dto.startedAtUtc,completedAtUtc=dto.completedAtUtc,durationSeconds=dto.durationSeconds,
            selectedConclusionId=dto.selectedConclusionId,resolvedEndingId=dto.resolvedEndingId,deductionAssessment=dto.deductionAssessment,orderedEvents=dto.orderedEvents},pretty);
    }
    public static ProcedureTimelineDefinition Timeline => JsonUtility.FromJson<ProcedureTimelineDefinition>(Resources.Load<TextAsset>("ProcedureTimeline_1").text);
    public static readonly HashSet<string> VerificationEvents = new()
    {
        "SessionStarted", "SessionEnded", "DeductionAssessmentSnapshot", "ReasoningCheckpointEligible",
        "ReasoningCheckpointSubmitted", "RelationshipInterpretationSubmitted", "ConclusionSelected", "EndingSelected", "ReportLinesSelected"
    };
    public static bool IsProcedure(SessionEvent e, ProcedureTimelineDefinition definition)
    {
        if (e.eventType == "EvidenceStatusChanged")
            return definition.statuses.Any(s => s.id == SessionLogReader.PayloadValue(e, "status"));
        return definition.events.Any(s => s.id == e.eventType);
    }
    public static List<DeductionQuestionRecord> CaptureQuestions(DeductionAssessmentInput input)
    {
        var records = new List<DeductionQuestionRecord>();
        var rules = FinalDeductionAssessment.Rules;
        var content = ReasoningCheckpointManager.Instance?.Definitions;
        if (content == null) return records; // missing content is never invented
        foreach (var c in content.checkpoints)
        {
            var state = input.checkpoints.Find(s => s.checkpointId == c.id);
            var rule = rules.checkpoints.Find(r => r.id == c.id);
            if (rule == null) continue;
            var q = new DeductionQuestionRecord { id=c.id, category="Reasoning", dimension=c.category,
                question=(string.IsNullOrEmpty(c.contextCatalogId)?"":GameDialogue.Text(c.contextCatalogId)+"\n\n")+c.prompt,
                wasAvailable=state?.wasEligible == true, selectedAnswerId=state?.responseId ?? "", maximumWeight=rules.maximumCheckpointWeight,
                answeredSeconds=string.IsNullOrEmpty(state?.responseId) ? -1 : state.answeredSeconds };
            q.options = c.responses.Select(a => new DeductionAnswerRecord { id=a.id, text=a.text,
                weight=rule.responses.Find(r => r.id == a.id).weight }).ToList();
            q.selectedAnswerWeight=q.options.Find(a=>a.id==q.selectedAnswerId)?.weight ?? 0;
            records.Add(q);
        }
        foreach (var r in rules.relationships)
        {
            var answer=input.relationshipResponses.Find(a=>a.relationshipId==r.id);
            var q=new DeductionQuestionRecord { id=r.id, category="Evidence Relationships", dimension="", question=r.reviewPrompt,
                wasAvailable=FinalDeductionAssessment.RelationshipSupported(r,input), selectedAnswerId=answer?.responseId ?? "",
                maximumWeight=r.weight, answeredSeconds=answer?.answeredSeconds ?? -1 };
            // Exact equivalent of earned weights minus the existing unsupported penalty.
            q.options=r.interpretations.Select(a=>new DeductionAnswerRecord { id=a.id,text=a.text,
                weight=a.id==r.correctInterpretationId ? r.weight : -rules.unsupportedRelationshipPenalty }).ToList();
            q.selectedAnswerWeight=q.options.Find(a=>a.id==q.selectedAnswerId)?.weight ?? 0;records.Add(q);
        }
        var owner=EndingManager.Instance;
        if (owner != null)
        {
            var q=new DeductionQuestionRecord { id="FINAL-CONCLUSION",category="Final Conclusion",dimension="",question="Submit your conclusion:",
                wasAvailable=true,selectedAnswerId=input.selectedConclusionId,maximumWeight=1,
                answeredSeconds=SessionLogger.Instance?.ElapsedSeconds ?? 0 };
            foreach(var option in owner.GetSelectableCaseConclusions())
            {
                var trial=JsonUtility.FromJson<DeductionAssessmentInput>(JsonUtility.ToJson(input));
                trial.selectedConclusionId=option.conclusionId;trial.resolvedEndingId=owner.ResolveEnding(option.conclusionId).Ending?.endingId ?? "";
                var score=FinalDeductionAssessment.Score(trial,false,rules);
                q.options.Add(new DeductionAnswerRecord { id=option.conclusionId,
                    text=option.displayName+(string.IsNullOrEmpty(option.description)?"":"\n"+option.description),
                    endingId=trial.resolvedEndingId,weight=score.finalConclusion });
            }
            q.selectedAnswerWeight=q.options.Find(a=>a.id==q.selectedAnswerId)?.weight ?? 0;records.Add(q);
        }
        return records;
    }
    public static void BuildMinimalUpload(AssessmentSessionUploadDto dto, List<SessionEvent> events)
    {
        var definition=Timeline;
        dto.includesRawEventLog=false;
        // Original sequence numbers and logger milliseconds survive filtering; no time rebasing,
        // truncation, re-ordering, or duplicated essential-event section.
        dto.orderedEvents=events.Where(e=>VerificationEvents.Contains(e.eventType)||IsProcedure(e,definition))
            .Select(e=>new UploadSessionEventDto {sequenceNumber=e.sequenceNumber,timestampMs=e.timestampMs,
                eventType=e.eventType,targetId=e.targetId,payload=e.payload ?? new List<PayloadEntry>()}).ToList();
        dto.deductionAssessmentEvents.Clear();
    }
}
