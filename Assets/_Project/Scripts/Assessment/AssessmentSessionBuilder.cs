// Assets/_Project/Scripts/Assessment/AssessmentSessionBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Builds one AssessmentSessionUploadDto for the just-completed session. Called exactly
/// once, synchronously, at the moment a session ends. Current 1.7.0 sessions retain
/// only completed procedure events and verification records; older versions keep
/// the legacy manager-based result sections below, while EvidenceStateManager/
/// RelationshipStateManager/ObjectiveStateManager are all still alive in the scene, NOT
/// as a standalone offline tool. That timing is load-bearing: several EvidenceRecord
/// fields in that legacy DTO still rely on live manager state (for example,
/// isFlipped and fingerprintLabStatus). Successful swabbing and fingerprint
/// processing now also emit completion events for the current procedure timeline.
/// Everything that IS fully captured in the log (report lines,
/// procedural violations, hypothesis answers, conclusion/ending selection) is derived
/// by replaying it instead, the same source-of-truth choice Tools/EvidenceScorer
/// already makes for the same reason.
///
/// Reads only public state on EvidenceStateManager/RelationshipStateManager/
/// ObjectiveStateManager/EvidenceProp - adds no new public surface to any of them,
/// per this feature's "extend through a separate layer" ground rule.
/// </summary>
public static class AssessmentSessionBuilder
{
    public static AssessmentSessionUploadDto Build(
        string sessionLogFilePath,
        AssessmentUploadConfiguration config,
        ScenarioDefinition scenario,
        string selectedConclusionId,
        string resolvedEndingId)
    {
        var events = SessionLogReader.ReadEvents(sessionLogFilePath);

        var dto = new AssessmentSessionUploadDto
        {
            payloadVersion = 1,
            sessionId = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : (events.Count > 0 ? events[0].sessionId : string.Empty),
            studentId = PairingIdentityService.Instance != null ? PairingIdentityService.Instance.StudentDisplayName : string.Empty,
            classId = PairingIdentityService.Instance != null ? PairingIdentityService.Instance.ClassDisplayName : string.Empty,
            scenarioId = scenario != null ? scenario.scenarioId : string.Empty,
            scenarioVersion = scenario != null ? scenario.scenarioVersion : string.Empty,
            scoringRulesVersion = scenario != null ? scenario.scoringRulesVersion : string.Empty,
            groundTruthVersion = scenario != null ? scenario.groundTruthVersion : string.Empty,
            clientVersion = Application.version,
            selectedConclusionId = selectedConclusionId ?? string.Empty,
            resolvedEndingId = resolvedEndingId ?? string.Empty,
            includesRawEventLog = config != null && config.uploadRawEventLogEnabled
        };

        ApplyTimestampsAndDuration(dto, events);
        if (FinalDeductionAssessment.UsesCurrentRules(dto.scoringRulesVersion))
        {
            BuildDeductionAssessment(dto, events);
            EducationalSessionRecord.BuildMinimalUpload(dto, events);
            return dto;
        }
        BuildEvidenceResults(dto);
        BuildReportLineResults(dto, events);
        BuildProceduralViolations(dto, events);
        BuildRelationshipResults(dto);
        BuildFindingAndInsightResults(dto);
        BuildScenePhotographyResult(dto);
        BuildMeasurementResult(dto);
        BuildHypothesisResults(dto, events);
        BuildClientReportedScores(dto);
        BuildDeductionAssessment(dto, events);

        if (dto.includesRawEventLog)
        {
            int maxEvents = config != null ? config.maxEventsPerRequest : events.Count;
            int count = Mathf.Min(events.Count, maxEvents);
            for (int i = 0; i < count; i++)
            {
                var e = events[i];
                dto.orderedEvents.Add(new UploadSessionEventDto
                {
                    sequenceNumber = e.sequenceNumber,
                    timestampMs = e.timestampMs,
                    eventType = e.eventType,
                    targetId = e.targetId,
                    payload = e.payload ?? new List<PayloadEntry>()
                });
            }
        }

        return dto;
    }

    private static void BuildDeductionAssessment(AssessmentSessionUploadDto dto, List<SessionEvent> events)
    {
        if (!FinalDeductionAssessment.UsesCurrentRules(dto.scoringRulesVersion)) return;
        DeductionAssessmentInput snapshot = null;
        var essential = new HashSet<string> { FinalDeductionAssessment.SnapshotEvent, "ReasoningCheckpointSubmitted", "ReasoningCheckpointEligible",
            "ConclusionSelected", "EndingSelected", "SessionEnded", "InvestigationCompleted",
            "BodyDocumentationRecorded", "BodyPreparedForRemoval", "BodyRemoved", "FingerprintIdentified", "RelationshipInterpretationSubmitted",
            "EvidenceDeposited", "CustodyPassCompleted" };
        foreach (var e in events)
        {
            if (!essential.Contains(e.eventType)) continue;
            dto.deductionAssessmentEvents.Add(new UploadSessionEventDto { sequenceNumber=e.sequenceNumber, timestampMs=e.timestampMs,
                eventType=e.eventType, targetId=e.targetId, payload=e.payload ?? new List<PayloadEntry>() });
            if (e.eventType == FinalDeductionAssessment.SnapshotEvent && snapshot == null)
            {
                try { snapshot = JsonUtility.FromJson<DeductionAssessmentInput>(SessionLogReader.PayloadValue(e,"assessment")); }
                catch (Exception) { /* Honest requires_review result below, never a fabricated grade. */ }
            }
        }
        bool complete = events.Exists(e=>e.eventType=="SessionEnded") && snapshot != null
            && (!snapshot.requiresSceneRelease || events.Exists(e=>e.eventType=="InvestigationCompleted"));
        dto.hasDeductionAssessment = true;
        dto.deductionAssessment = FinalDeductionAssessment.Score(snapshot, complete, FinalDeductionAssessment.Rules);
    }

