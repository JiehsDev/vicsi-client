using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Local, non-scored authorization and observed perimeter visits for one identified student.</summary>
public sealed class SceneAccessRegister : MonoBehaviour
{
    [Serializable] public sealed class Visit
    {
        public int number;
        public long timeInMs, timeOutMs = -1, simulatedRepositionMs = -1;
        public string roleAtEntry, purpose;
        public bool presentAtScenarioEnd;
        public string Status => presentAtScenarioEnd ? "Present at scenario end"
            : simulatedRepositionMs >= 0 ? "Simulated reposition; exit not observed" : timeOutMs >= 0 ? "Exited" : "Inside";
    }
    [Serializable] public sealed class Transition
    {
        public long elapsedMs;
        public string description;
    }
    [Serializable] public sealed class Record
    {
        public int version = 1;
        public string sessionId, studentId, name, roleAtRegistration, purpose, keeper = "First Responder";
        public long registrationMs;
        public bool authorized, finalized, developmentPreparation;
        public bool unassignedSession;
        public List<Visit> visits = new();
        public List<Transition> transitions = new();
    }
    private SceneCordon cordon;
    private SessionLogger logger;
    private Record record = new();
    private bool hasBaseline, stableInside;
    private bool? candidate;
    private float candidateSince;
    private long candidateMs;
    private int ignoreThroughFrame;
    private ScenarioPlayerRole lastRole;
    public event Action Changed;
    public bool IsAuthorized => record.authorized;
    public bool IsFinalized => record.finalized;
    public bool IsInside => stableInside;
    public string StudentName => record.name ?? ResolveOwner()?.displayNameSnapshot
        ?? (IsUnassignedDirectRun ? "Unassigned investigator (local session)" : "Student (test session)");
    public string StudentId => record.studentId;
    public IReadOnlyList<Visit> Visits => record.visits;
    public IReadOnlyList<Transition> Transitions => record.transitions;
    public string LocalPath => logger != null && !string.IsNullOrEmpty(logger.ActiveLogFilePath) ? logger.ActiveLogFilePath + ".access.json" : null;
    public string LastPersistenceError { get; private set; }
    public string CurrentPurpose => SceneTurnover.PlayerRole == ScenarioPlayerRole.IOC ? "Initial scene assessment" : "Forensic scene investigation";
    public SceneAccessRegisterUI UI { get; private set; }
    private long Now => logger != null ? (long)(logger.ElapsedSeconds * 1000) : 0;
    private Visit OpenVisit => record.visits.FindLast(v => v.timeOutMs < 0 && v.simulatedRepositionMs < 0 && !v.presentAtScenarioEnd);

    public void Initialize(SceneCordon owner)
    {
        cordon = owner; logger = SessionLogger.Instance; lastRole = SceneTurnover.PlayerRole;
        SceneTurnover.OnPhaseChanged += PhaseChanged;
        if (logger != null) logger.BeforeSessionEnd += FinalizeRegister;
        UI = owner.AuthoredRegister;
        if (UI == null)
        {
            var go = new GameObject("Scene access register");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            UI = go.AddComponent<SceneAccessRegisterUI>();
        }
        UI.Initialize(this, cordon);
    }
    private SessionOwnerRecord ResolveOwner() => logger == null ? null : SessionOwnership.Store.Find(logger.SessionId);
    private bool IsUnassignedDirectRun
    {
        get
        {
            if (logger == null) return false;
            var flow = FindAnyObjectByType<PreSessionFlowController>();
            return flow != null && flow.gameObject.scene == gameObject.scene
                && flow.AllowsUnassignedCheckIn(logger.SessionId);
        }
    }
    internal bool Authorize(out string reason)
    {
        reason = null;
        if (record.authorized || record.finalized || SceneTurnover.Phase != ScenarioPhase.IOCAssessment
            || cordon == null || !cordon.PPEReady) { reason = "Complete the briefing and put on gloves first."; return false; }
        var owner = ResolveOwner();
        bool unassigned = owner == null && IsUnassignedDirectRun;
        if (owner == null && !unassigned && !AutomatedTestSandbox.IsActive) { reason = "Confirm your student identity before checking in."; return false; }
        record.unassignedSession = unassigned;
        record.sessionId = logger.SessionId; record.studentId = owner?.studentId ?? (unassigned ? null : "test-investigator");
        record.name = owner?.displayNameSnapshot ?? (unassigned ? "Unassigned investigator (local session)" : "Student (test session)");
        record.roleAtRegistration = SceneTurnover.PlayerRole.ToString(); record.purpose = CurrentPurpose;
        record.registrationMs = Now; record.authorized = true;
        if (!Save()) { record.authorized = false; reason = "Unable to save check-in locally. Please try again."; return false; }
        InteractionSfx.Play(InteractionSound.Writing,UI!=null ? UI.transform.position : transform.position);
        Emit(SessionEventType.SceneLogSigned, new Dictionary<string, string> {
            { "studentId", record.studentId }, { "role", record.roleAtRegistration }, { "purpose", record.purpose },
            { "keeper", record.keeper }, { "status", "awaiting-entry" }, { "scored", "false" } });
        Changed?.Invoke(); return true;
    }

