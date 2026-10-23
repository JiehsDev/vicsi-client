// Assets/_Project/Scripts/ScenePhotography/ScenePhotographyManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guided scene (location) photography, independent of EvidenceStateManager: no evidence
/// statuses, no evidence ids. The route is whatever ScenePhotoPoints exist in the scene,
/// ordered by sequenceIndex - nothing here knows how many points there are, what they
/// are called, or which phase follows which.
///
/// Listens to PhotographTool.OnShutterReleased (the same shutter the evidence flow uses),
/// so both systems react to one press without either blocking the other. A press counts
/// as a scene photo only while the player stands in a point's zone - except a pure scene
/// shot of the current view taken from near (but outside) its zone, which is recorded as
/// a wrong-position attempt. Anything else outside every zone is ignored here.
///
/// Query surface for later RequirementNode / scoring integration: IsPointCompleted,
/// IsPhaseCompleted, IsScenePhotographyComplete, and the attempt counters.
/// </summary>
public class ScenePhotographyManager : MonoBehaviour
{
    public static ScenePhotographyManager Instance { get; private set; }

    // Feedback wording lives in ViCSIDialogueCatalog: "photo-wrong-angle", "photo-out-of-sequence",
    // "photo-scene-complete", "photo-wrong-position", "photo-zone-entered" ({0} = the point's displayName),
    // and each ScenePhotoPoint's own instructionId.
    [Tooltip("The tip for the same point is not repeated within this many seconds, so stepping in and out of the circle doesn't spam it.")]
    [SerializeField, Min(0f)] private float zoneTipCooldownSeconds = 10f;

    [Header("Wrong-position detection")]
    [Tooltip("A pure scene shot (no evidence in frame) aimed at the current point's view counts as a wrong-position attempt only within this horizontal distance (m) of its zone centre - further away it is just an unrelated photo.")]
    [SerializeField, Min(0f)] private float wrongPositionMaxDistance = 3f;

    [Header("Assessment weights (client display; the server's ScoringRules are authoritative)")]
    [Tooltip("Relative weights of coverage / sequence / position-and-angle accuracy. Normalized by their sum. Keep in step with the dashboard's ScoringRules.scenePhotography.")]
    [SerializeField] private ScenePhotographyWeights assessmentWeights = ScenePhotographyWeights.Default;

    [Tooltip("A repeat of the same non-completing result at the same point within this window (s) is not logged or notified again - stops rapid shutter presses spamming the log.")]
    [SerializeField, Min(0f)] private float repeatSuppressSeconds = 1.5f;

    [Tooltip("Seconds after scene start before the first point's instruction is shown, so it doesn't land on top of the opening dispatch.")]
    [SerializeField, Min(0f)] private float firstInstructionDelay = 2f;

    private readonly List<ScenePhotoPoint> points = new();
    private readonly HashSet<ScenePhotoPhase> completedPhases = new();
    private readonly List<PhotographTool> subscribedTools = new();
    private int currentIndex = -1;
    private bool allCompleteFired;

    private string lastRepeatKey;
    private float lastRepeatTime;

    private ScenePhotoPoint lastTipPoint;
    private float lastTipTime = float.NegativeInfinity;

    public event Action<ScenePhotoPoint> OnPointActivated;
    public event Action<ScenePhotoPoint> OnPointCompleted;
    public event Action<ScenePhotoPhase> OnPhaseCompleted;
    public event Action OnScenePhotographyCompleted;

    /// <summary>
    /// Static counterpart of OnPointCompleted (photoPointId), fired for every completed
    /// point including the last. Matches EvidenceStateManager's static-event pattern so
    /// requirement-driven managers can re-evaluate RequirementType.ScenePhotographyCompleted
    /// without depending on this manager's Awake order.
    /// </summary>
    public static event Action<string> OnScenePhotoProgress;

    /// <summary>
    /// Fired when the player's head enters (true) or leaves (false) the ACTIVE point's
    /// standing zone - the one place a shutter press completes the current scene photo.
    /// Static so viewfinder feedback can subscribe without depending on Awake order.
    /// </summary>
    public static event Action<bool> OnActiveZoneChanged;

    /// <summary>True while the player stands inside the active point's zone (never for later or completed points).</summary>
    public bool IsPlayerInActiveZone { get; private set; }

    public IReadOnlyList<ScenePhotoPoint> Points => points;

    /// <summary>The point the player is currently guided to, or null when the route is finished (or empty).</summary>
    public ScenePhotoPoint CurrentPoint => currentIndex >= 0 && currentIndex < points.Count ? points[currentIndex] : null;

