using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ScenarioPhase
{
    IOCTurnover = 0,
    IOCAssessment = 1,
    IOCSOCODecision = 2,
    SOCORequested = 3,
    SOCOTurnover = 4,
    SOCOInvestigation = 5,
    InvestigationComplete = 6,
    // Append-only: InvestigationComplete keeps its historical serialized value.
    SOCOCloseout = 7,
    EvidenceInventoryReview = 8,
    FinalSceneSurvey = 9,
    IOCCompletionReport = 10,
    SceneRelease = 11
}

public enum ScenarioPlayerRole { IOC, SOCO }

/// <summary>
/// Authoritative session-local procedural progression in the existing crime scene.
/// The FR controller registers in early Awake; scenes without it retain legacy ungated
/// behaviour. New sessions reload the scene, resetting this owner and all investigation
/// managers together. No partial reset of a live investigation is exposed.
/// Future FR response can be inserted before IOCTurnover without another role manager.
/// </summary>
public static class SceneTurnover
{
    private static FirstResponderTurnover controller;
    private static IOCAssessmentDefinition assessment;
    private static SceneCordon assessmentCordon;
    private static readonly HashSet<string> observations = new();
    private static readonly HashSet<string> assistanceDecisions = new();
    public static bool SOCOAssistanceRequested => IsGated && Phase >= ScenarioPhase.SOCORequested;

    public static bool DecideSOCOAssistance(bool request)
    {
        if (!IsGated || Phase != ScenarioPhase.IOCSOCODecision || !IOCAssessmentCompleted) return false;
        string choice = request ? "request-soco" : "continue-without-soco";
        if (assistanceDecisions.Add(choice))
            SessionLogger.Instance?.LogEvent(SessionEventType.SOCOAssistanceDecision, choice,
                new Dictionary<string, string> { { "appropriate", request ? "true" : "false" } });
        return request && TryAdvance(ScenarioPhase.SOCORequested);
    }
    public static IReadOnlyCollection<string> IOCObservations => observations;
    public static event Action<string> OnIOCObservationRecorded;
    public static bool IOCAssessmentCompleted => IsGated && Phase >= ScenarioPhase.IOCSOCODecision;
    public static bool SceneSecurityAcknowledged => assessment != null && assessmentCordon != null
        && assessmentCordon.CordonVisible && !assessmentCordon.EntranceIsOpen
        && assessment.observations.Any(o => o.kind == IOCObservationKind.SceneSurvey && observations.Contains(o.observationId));
    public static bool VictimObserved => assessment != null && assessment.observations.Any(o =>
        o.kind == IOCObservationKind.Victim && observations.Contains(o.observationId));
    public static int PossibleEvidenceObserved => assessment == null ? 0 : assessment.observations.Count(o =>
        o.kind == IOCObservationKind.PossibleEvidence && observations.Contains(o.observationId));
    public static bool AssessmentReady => Form1Received && SceneSecurityAcknowledged && VictimObserved
        && PossibleEvidenceObserved >= assessment.minimumPossibleEvidence;

    internal static void RegisterAssessment(IOCAssessmentDefinition definition, SceneCordon cordon)
    {
        assessment = definition;
        assessmentCordon = cordon;
    }

    // Procedural state only. The interaction layer validates gaze, range and line of sight.
    public static bool RecordIOCObservation(string id)
    {
        if (!IsGated || Phase != ScenarioPhase.IOCAssessment || assessment == null
            || !EntryAccessReady) return false;
        var observation = assessment.Find(id);
        if (observation == null || (observation.kind == IOCObservationKind.SceneSurvey
            && (assessmentCordon == null || !assessmentCordon.CordonVisible || assessmentCordon.EntranceIsOpen))
            || !observations.Add(id)) return false;
        SessionLogger.Instance?.LogEvent(SessionEventType.IOCObservationRecorded, id);
        OnIOCObservationRecorded?.Invoke(id);
        return true;
    }

    public static bool CompleteIOCAssessment() => Phase == ScenarioPhase.IOCAssessment
        && AssessmentReady && TryAdvance(ScenarioPhase.IOCSOCODecision);