    /// <summary>Called after the cordon's SDK-aware rejection/correction, never on attempted crossings.</summary>
    public void ObserveBoundary(Vector3 head, Vector3 capsule, bool entryAllowed)
    {
        if (record.finalized || Time.frameCount <= ignoreThroughFrame) return;
        if (!record.authorized || !entryAllowed) { stableInside = false; hasBaseline = true; candidate = null; return; }
        bool inside = cordon.IsSecurelyInside(head, .06f) && cordon.IsSecurelyInside(capsule, .06f);
        bool outside = cordon.IsSecurelyOutside(head, .06f) && cordon.IsSecurelyOutside(capsule, .06f);
        if (!hasBaseline) { stableInside = inside; hasBaseline = true; candidate = null; return; }
        if ((!inside && !outside) || (inside == stableInside)) { candidate = null; return; }
        if (candidate != inside) { candidate = inside; candidateSince = Time.unscaledTime; candidateMs = Now; return; }
        if (Time.unscaledTime - candidateSince < .2f) return;
        stableInside = inside; candidate = null;
        if (inside)
        {
            var visit = new Visit { number = record.visits.Count + 1, timeInMs = candidateMs,
                roleAtEntry = SceneTurnover.PlayerRole.ToString(), purpose = CurrentPurpose };
            record.visits.Add(visit);
            Emit(SessionEventType.SceneAccessEntered, VisitPayload(visit));
        }
        else if (OpenVisit is Visit visit)
        {
            visit.timeOutMs = candidateMs;
            Emit(SessionEventType.SceneAccessExited, VisitPayload(visit));
        }
        Save(); Changed?.Invoke();
    }
    public void RecordSimulationReposition(Vector3 headAfter)
    {
        if (!record.authorized || record.finalized) return;
        bool nowInside = cordon.IsWithinSecurityFootprint(headAfter);
        if (!nowInside && OpenVisit is Visit visit) visit.simulatedRepositionMs = Now;
        record.transitions.Add(new Transition { elapsedMs = Now, description = "Simulated SOCO arrival reposition; no physical exit recorded" });
        stableInside = nowInside; hasBaseline = true; candidate = null; ignoreThroughFrame = Time.frameCount + 2;
        Emit(SessionEventType.SceneAccessSimulationTransition, new Dictionary<string, string> { { "kind", "soco-arrival-reposition" } });
        Save(); Changed?.Invoke();
    }
    private void PhaseChanged(ScenarioPhase phase)
    {
        var role = SceneTurnover.PlayerRole;
        if (record.authorized && !record.finalized && role != lastRole)
        {
            record.transitions.Add(new Transition { elapsedMs = Now, description = "Same student: simulated role handover from " + lastRole + " to " + role });
            Emit(SessionEventType.SceneAccessRoleChanged, new Dictionary<string, string> { { "from", lastRole.ToString() }, { "to", role.ToString() }, { "studentId", record.studentId } });
            Save(); Changed?.Invoke();
        }
        lastRole = role;
        if (phase == ScenarioPhase.InvestigationComplete) FinalizeRegister();
    }
    public void FinalizeRegister()
    {
        if (record.finalized) return;
        record.finalized = true; candidate = null;
        if (OpenVisit is Visit visit) visit.presentAtScenarioEnd = true;
        if (record.authorized)
        {
            Emit(SessionEventType.SceneAccessRegisterFinalized, new Dictionary<string, string> { { "visitCount", record.visits.Count.ToString() }, { "physicalExitInvented", "false" } });
            Save(); Changed?.Invoke();
        }
    }
    private Dictionary<string, string> VisitPayload(Visit visit) => new() {
        { "studentId", record.studentId }, { "visit", visit.number.ToString() }, { "role", visit.roleAtEntry },
        { "timeInMs", visit.timeInMs.ToString() }, { "timeOutMs", visit.timeOutMs.ToString() }, { "scored", "false" } };
    private void Emit(SessionEventType type, Dictionary<string, string> payload) { logger?.LogEvent(type, "scene-access-register", payload); logger?.Flush(); }
    private bool Save()
    {
        try
        {
            if (LocalPath == null) throw new IOException("Session log location unavailable.");
            AssessmentUploadQueueStore.WriteAtomic(LocalPath, JsonUtility.ToJson(record, true));
            LastPersistenceError = null; return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { LastPersistenceError = e.Message; Debug.LogWarning("[SceneAccessRegister] Local save failed: " + e.Message); return false; }
    }
    public bool RetrySave() => !record.authorized || Save();
    private void OnApplicationPause(bool paused) { if (paused && record.authorized) Save(); }
    private void OnDestroy()
    {
        // Scene-unload destruction order is unspecified: finalize while the logger is still alive,
        // or its BeforeSessionEnd callback has already finalized us.
        if (record.authorized && !record.finalized) FinalizeRegister();
        SceneTurnover.OnPhaseChanged -= PhaseChanged;
        if (logger != null) logger.BeforeSessionEnd -= FinalizeRegister;
        if (UI != null) Destroy(UI.gameObject);
    }
    public static string FormatTime(long milliseconds) => milliseconds < 0 ? "Not observed" : TimeSpan.FromMilliseconds(milliseconds).ToString(@"hh\:mm\:ss\.fff");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void PrepareDevelopmentAuthorization()
    {
        if (!AutomatedTestSandbox.IsActive) throw new InvalidOperationException("Access fixtures require the isolated sandbox.");
        record.authorized = true; record.developmentPreparation = true;
        record.studentId = "test-investigator"; record.name = "Student (test session)"; record.sessionId = logger.SessionId;
        record.roleAtRegistration = SceneTurnover.PlayerRole.ToString(); record.purpose = CurrentPurpose;
        hasBaseline = false; candidate = null;
    }
#endif
}