    public ScenePhotoPhase? CurrentPhase => CurrentPoint != null ? CurrentPoint.phase : (ScenePhotoPhase?)null;

    public bool IsScenePhotographyComplete { get; private set; }

    /// <summary>An unfinished route station owns its shutter, including retries and
    /// out-of-sequence attempts. Decide before evidence logic or route completion runs.</summary>
    public bool OwnsShutter(Vector3 headPosition)
    {
        if (!isActiveAndEnabled || SceneTurnover.IsInvestigationBlocked || IsScenePhotographyComplete)
            return false;
        foreach (var point in points)
            if (!point.IsCompleted && point.ContainsPlayer(headPosition))
                return true;
        return false;
    }

    public int OutOfSequenceAttempts { get; private set; }
    public int WrongAngleAttempts { get; private set; }
    public int WrongPositionAttempts { get; private set; }
    public int CorrectSequenceCount { get; private set; }

    /// <summary>Real time (same clock as SessionLogger timestamps) when the route became active, or -1 before that.</summary>
    public float StartedAtTime { get; private set; } = -1f;

    /// <summary>Real time when every required point was completed, or -1.</summary>
    public float CompletedAtTime { get; private set; } = -1f;

    private void Awake()
    {
        Instance = this;
        if (GetComponent<ScenePhotographyGuide>() == null) gameObject.AddComponent<ScenePhotographyGuide>();
    }