    private static void ApplyTimestampsAndDuration(AssessmentSessionUploadDto dto, List<SessionEvent> events)
    {
        long endedAtMs = 0;
        foreach (var e in events)
        {
            if (e.eventType == "SessionStarted")
            {
                dto.startedAtUtc = SessionLogReader.PayloadValue(e, "startedAtUtc") ?? string.Empty;
            }
            else if (e.eventType == "SessionEnded")
            {
                dto.completedAtUtc = SessionLogReader.PayloadValue(e, "endedAtUtc") ?? string.Empty;
                endedAtMs = e.timestampMs;
            }
        }

        if (endedAtMs <= 0 && events.Count > 0)
        {
            // Defensive: builder was asked to run before SessionEnded was ever logged
            // (should not happen via the normal EndingManager flow, but never fabricate
            // a duration from wall-clock "now" when the log's own last event already
            // gives an honest session-relative figure).
            endedAtMs = events[^1].timestampMs;
        }

        dto.durationSeconds = endedAtMs / 1000.0;
    }

    private static void BuildEvidenceResults(AssessmentSessionUploadDto dto)
    {
        if (EvidenceStateManager.Instance == null)
        {
            return;
        }

        foreach (var prop in EvidenceProp.All)
        {
            if (prop == null || string.IsNullOrEmpty(prop.evidenceId))
            {
                continue;
            }

            var record = EvidenceStateManager.Instance.GetRecord(prop.evidenceId);
            if (record == null)
            {
                continue;
            }

            dto.evidenceResults.Add(new EvidenceResultDto
            {
                evidenceId = prop.evidenceId,
                finalStatus = record.status.ToString(),
                swabbingDone = record.swabbingDone,
                fingerprintingDone = record.fingerprintingDone,
                isFlipped = record.isFlipped,
                fingerprintLabStatus = record.fingerprintLabStatus.ToString(),
                fingerprintMatchIdentities = new List<string>(record.fingerprintLabMatchIdentities),
                tentNumber = record.tentNumber ?? 0,
                tentLetter = record.tentLetter ?? string.Empty
            });
        }
    }

    private static void BuildReportLineResults(AssessmentSessionUploadDto dto, List<SessionEvent> events)
    {
        foreach (var e in events)
        {
            if (e.eventType != "ReportLinesSelected")
            {
                continue;
            }

            string lineIdsRaw = SessionLogReader.PayloadValue(e, "lineIds");
            if (string.IsNullOrEmpty(lineIdsRaw) || string.IsNullOrEmpty(e.targetId))
            {
                continue;
            }

            foreach (var lineId in lineIdsRaw.Split(','))
            {
                if (string.IsNullOrEmpty(lineId)) continue;
                dto.reportLineResults.Add(new ReportLineResultDto { evidenceId = e.targetId, lineId = lineId });
            }
        }
    }

    private static readonly HashSet<string> ProceduralViolationEventTypes = new()
    {
        "EvidenceTransitionBlocked", "NonEvidenceMarked", "MarkerReclaimBlocked", "EvidenceMarkerRejected"
    };

    private static void BuildProceduralViolations(AssessmentSessionUploadDto dto, List<SessionEvent> events)
    {
        foreach (var e in events)
        {
            if (!ProceduralViolationEventTypes.Contains(e.eventType))
            {
                continue;
            }

            dto.proceduralViolations.Add(new ProceduralViolationDto
            {
                eventType = e.eventType,
                targetId = e.targetId ?? string.Empty,
                timestampMs = e.timestampMs,
                detail = FormatPayload(e.payload)
            });
        }
    }

    private static string FormatPayload(List<PayloadEntry> payload)
    {
        if (payload == null || payload.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < payload.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(payload[i].key).Append('=').Append(payload[i].value);
        }
        return sb.ToString();
    }

