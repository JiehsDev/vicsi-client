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

    private int nextSequenceNumber;
    private double sessionStartRealtime;
    private float timeSinceLastFlush;
    private int eventsSinceLastFlush;
    private bool sessionEnded;

    private void Awake()
    {
        Instance = this;
        SessionId = Guid.NewGuid().ToString();
        sessionStartRealtime = Time.realtimeSinceStartupAsDouble;

        if (writerSource is LocalJsonLogWriter localWriter)
        {
            localWriter.Initialize(SessionId);
        }

        // No ScenarioDefinition/scenario-ID system exists yet (Data/Scenarios is
        // still an empty folder) - the active scene name is the best available
        // stand-in until one does. Replace this with a real scenario ID once that
        // system lands.
        string scenarioId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        LogEvent(SessionEventType.SessionStarted, targetId: scenarioId);
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

        eventsSinceLastFlush++;
        if (eventsSinceLastFlush >= flushEveryNEvents)
        {
            Flush();
        }
    }

    /// <summary>Logs SessionEnded, flushes, and blocks any further LogEvent calls this session. Safe to call more than once.</summary>
    public void EndSession()
    {
        if (sessionEnded)
        {
            return;
        }

        LogEvent(SessionEventType.SessionEnded);
        Flush();
        sessionEnded = true;
    }

    public void Flush()
    {
        Writer?.Flush();
        timeSinceLastFlush = 0f;
        eventsSinceLastFlush = 0;
    }

    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus newStatus)
    {
        LogEvent(SessionEventType.EvidenceStatusChanged, evidenceId, new Dictionary<string, string>
        {
            { "status", newStatus.ToString() }
        });
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