    private void OnDestroy()
    {
        SceneTurnover.OnInvestigationStarted -= HandleSceneTurnedOver;
        foreach (var tool in subscribedTools)
        {
            if (tool != null)
            {
                tool.OnShutterReleased -= HandleShutter;
            }
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        foreach (var tool in FindObjectsByType<PhotographTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            tool.OnShutterReleased += HandleShutter;
            subscribedTools.Add(tool);
        }

        foreach (var point in FindObjectsByType<ScenePhotoPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            points.Add(point);
        }

        // Stable sort: equal sequenceIndex keeps a deterministic order by id.
        points.Sort((a, b) =>
        {
            int c = a.sequenceIndex.CompareTo(b.sequenceIndex);
            return c != 0 ? c : string.CompareOrdinal(a.photoPointId, b.photoPointId);
        });
        ValidatePoints();

        foreach (var point in points)
        {
            point.ResetState();
        }

        if (points.Count == 0)
        {
            return;
        }

        // Scene photography starts at SOCO investigation, after IOC and both turnovers.
        // Scenes without a registered procedural owner retain their previous behaviour.
        if (SceneTurnover.IsInvestigationBlocked)
        {
            SceneTurnover.OnInvestigationStarted += HandleSceneTurnedOver;
            return;
        }

        Invoke(nameof(ActivateFirstPoint), firstInstructionDelay);
    }

    private void HandleSceneTurnedOver()
    {
        SceneTurnover.OnInvestigationStarted -= HandleSceneTurnedOver;
        if (this == null) return;
        // Route markers are held hidden until turnover (ScenePhotoPoint.ApplyVisibility);
        // re-apply them now so whichever point the route has current shows.
        foreach (var point in points)
        {
            point.ApplyVisibility();
        }
        Invoke(nameof(ActivateFirstPoint), firstInstructionDelay);
    }

    private void ActivateFirstPoint()
    {
        if (!SceneTurnover.IsInvestigationBlocked && currentIndex < 0)
        {
            AdvanceTo(0);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentStart(bool completed)
    {
        SceneTestStart.RequirePreparation();
        CancelInvoke(nameof(ActivateFirstPoint));
        if (!completed) { ActivateFirstPoint(); return; }
        foreach (var point in points)
        {
            point.MarkCompleted(); point.SetHighlighted(false);
            completedPhases.Add(point.phase);
        }
        currentIndex = points.Count;
        allCompleteFired = IsScenePhotographyComplete = true;
        IsPlayerInActiveZone = false;
        StartedAtTime = CompletedAtTime = Time.time;
        OnActiveZoneChanged?.Invoke(false);
        foreach (var point in points) OnScenePhotoProgress?.Invoke(point.photoPointId);
        OnScenePhotographyCompleted?.Invoke();
    }
#endif

    // --- Queries ---------------------------------------------------------

    public bool IsPointCompleted(string photoPointId)
    {
        var point = Find(photoPointId);
        return point != null && point.IsCompleted;
    }

    /// <summary>True when the phase has at least one required point and all of them are completed.</summary>
    public bool IsPhaseCompleted(ScenePhotoPhase phase)
    {
        bool any = false;
        foreach (var point in points)
        {
            if (point.phase != phase || !point.isRequired)
            {
                continue;
            }
            any = true;
            if (!point.IsCompleted)
            {
                return false;
            }
        }
        return any;
    }

    public ScenePhotoPoint Find(string photoPointId)
    {
        if (string.IsNullOrEmpty(photoPointId))
        {
            return null;
        }
        foreach (var point in points)
        {
            if (point.photoPointId == photoPointId)
            {
                return point;
            }
        }
        return null;
    }

    // --- Shutter handling ------------------------------------------------

    private void Update()
    {
        var current = IsScenePhotographyComplete ? null : CurrentPoint;
        var cam = Camera.main;
        bool inZone = current != null && cam != null && current.ContainsPlayer(cam.transform.position);
        if (inZone == IsPlayerInActiveZone)
        {
            return;
        }

        IsPlayerInActiveZone = inZone;
        OnActiveZoneChanged?.Invoke(inZone);

        if (inZone
            && (current != lastTipPoint || Time.time - lastTipTime >= zoneTipCooldownSeconds))
        {
            lastTipPoint = current;
            lastTipTime = Time.time;
            string viewName = string.IsNullOrEmpty(current.displayName) ? GameDialogue.Text("photo-view-unnamed") : current.displayName;
            GameDialogue.Say("photo-zone-entered", viewName);
        }
    }

    private void HandleShutter(PhotographTool tool, bool evidenceCaptured)
    {
        if (SceneTurnover.IsInvestigationBlocked || IsScenePhotographyComplete || points.Count == 0)
        {
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        // Not started yet (still inside the initial instruction delay) - start now so
        // the press isn't silently lost.
        if (!SceneTurnover.IsInvestigationBlocked && currentIndex < 0)
        {
            AdvanceTo(0);
        }

        Transform camTransform = cam.transform;
        Vector3 head = camTransform.position;
        var current = CurrentPoint;

        if (current != null && current.ContainsPlayer(head))
        {
            HandleAttemptAtCurrent(current, camTransform, tool, evidenceCaptured);
            return;
        }

        int laterIndex = FindIncompleteLaterPointContaining(head);
        if (laterIndex < 0)
        {
            // Outside every open zone (or inside an already-completed one). Repeats at
            // a completed point deliberately do nothing; the only thing that still
            // counts is trying the current view from the wrong spot.
            if (current != null && !evidenceCaptured && !tool.CanCapture && !IsInsideCompletedPoint(head))
            {
                TryRecordWrongPosition(current, camTransform, tool);
            }
            return;
        }

        var later = points[laterIndex];
        float angle = later.AngleFrom(camTransform);
        if (angle > later.angleTolerance)
        {
            // Standing in a future zone but not framing its view - most likely an
            // ordinary evidence photo; not a scene attempt.
            return;
        }

        if (OnlyOptionalPointsBefore(laterIndex))
        {
            PlayShutterIfNeeded(tool, evidenceCaptured);
            LogAttempt(later, "completed-skipping-optional", angle);
            Complete(later, angle, inOrder: true);
            tool?.GetComponentInChildren<PhotoCaptureListener>()?.CaptureScenePhoto(later);
            AdvanceTo(NextIncompleteIndex(laterIndex + 1));
            return;
        }

        PlayShutterIfNeeded(tool, evidenceCaptured);
        if (IsRepeat(later, "out-of-sequence"))
        {
            return;
        }

        OutOfSequenceAttempts++;
        LogAttempt(later, "out-of-sequence", angle);
        SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotoOutOfSequence, later.LogLabel, new Dictionary<string, string>
        {
            { "expectedPointId", current != null ? current.LogLabel : string.Empty },
            { "phase", later.phase.ToString() },
            { "sequenceIndex", later.sequenceIndex.ToString() }
        });
        GameDialogue.Say("photo-out-of-sequence");
    }

    private void TryRecordWrongPosition(ScenePhotoPoint current, Transform camTransform, PhotographTool tool)
    {
        Vector3 head = camTransform.position;
        Vector3 center = current.transform.position;
        float distance = new Vector2(head.x - center.x, head.z - center.z).magnitude;
        float angle = current.AngleFrom(camTransform);
        if (distance > wrongPositionMaxDistance || angle > current.angleTolerance || Mathf.Abs(head.y - center.y) > current.maxHeadHeight)
        {
            return;
        }

        PlayShutterIfNeeded(tool, false);
        if (IsRepeat(current, "wrong-position"))
        {
            return;
        }

        WrongPositionAttempts++;
        LogAttempt(current, "wrong-position", angle);
        SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotoWrongPosition, current.LogLabel, new Dictionary<string, string>
        {
            { "distance", distance.ToString("F2") },
            { "angle", angle.ToString("F1") },
            { "phase", current.phase.ToString() }
        });
        GameDialogue.Say("photo-wrong-position");
    }

    private bool IsInsideCompletedPoint(Vector3 head)
    {
        foreach (var point in points)
        {
            if (point.IsCompleted && point.ContainsPlayer(head))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Current scene-photography performance - see ScenePhotographyScoring for the formula.</summary>
    public ScenePhotographyAssessmentResult BuildAssessment()
    {
        int required = 0, completedRequired = 0, completedTotal = 0;
        foreach (var point in points)
        {
            if (point.isRequired)
            {
                required++;
            }
            if (point.IsCompleted)
            {
                completedTotal++;
                if (point.isRequired)
                {
                    completedRequired++;
                }
            }
        }

        float seconds = StartedAtTime >= 0f && CompletedAtTime >= 0f ? CompletedAtTime - StartedAtTime : -1f;
        return ScenePhotographyScoring.Compute(required, completedRequired, completedTotal, CorrectSequenceCount,
            OutOfSequenceAttempts, WrongPositionAttempts, WrongAngleAttempts, IsScenePhotographyComplete, seconds, assessmentWeights);
    }

    private void HandleAttemptAtCurrent(ScenePhotoPoint current, Transform camTransform, PhotographTool tool, bool evidenceCaptured)
    {
        // The photo itself always happens - only completion depends on aim.
        PlayShutterIfNeeded(tool, evidenceCaptured);

        float angle = current.AngleFrom(camTransform);
        if (angle > current.angleTolerance)
        {
            if (IsRepeat(current, "wrong-angle"))
            {
                return;
            }

            WrongAngleAttempts++;
            LogAttempt(current, "wrong-angle", angle);
            SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotoWrongAngle, current.LogLabel, new Dictionary<string, string>
            {
                { "angle", angle.ToString("F1") },
                { "tolerance", current.angleTolerance.ToString("F1") },
                { "phase", current.phase.ToString() }
            });
            GameDialogue.Say("photo-wrong-angle");
            return;
        }

        LogAttempt(current, "completed", angle);
        Complete(current, angle, inOrder: true);
        tool?.GetComponentInChildren<PhotoCaptureListener>()?.CaptureScenePhoto(current);
        AdvanceTo(NextIncompleteIndex(currentIndex + 1));
    }

    private void Complete(ScenePhotoPoint point, float angle, bool inOrder)
    {
        if (point.IsCompleted)
        {
            return;
        }

        point.MarkCompleted();
        lastRepeatKey = null;
        if (inOrder)
        {
            CorrectSequenceCount++;
        }

        SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotoCompleted, point.LogLabel, new Dictionary<string, string>
        {
            { "phase", point.phase.ToString() },
            { "sequenceIndex", point.sequenceIndex.ToString() },
            { "angle", angle.ToString("F1") },
            { "inOrder", inOrder.ToString() },
            { "required", point.isRequired.ToString() },
            { "scoreWeight", point.scoreWeight.ToString("F2") }
        });
        Debug.Log($"[ScenePhotographyManager] Completed {point.LogLabel} ({point.phase}, angle {angle:F1}).");
        OnPointCompleted?.Invoke(point);

        if (!completedPhases.Contains(point.phase) && IsPhaseCompleted(point.phase))
        {
            completedPhases.Add(point.phase);
            SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotographyPhaseCompleted, point.phase.ToString());
            OnPhaseCompleted?.Invoke(point.phase);
        }

        if (!allCompleteFired && AllRequiredCompleted())
        {
            allCompleteFired = true;
            IsScenePhotographyComplete = true;
            CompletedAtTime = (float)Time.realtimeSinceStartupAsDouble;

            int completedCount = 0;
            foreach (var p in points)
            {
                if (p.IsCompleted)
                {
                    completedCount++;
                }
            }

            SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotographyCompleted, null, new Dictionary<string, string>
            {
                { "completedCount", completedCount.ToString() },
                { "outOfSequenceAttempts", OutOfSequenceAttempts.ToString() },
                { "wrongAngleAttempts", WrongAngleAttempts.ToString() },
                { "wrongPositionAttempts", WrongPositionAttempts.ToString() },
                { "completionSeconds", (CompletedAtTime - StartedAtTime).ToString("F1") }
            });
            GameDialogue.Say("photo-scene-complete");
            OnScenePhotographyCompleted?.Invoke();
        }

        // After IsScenePhotographyComplete is set, so listeners re-evaluating
        // requirements see the final state.
        OnScenePhotoProgress?.Invoke(point.LogLabel);
    }

