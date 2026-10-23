// Assets/_Project/Scripts/Assessment/AssessmentSessionUploadDto.cs
using System;
using System.Collections.Generic;

/// <summary>
/// Versioned wire contract between the Unity client and the Node.js assessment-ingest
/// endpoint (POST /api/v1/assessment-sessions). Deliberately a plain, JsonUtility-
/// serializable POCO tree - no MonoBehaviours, no ScriptableObject references, no
/// UnityEngine types beyond what JsonUtility already handles (this file has no "using
/// UnityEngine" at all). Never edit an existing field's meaning or type once
/// payloadVersion has shipped; add a new payloadVersion and a new field instead - the
/// server is expected to branch on payloadVersion, the same "explicit, append-only,
/// never reorder" discipline this project already applies to EvidenceStatus/ToolType/
/// SessionEventType.
///
/// JsonUtility, not Newtonsoft, on purpose - matches the project's one existing
/// convention (SessionEvent/LocalJsonLogWriter) rather than introducing a second JSON
/// library for one new subsystem. JsonUtility's two real limitations
/// (Dictionary&lt;,&gt; unsupported; a null reference-typed field throws rather than
/// serializing as JSON null) are designed around the same way SessionEvent already
/// does: every "map" is a List of a small struct (see UploadPayloadEntry, reusing
/// SessionEvent's own PayloadEntry shape), and every "optional section" is always a
/// non-null, default-valued instance guarded by an explicit has*/count field rather
/// than relying on the serializer's null handling - "empty struct + a flag", not "null".
///
/// SECURITY BOUNDARY, load-bearing: studentId/classId below are INFORMATIONAL ONLY -
/// they exist so the local pending-upload queue and its status UI can show "queued for
/// <name>" before a session ever reaches the server. The server MUST NEVER treat these
/// fields as authoritative identity. It re-derives the real student from the bearer
/// session token (see AssessmentUploadService's Authorization header) on every request
/// and rejects the submission if the token's identity disagrees with these fields. A
/// modified or rooted client could otherwise claim to be any student by editing this
/// JSON - trusting a client-supplied identity string is exactly the class of bug this
/// boundary exists to prevent.
///
/// Historical DTO shape is retained for old versions and local queue metadata.
/// Current 1.7.0 requests use EducationalSessionRecord.Serialize to omit legacy
/// analytics. Their frozen assessment contains the question/answer/weight snapshot
/// needed for the post-session educational record; the server verifies numerical
/// metadata against its existing versioned scorer rather than trusting client totals.
/// </summary>
[Serializable]
public class AssessmentSessionUploadDto
{
    public int payloadVersion = 1;

    public string sessionId;

    // Informational only - see the class-level SECURITY BOUNDARY note. Never trusted by the server as identity.
    public string studentId;
    public string classId;

    // Informational only, like studentId: true for a "Continue Without Code" session. The
    // server takes the authoritative flag from the upload token (vicsi-dashboard 019),
    // never from this field.
    public bool isTestSession;

    public string scenarioId;
    public string scenarioVersion;
    public string scoringRulesVersion;

    /// <summary>Which server answer key (ScenarioDefinition.groundTruthVersion) this session is scored against. Additive: the ingest RPC already reads p_payload->>'groundTruthVersion'; absent, the server falls back to its latest registered key.</summary>
    public string groundTruthVersion;
    public string clientVersion;

    public string startedAtUtc;
    public string completedAtUtc;
    public double durationSeconds;

    public string selectedConclusionId;
    public string resolvedEndingId;

    public bool hasClientReportedScores;
    public ClientReportedScoresDto clientReportedScores = new();

    public List<EvidenceResultDto> evidenceResults = new();
    public List<ReportLineResultDto> reportLineResults = new();
    public List<ProceduralViolationDto> proceduralViolations = new();
    public List<RelationshipResultDto> relationshipResults = new();
    public List<HypothesisResultDto> hypothesisResults = new();

    // Additive 1.7.0 data. Essential assessment records survive a disabled/capped
    // raw log. Server recomputes from frozen facts/response IDs, never these scores.
    public List<UploadSessionEventDto> deductionAssessmentEvents = new();
    public bool hasDeductionAssessment;
    public DeductionAssessmentResult deductionAssessment = new();

