// Assets/_Project/Scripts/Logging/SessionLogger.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton sink for every SessionEvent recorded during a play session - matches
/// EvidenceStateManager/ProceduralGateValidator's Instance pattern. Starts its own
/// session (new GUID, its own elapsed-time clock so timestamps are comparable across
/// sessions regardless of wall-clock time) in Awake, same as its sibling managers -
/// deliberately not triggered externally by RoleSceneLoader or anything else, so it's
/// always recording from the moment the scene's managers spin up, before any other
/// script's Start() could fire an event it would otherwise miss.
///
/// Subscribes directly to EvidenceStateManager.OnEvidenceStatusChanged and
/// OnEvidenceTransitionBlocked - individual tool scripts don't call SessionLogger for
/// evidence state, those two events are already the single source of truth for that
/// concern. Everything else (photography shot details, briefing flags, hypothesis
/// text, deduction board interactions) has no existing event to subscribe to yet;
/// once those systems exist, they call LogEvent() directly at the point of the
/// action - the API is ready for that today even though nothing calls it yet.
/// </summary>
public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance { get; private set; }

    [Tooltip("Active log backend. Must be a component implementing ISessionLogWriter (LocalJsonLogWriter today; a future SupabaseLogWriter drops in here unchanged - see ISessionLogWriter).")]
    [SerializeField] private MonoBehaviour writerSource;

    [Tooltip("Flush the writer after this many buffered events, in addition to the time-based flush below - whichever comes first.")]
    [SerializeField] private int flushEveryNEvents = 10;

    [Tooltip("Flush the writer after this many seconds have passed since the last flush, in addition to the count-based flush above. Matters on standalone Quest hardware, where a crash or unexpected quit has no guaranteed clean shutdown.")]
    [SerializeField] private float flushEverySeconds = 30f;

    private ISessionLogWriter Writer => writerSource as ISessionLogWriter;

    public string SessionId { get; private set; }

    /// <summary>
    /// Read-only accessor for the active log file's path, when the current writer is a
    /// LocalJsonLogWriter - null otherwise (a future writer backend, or none assigned).
    /// Purely additive: does not change LogEvent/Flush/EndSession behavior at all. Added
    /// so the separate assessment-upload layer (AssessmentSessionBuilder/
    /// AssessmentUploadService - see Assets/_Project/Scripts/Assessment) can locate the
    /// just-completed session's own log file to read back and embed, without that layer
    /// reaching into SessionLogger's private writerSource field or SessionLogger needing
    /// to know anything about uploading.
    /// </summary>
    public string ActiveLogFilePath => (writerSource as LocalJsonLogWriter)?.FilePath;

    private int nextSequenceNumber;
    private double sessionStartRealtime;
    private float timeSinceLastFlush;
    private int eventsSinceLastFlush;
    private bool sessionEnded;

    private readonly Dictionary<SessionEventType, int> eventCounts = new();

    /// <summary>
    /// How many times each SessionEventType has been logged so far this session - purely
    /// additive in-memory tally alongside the existing disk-written log, added so a
    /// post-session review can cheaply query counts (e.g. procedural-violation events)
    /// without re-reading and parsing the JSON log file back. Never changes what gets
    /// logged or how - see LogEvent, the only place this is incremented.
    /// </summary>
    public IReadOnlyDictionary<SessionEventType, int> EventCounts => eventCounts;

    /// <summary>Seconds since this session started (SessionStarted was logged) - the authoritative "investigation time" for post-session review, independent of any individual component's own Awake-relative timer (see DeductionScorer.Score's own narrower elapsedSeconds, which only measures time since the board component woke up).</summary>
    private double sessionEndRealtime = -1d;
    public float ElapsedSeconds => (float)((sessionEndRealtime >= 0d ? sessionEndRealtime : Time.realtimeSinceStartupAsDouble) - sessionStartRealtime);

    private void Awake()
    {
        Instance = this;
        SessionId = Guid.NewGuid().ToString();
        sessionStartRealtime = Time.realtimeSinceStartupAsDouble;

        if (writerSource is LocalJsonLogWriter localWriter)
        {
            localWriter.Initialize(SessionId);
            if (AutomatedTestSandbox.IsActive)
                localWriter.RelocateTo(System.IO.Path.Combine(AutomatedTestSandbox.Root, "session_log"));
        }

        // Keep the existing scene-based scenario identifier until the versioned
        // scenario/upload contract is introduced.
        string scenarioId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        LogEvent(SessionEventType.SessionStarted, targetId: scenarioId, payload: new Dictionary<string, string>
        {
            { "startedAtUtc", DateTime.UtcNow.ToString("O") },
            { "applicationVersion", Application.version }
        });
        Flush();
    }

    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleEvidenceStatusChanged;
        EvidenceStateManager.OnEvidenceTransitionBlocked += HandleEvidenceTransitionBlocked;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleEvidenceStatusChanged;
        EvidenceStateManager.OnEvidenceTransitionBlocked -= HandleEvidenceTransitionBlocked;
    }

    private void Update()
    {
        timeSinceLastFlush += Time.unscaledDeltaTime;
        if (timeSinceLastFlush >= flushEverySeconds)
        {
            Flush();
        }
    }

    private void OnApplicationQuit()
    {
        EndSession();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Flush();
    }

    private void OnDestroy()
    {
        EndSession();
        if (Instance == this) Instance = null;
    }

    /// <summary>Records one event. sequenceNumber/timestampMs are assigned internally - callers never compute these themselves.</summary>
    public void LogEvent(SessionEventType type, string targetId = null, Dictionary<string, string> payload = null)
    {
        if (sessionEnded)
        {
            Debug.LogWarning($"[SessionLogger] LogEvent({type}) called after EndSession(); ignored.");
            return;
        }

        var evt = new SessionEvent
        {
            sessionId = SessionId,
            sequenceNumber = nextSequenceNumber++,
            timestampMs = (long)((Time.realtimeSinceStartupAsDouble - sessionStartRealtime) * 1000.0),
            eventType = type.ToString(),
            targetId = targetId,
            payload = ToPayloadList(payload)
        };

        if (Writer == null)
        {
            Debug.LogWarning($"[SessionLogger] No ISessionLogWriter assigned; event {type} was not persisted.");
            return;
        }

        Writer.WriteEvent(evt);

        eventCounts.TryGetValue(type, out int currentCount);
        eventCounts[type] = currentCount + 1;

        eventsSinceLastFlush++;
        if (eventsSinceLastFlush >= flushEveryNEvents)
        {
            Flush();
        }
    }

    /// <summary>Allows session-local records to finalize before the event stream is frozen.</summary>
    public event Action BeforeSessionEnd;
    /// <summary>Logs SessionEnded, flushes, and blocks any further LogEvent calls this session. Safe to call more than once.</summary>
    public void EndSession()
    {
        if (sessionEnded)
        {
            return;
        }

        BeforeSessionEnd?.Invoke();
        LogEvent(SessionEventType.SessionEnded, payload: new Dictionary<string, string>
        {
            { "endedAtUtc", DateTime.UtcNow.ToString("O") }
        });
        Flush();
        sessionEndRealtime = Time.realtimeSinceStartupAsDouble;
        sessionEnded = true;
    }

    public void Flush()
    {
        Writer?.Flush();
        // A transient disk failure must not reset the retry thresholds.
        if (writerSource is LocalJsonLogWriter local && !local.LastFlushSucceeded) return;
        timeSinceLastFlush = 0f;
        eventsSinceLastFlush = 0;
    }

    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus newStatus)
    {
        var payload = new Dictionary<string, string>
        {
            { "status", newStatus.ToString() }
        };
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record != null)
        {
            payload["tool"] = record.lastToolUsed.ToString();
            if (newStatus == EvidenceStatus.Marked)
            {
                if (record.tentNumber.HasValue) payload["tentNumber"] = record.tentNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(record.tentLetter)) payload["tentLetter"] = record.tentLetter;
            }
        }
        LogEvent(SessionEventType.EvidenceStatusChanged, evidenceId, payload);
    }

    private void HandleEvidenceTransitionBlocked(string evidenceId, EvidenceStatus attempted, EvidenceStatus current)
    {
        LogEvent(SessionEventType.EvidenceTransitionBlocked, evidenceId, new Dictionary<string, string>
        {
            { "attempted", attempted.ToString() },
            { "current", current.ToString() }
        });
    }

    private static List<PayloadEntry> ToPayloadList(Dictionary<string, string> payload)
    {
        if (payload == null || payload.Count == 0)
        {
            return null;
        }

        var list = new List<PayloadEntry>(payload.Count);
        foreach (var kvp in payload)
        {
            list.Add(new PayloadEntry { key = kvp.Key, value = kvp.Value });
        }
        return list;
    }
}