    private void AdvanceTo(int index)
    {
        if (StartedAtTime < 0f)
        {
            StartedAtTime = (float)Time.realtimeSinceStartupAsDouble;
            int required = 0;
            foreach (var point in points)
            {
                if (point.isRequired)
                {
                    required++;
                }
            }
            SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotographyStarted, null, new Dictionary<string, string>
            {
                { "requiredPointCount", required.ToString() },
                { "pointCount", points.Count.ToString() }
            });
        }

        if (CurrentPoint != null)
        {
            CurrentPoint.SetHighlighted(false);
        }

        currentIndex = index;
        var next = CurrentPoint;
        if (next == null)
        {
            return;
        }

        next.SetHighlighted(true);
        // The completion toast takes precedence over the next instruction.
        if (!IsScenePhotographyComplete && next.HasInstruction)
        {
            next.ShowInstruction();
        }
        OnPointActivated?.Invoke(next);
    }

    // --- Helpers ----------------------------------------------------------

    private int NextIncompleteIndex(int from)
    {
        for (int i = Mathf.Max(0, from); i < points.Count; i++)
        {
            if (!points[i].IsCompleted)
            {
                return i;
            }
        }
        return points.Count;
    }

    private int FindIncompleteLaterPointContaining(Vector3 head)
    {
        for (int i = Mathf.Max(0, currentIndex + 1); i < points.Count; i++)
        {
            if (!points[i].IsCompleted && points[i].ContainsPlayer(head))
            {
                return i;
            }
        }
        return -1;
    }

    private bool OnlyOptionalPointsBefore(int index)
    {
        for (int i = Mathf.Max(0, currentIndex); i < index; i++)
        {
            if (points[i].isRequired && !points[i].IsCompleted)
            {
                return false;
            }
        }
        return true;
    }

    private bool AllRequiredCompleted()
    {
        bool anyRequired = false;
        foreach (var point in points)
        {
            if (!point.isRequired)
            {
                continue;
            }
            anyRequired = true;
            if (!point.IsCompleted)
            {
                return false;
            }
        }
        return anyRequired;
    }

    private bool IsRepeat(ScenePhotoPoint point, string result)
    {
        string key = point.LogLabel + "|" + result;
        bool repeat = key == lastRepeatKey && Time.unscaledTime - lastRepeatTime < repeatSuppressSeconds;
        lastRepeatKey = key;
        lastRepeatTime = Time.unscaledTime;
        return repeat;
    }

    private static void PlayShutterIfNeeded(PhotographTool tool, bool evidenceCaptured)
    {
        if (!evidenceCaptured && tool != null)
        {
            tool.PlayShutterFeedback();
        }
    }

    private static void LogAttempt(ScenePhotoPoint point, string result, float angle)
    {
        SessionLogger.Instance?.LogEvent(SessionEventType.ScenePhotoAttempted, point.LogLabel, new Dictionary<string, string>
        {
            { "phase", point.phase.ToString() },
            { "sequenceIndex", point.sequenceIndex.ToString() },
            { "result", result },
            { "angle", angle.ToString("F1") }
        });
    }

    private void ValidatePoints()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (string.IsNullOrEmpty(point.photoPointId))
            {
                Debug.LogWarning($"[ScenePhotographyManager] ScenePhotoPoint '{point.name}' has no photoPointId.", point);
            }
            else if (!ids.Add(point.photoPointId))
            {
                Debug.LogWarning($"[ScenePhotographyManager] Duplicate photoPointId '{point.photoPointId}'.", point);
            }

            if (i > 0 && points[i - 1].sequenceIndex == point.sequenceIndex)
            {
                Debug.LogWarning($"[ScenePhotographyManager] '{points[i - 1].LogLabel}' and '{point.LogLabel}' share sequenceIndex {point.sequenceIndex}; ordered by id.", point);
            }

            if (i > 0 && point.phase < points[i - 1].phase)
            {
                Debug.LogWarning($"[ScenePhotographyManager] '{point.LogLabel}' ({point.phase}) comes after '{points[i - 1].LogLabel}' ({points[i - 1].phase}) - phases are out of outside-in order.", point);
            }
        }
    }
}