    // Purely additive (payloadVersion unchanged - no existing field's meaning or type
    // changes, see this class's own header rule on when a version bump is actually
    // required). Stable ids only - never a display name, matching every other id field
    // in this DTO (selectedConclusionId/resolvedEndingId/relationshipId/evidenceId/...).
    // Analytics/context only: the server's own verified scoring pipeline must never treat
    // these as authoritative (see FindingStateManager.OnFindingConfirmed/
    // InsightStateManager.OnInsightUnlocked's own "monotonic, fires once" guarantee for
    // why a session can never contain a duplicate id here).
    public List<string> confirmedFindingIds = new();
    public List<string> unlockedInsightIds = new();

    // Purely additive, same "flag + always-non-null section" pattern as
    // clientReportedScores. Analytics only - the server derives the authoritative
    // scene-photography category from the ScenePhoto* events in orderedEvents.
    public bool hasScenePhotography;
    public ScenePhotographyAssessmentResult scenePhotography = new();

    // Purely additive, same pattern as scenePhotography: one entry per required
    // measurement (MeasurementStateManager's authoritative records). Analytics only -
    // the server derives the measurement category from the Measurement* events in
    // orderedEvents, against its own expected distances (scoringRulesVersion 1.2.0+).
    public bool hasMeasurement;
    public MeasurementAssessmentResult measurement = new();

    public bool includesRawEventLog;
    public List<UploadSessionEventDto> orderedEvents = new();
}

/// <summary>
/// Cheap, honest counts Unity can compute from live manager state with no ground-truth
/// dependency - NOT the offline EvidenceScorer's recall/precision/reasoning-accuracy
/// metrics, which require a ground-truth export that never ships to a device build (see
/// AssessmentUploadService's own class comment). Only populated (hasClientReportedScores
/// = true) once a caller actually has these numbers; left zeroed and flagged false
/// otherwise, matching student_session_summary's own "null over fabricated zero"
/// precedent in the dashboard schema, projected into this JsonUtility-safe shape as a
/// flag instead of a real null.
/// </summary>
[Serializable]
public class ClientReportedScoresDto
{
    public int relationshipsCompletedCorrect;
    public int relationshipsCompletedIncorrect;
    public int objectivesCompleted;
    public int objectivesFailed;
    public int proceduralViolationCount;
}

[Serializable]
public class EvidenceResultDto
{
    public string evidenceId;
    public string finalStatus;            // EvidenceStatus.ToString()
    public bool swabbingDone;
    public bool fingerprintingDone;
    public bool isFlipped;
    public string fingerprintLabStatus;   // FingerprintLabStatus.ToString()
    public List<string> fingerprintMatchIdentities = new();
    public int tentNumber;                // 0 = none (matches EvidenceRecord.tentNumber's null)
    public string tentLetter;             // "" = none
}

[Serializable]
public class ReportLineResultDto
{
    public string evidenceId;
    public string lineId;
}

/// <summary>
/// One procedural refusal - EvidenceTransitionBlocked, NonEvidenceMarked,
/// MarkerReclaimBlocked, or EvidenceMarkerRejected from SessionEventType. A blocked
/// attempt is itself diagnostic (see EvidenceStateManager's own comments on why these
/// are logged rather than swallowed); this section exists so a server-side or
/// instructor-facing view doesn't have to re-derive "how many procedural mistakes" by
/// replaying the entire raw event stream when only the count/nature of violations is
/// needed.
/// </summary>
[Serializable]
public class ProceduralViolationDto
{
    public string eventType;
    public string targetId;
    public long timestampMs;
    public string detail;
}

[Serializable]
public class RelationshipResultDto
{
    public string selectedInterpretationId;
    public int interpretationOrder;
    public bool interpretationLocked;
    public string relationshipId;
    public string sourceId;
    public string targetId;
    public string relationshipType;
    public string state;          // RelationshipState.ToString()
    public bool wasEverSelected;
}

[Serializable]
public class HypothesisResultDto
{
    public string checkpointId;
    public string selectedOptionId;
    public string reasoningOptionId;   // "" if the checkpoint had no reasoning step
}

/// <summary>
/// One SessionEvent, re-shaped for upload. Deliberately its own type rather than
/// reusing SessionEvent directly: SessionEvent.sessionId is redundant at this nesting
/// level (the parent DTO already carries it once), and duplicating it per-event would
/// bloat exactly the payload this project's own "payload and event-count limits" rule
/// (Phase 6) exists to bound.
/// </summary>
[Serializable]
public class UploadSessionEventDto
{
    public int sequenceNumber;
    public long timestampMs;
    public string eventType;
    public string targetId;
    public List<PayloadEntry> payload = new();
}