    private static double investigationStartRealtime = -1d;
    private static double investigationEndRealtime = -1d;
    public static ScenarioPhase Phase { get; private set; } = ScenarioPhase.IOCTurnover;
    public static ScenarioPlayerRole PlayerRole => Phase >= ScenarioPhase.SOCOInvestigation
        ? ScenarioPlayerRole.SOCO : ScenarioPlayerRole.IOC;
    public static event Action<ScenarioPhase> OnPhaseChanged;
    /// <summary>FR to IOC turnover only; does not unlock forensic processing.</summary>
    public static event Action OnCompleted;
    public static event Action OnInvestigationStarted;
    public static bool IsGated => controller != null;
    public static bool IsPending => IsGated && Phase == ScenarioPhase.IOCTurnover;
    public static bool IsComplete => IsGated && Phase != ScenarioPhase.IOCTurnover;
    public static bool IsInvestigationBlocked => IsGated && Phase != ScenarioPhase.SOCOInvestigation && Phase != ScenarioPhase.SOCOCloseout;
    public static bool IsDeductionBlocked => ProcedureInterlock.BlocksUnrelatedActions || (IsGated && (Phase != ScenarioPhase.SOCOInvestigation || EndingManager.Instance?.AcceptedEnding != null));
    public static bool HasReachedPhase(ScenarioPhase phase) => PhaseOrder(Phase) >= PhaseOrder(phase);
    private static int PhaseOrder(ScenarioPhase phase) => phase == ScenarioPhase.InvestigationComplete ? 12 : (int)phase;
    public static bool InvestigationStarted => !IsGated || investigationStartRealtime >= 0d;
    public static bool Form1Prepared => IsGated;
    public static bool Form1Received => IsComplete;
    public static bool EntryPPEReady => assessmentCordon == null || assessmentCordon.PPEReady;
    public static bool EntryAccessReady => assessmentCordon == null || (assessmentCordon.PPEReady && assessmentCordon.AccessRegister != null && assessmentCordon.AccessRegister.IsAuthorized);
    /// <summary>IOC submits Form 2 on acknowledged SOCO arrival, before the briefing/processing.</summary>
    public static bool Form2Accomplished => IsGated && Phase >= ScenarioPhase.SOCOTurnover;
    public static float InvestigationSeconds => investigationStartRealtime < 0d ? 0f
        : (float)((investigationEndRealtime >= 0d ? investigationEndRealtime : Time.realtimeSinceStartupAsDouble) - investigationStartRealtime);
    public static FirstResponderTurnover Controller => controller;

    internal static void Register(FirstResponderTurnover owner)
    {
        controller = owner;
        ResetPhase();
    }
    internal static void Unregister(FirstResponderTurnover owner)
    {
        if (controller != owner) return;
        controller = null;
        ResetPhase();
    }
    internal static bool MarkComplete() => TryAdvance(ScenarioPhase.IOCAssessment);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static void PrepareDevelopmentStart()
    {
        SceneTestStart.RequirePreparation();
        if (!IsGated || assessment == null) throw new InvalidOperationException("Missing IOC workflow definition.");
        assessmentCordon?.PPEStation?.PrepareDevelopmentGloves();
        SwabTool.Instance?.PrepareDevelopmentSwab();
        assessmentCordon?.AccessRegister?.PrepareDevelopmentAuthorization();
        foreach (var observation in assessment.observations) observations.Add(observation.observationId);
        assistanceDecisions.Add("request-soco");
        Phase = ScenarioPhase.SOCOInvestigation;
        investigationStartRealtime = Time.realtimeSinceStartupAsDouble;
        OnPhaseChanged?.Invoke(Phase);
        OnInvestigationStarted?.Invoke();
    }
#endif