    private static void BuildRelationshipResults(AssessmentSessionUploadDto dto)
    {
        if (RelationshipStateManager.Instance == null)
        {
            return;
        }

        foreach (var record in RelationshipStateManager.Instance.AllRecords)
        {
            if (record?.definition == null) continue;

            dto.relationshipResults.Add(new RelationshipResultDto
            {
                selectedInterpretationId = EvidenceRelationshipsReview.Instance?.Responses.FirstOrDefault(a => a.relationshipId == record.definition.relationshipId)?.responseId ?? "",
                interpretationOrder = EvidenceRelationshipsReview.Instance?.Responses.FirstOrDefault(a => a.relationshipId == record.definition.relationshipId)?.responseOrder ?? 0,
                interpretationLocked = EvidenceRelationshipsReview.Instance?.HasResponse(record.definition.relationshipId) == true,
                relationshipId = record.definition.relationshipId,
                sourceId = record.definition.sourceId,
                targetId = record.definition.targetId,
                relationshipType = record.definition.relationshipType,
                state = record.state.ToString(),
                wasEverSelected = record.wasEverSelected
            });
        }
    }

    /// <summary>
    /// Stable ids only, read from live manager state (same "live state, not raw event
    /// replay" choice BuildEvidenceResults/BuildRelationshipResults already make) - never
    /// gated behind includesRawEventLog, so this section is always present regardless of
    /// that optional flag. FindingStateManager.GetConfirmedFindings/InsightStateManager.
    /// GetUnlockedInsights are both already monotonic (see their own class comments), so
    /// there is no risk of listing the same id twice here.
    /// </summary>
    private static void BuildFindingAndInsightResults(AssessmentSessionUploadDto dto)
    {
        if (FindingStateManager.Instance != null)
        {
            dto.confirmedFindingIds.AddRange(FindingStateManager.Instance.GetConfirmedFindings());
        }

        if (InsightStateManager.Instance != null)
        {
            foreach (var insight in InsightStateManager.Instance.GetUnlockedInsights())
            {
                if (insight != null && !string.IsNullOrEmpty(insight.insightId))
                {
                    dto.unlockedInsightIds.Add(insight.insightId);
                }
            }
        }
    }

    // Left as the default (zeroed, flag false) when the scene has no scene photography
    // route, so a scenario without one is never reported as "0% scene photography".
    private static void BuildScenePhotographyResult(AssessmentSessionUploadDto dto)
    {
        var manager = ScenePhotographyManager.Instance;
        if (manager == null || manager.Points.Count == 0)
        {
            return;
        }

        dto.hasScenePhotography = true;
        dto.scenePhotography = manager.BuildAssessment();
    }

    // Left as the default (empty, flag false) when the scene has no measurement
    // requirements, so a scenario without them is never reported as "0% measurement".
    private static void BuildMeasurementResult(AssessmentSessionUploadDto dto)
    {
        var manager = MeasurementStateManager.Instance;
        if (manager == null)
        {
            return;
        }

        var result = manager.BuildAssessment();
        if (result.requirementCount == 0)
        {
            return;
        }

        dto.hasMeasurement = true;
        dto.measurement = result;
    }

    private static void BuildHypothesisResults(AssessmentSessionUploadDto dto, List<SessionEvent> events)
    {
        // Last submission per checkpoint wins - checkpoints fire at most once in
        // practice (HypothesisCheckpointManager.firedCheckpointIds), but "last wins"
        // costs nothing and stays correct if that ever changes.
        var byCheckpoint = new Dictionary<string, HypothesisResultDto>();

        foreach (var e in events)
        {
            if (e.eventType != "HypothesisSubmitted" || string.IsNullOrEmpty(e.targetId))
            {
                continue;
            }

            byCheckpoint[e.targetId] = new HypothesisResultDto
            {
                checkpointId = e.targetId,
                selectedOptionId = SessionLogReader.PayloadValue(e, "selectedOptionId") ?? string.Empty,
                reasoningOptionId = SessionLogReader.PayloadValue(e, "reasoningOptionId") ?? string.Empty
            };
        }

        dto.hypothesisResults.AddRange(byCheckpoint.Values);
    }

    private static void BuildClientReportedScores(AssessmentSessionUploadDto dto)
    {
        var scores = new ClientReportedScoresDto();
        bool anySource = false;

        if (RelationshipStateManager.Instance != null)
        {
            anySource = true;
            foreach (var record in RelationshipStateManager.Instance.AllRecords)
            {
                if (record?.definition == null || record.state != RelationshipState.Completed) continue;
                if (record.definition.isCorrect) scores.relationshipsCompletedCorrect++;
                else scores.relationshipsCompletedIncorrect++;
            }
        }

        if (ObjectiveStateManager.Instance != null)
        {
            anySource = true;
            foreach (var record in ObjectiveStateManager.Instance.AllRecords)
            {
                if (record == null || (record.definition != null && record.definition.excludeFromScoring)) continue;
                if (record.state == ObjectiveState.Completed) scores.objectivesCompleted++;
                else if (record.state == ObjectiveState.Failed) scores.objectivesFailed++;
            }
        }

        scores.proceduralViolationCount = dto.proceduralViolations.Count;

        dto.clientReportedScores = scores;
        dto.hasClientReportedScores = anySource;
    }
}