    /// <summary>
    /// Ordered hook for future procedural interactions. Skips, repeats and backward
    /// transitions are rejected. Entering SOCOTurnover certifies Form 2; completing it certifies briefing
    /// before processing begins; no form-entry UI is implied here.
    /// </summary>
    public static bool TryAdvance(ScenarioPhase next)
    {
        if (!IsGated || !IsNextPhase(Phase, next)) return false;
        if ((next == ScenarioPhase.SOCOCloseout || next == ScenarioPhase.InvestigationComplete)
            && (CrimeSceneSketchManager.CompletionBlockReason != null || VendorFollowupInterview.CompletionBlockReason != null)) return false;
        if (next == ScenarioPhase.IOCSOCODecision && !AssessmentReady) return false;
        if (next == ScenarioPhase.SOCOCloseout && EndingManager.Instance?.FeedbackAccepted != true) return false;
        if (next == ScenarioPhase.EvidenceInventoryReview && SOCOCloseout.Instance?.AllAccountedFor != true) return false;
        if ((next == ScenarioPhase.FinalSceneSurvey || next == ScenarioPhase.IOCCompletionReport || next == ScenarioPhase.SceneRelease)
            && (SOCOCloseout.Instance?.IsTransitionAuthorized(next) != true || !SOCOCloseout.Instance.CustodyPassComplete)) return false;
        if (next == ScenarioPhase.InvestigationComplete && SOCOCloseout.Instance?.ReleaseCompleted != true) return false;
        Phase = next;
        if (next == ScenarioPhase.IOCAssessment)
            SessionLogger.Instance?.LogEvent(SessionEventType.IOCAssessmentStarted, "ioc-assessment");
        if (next == ScenarioPhase.IOCSOCODecision)
            SessionLogger.Instance?.LogEvent(SessionEventType.IOCAssessmentCompleted, "ioc-assessment");
        if (next == ScenarioPhase.SOCORequested)
            SessionLogger.Instance?.LogEvent(SessionEventType.SOCORequested, "soco-request");
        if (next == ScenarioPhase.SOCOTurnover)
        {
            SessionLogger.Instance?.LogEvent(SessionEventType.CSIForm2Completed, "CSI-Form-2");
            SessionLogger.Instance?.LogEvent(SessionEventType.SOCOTurnoverStarted, "ioc-soco-turnover");
        }
        if (next == ScenarioPhase.SOCOInvestigation)
        {
            investigationStartRealtime = Time.realtimeSinceStartupAsDouble;
            SessionLogger.Instance?.LogEvent(SessionEventType.SOCOTurnoverCompleted, "ioc-soco-turnover");
            SessionLogger.Instance?.LogEvent(SessionEventType.SOCOInvestigationStarted, "soco-investigation");
        }
        if (next == ScenarioPhase.InvestigationComplete)
            investigationEndRealtime = Time.realtimeSinceStartupAsDouble;
        SessionEventType? milestone = next switch
        {
            ScenarioPhase.SOCOCloseout => SessionEventType.SOCOCloseoutStarted,
            ScenarioPhase.FinalSceneSurvey => SessionEventType.EvidenceInventoryConfirmed,
            ScenarioPhase.IOCCompletionReport => SessionEventType.FinalSceneSurveyCompleted,
            ScenarioPhase.SceneRelease => SessionEventType.SceneReleaseConfirmed,
            ScenarioPhase.InvestigationComplete => SessionEventType.InvestigationCompleted,
            _ => null
        };
        if (milestone.HasValue) SessionLogger.Instance?.LogEvent(milestone.Value, "soco-closeout");
        OnPhaseChanged?.Invoke(next);
        if (next == ScenarioPhase.IOCAssessment) OnCompleted?.Invoke();
        if (next == ScenarioPhase.SOCOInvestigation) OnInvestigationStarted?.Invoke();
        return true;
    }
    private static bool IsNextPhase(ScenarioPhase current, ScenarioPhase next) => current switch
    {
        ScenarioPhase.IOCTurnover => next == ScenarioPhase.IOCAssessment,
        ScenarioPhase.IOCAssessment => next == ScenarioPhase.IOCSOCODecision,
        ScenarioPhase.IOCSOCODecision => next == ScenarioPhase.SOCORequested,
        ScenarioPhase.SOCORequested => next == ScenarioPhase.SOCOTurnover,
        ScenarioPhase.SOCOTurnover => next == ScenarioPhase.SOCOInvestigation,
        ScenarioPhase.SOCOInvestigation => next == ScenarioPhase.SOCOCloseout,
        ScenarioPhase.SOCOCloseout => next == ScenarioPhase.EvidenceInventoryReview,
        ScenarioPhase.EvidenceInventoryReview => next == ScenarioPhase.FinalSceneSurvey,
        ScenarioPhase.FinalSceneSurvey => next == ScenarioPhase.IOCCompletionReport,
        ScenarioPhase.IOCCompletionReport => next == ScenarioPhase.SceneRelease,
        ScenarioPhase.SceneRelease => next == ScenarioPhase.InvestigationComplete,
        _ => false
    };
    private static void ResetPhase()
    {
        Phase = ScenarioPhase.IOCTurnover;
        observations.Clear();
        assistanceDecisions.Clear();
        assessment = null;
        assessmentCordon = null;
        investigationStartRealtime = investigationEndRealtime = -1d;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        controller = null;
        ResetPhase();
        OnIOCObservationRecorded = null;
        OnCompleted = null;
        OnInvestigationStarted = null;
        OnPhaseChanged = null;
    }
}
