// Assets/_Project/Scripts/Narrative/NarrativeEventManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton runtime driver for NarrativeEventSet - the requirement-gated, priority-
/// queued generalization of STCSManager's imperative Fire(triggerId). Coexists with
/// STCSManager rather than replacing it: STCSManager is live in this scene with real
/// authored DialoguePool content and real call sites throughout PlayerTool/RecorderTool,
/// so it stays completely untouched (see this sub-project's design note). Authors choose
/// per-beat which system fits: a simple "teammate comments the moment this exact code
/// path runs" beat stays on STCSManager; a beat that needs to react to investigation
/// progress generally (not just one call site), be gated by an ALL/ANY/NOT requirement
/// tree, compete on priority against other eligible beats, or unlock something elsewhere
/// via RequirementType.NarrativeEventCompleted belongs here.
///
/// Reuses STCSNotificationUI as its display surface rather than building new UI - the
/// popup doesn't care which system produced the line.
/// </summary>
public class NarrativeEventManager : MonoBehaviour
{
    public static NarrativeEventManager Instance { get; private set; }

    [SerializeField] private NarrativeEventSet eventSet;

    /// <summary>The authored narrative events (read-only; validation/tests).</summary>
    public IReadOnlyList<NarrativeEvent> DefinitionsForTest() => eventSet != null ? eventSet.events : new List<NarrativeEvent>();
    [SerializeField] private STCSNotificationUI notificationUI;
    [SerializeField] private float lineDisplaySeconds = 3f;
    [SerializeField] private AudioSource audioSource;

    /// <summary>Fired the moment a narrative event finishes playing - what RequirementType.NarrativeEventCompleted reads via IsCompleted, and what any other system can subscribe to directly.</summary>
    public static event System.Action<string> OnNarrativeEventCompleted;

    private class NarrativeEventRecord
    {
        public NarrativeEvent definition;
        public int timesFired;
        public bool isQueuedOrPlaying;
    }

    private readonly Dictionary<string, NarrativeEventRecord> records = new();
    private readonly List<NarrativeEventRecord> pending = new();
    private bool isPlaying;

    private void Awake()
    {
        Instance = this;

        if (eventSet == null || eventSet.events == null)
        {
            return;
        }

        foreach (var def in eventSet.events)
        {
            if (def == null || string.IsNullOrEmpty(def.eventId))
            {
                Debug.LogWarning("[NarrativeEventManager] Skipped a NarrativeEvent with no eventId.", this);
                continue;
            }

            if (records.ContainsKey(def.eventId))
            {
                Debug.LogWarning($"[NarrativeEventManager] Duplicate eventId '{def.eventId}' - only the first registered instance is tracked.", this);
                continue;
            }

            records[def.eventId] = new NarrativeEventRecord { definition = def };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleEvidenceStatusChanged;
        EvidenceStateManager.OnReportLineSelected += HandleReportLineSelected;
        EvidenceStateManager.OnFingerprintLabStatusChanged += HandleFingerprintLabStatusChanged;
        ScenePhotographyManager.OnScenePhotoProgress += HandleScenePhotoProgress;
        MeasurementStateManager.OnMeasurementProgress += HandleMeasurementProgress;
        EvidenceStateManager.OnEvidenceSwabbed += HandleEvidenceSwabbed;
        EvidenceStateManager.OnEvidenceFingerprinted += HandleEvidenceFingerprinted;
        FindingStateManager.OnFindingConfirmed += HandleFindingConfirmed;
        ObjectiveStateManager.OnObjectiveStateChanged += HandleObjectiveStateChanged;
        SceneTurnover.OnInvestigationStarted += HandleSceneTurnedOver;

        // Initial pass: any Automatic event whose trigger is already satisfied at scene
        // start (an empty ALL group, e.g.) should not have to wait for the first event.
        RefreshEligibility();
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleEvidenceStatusChanged;
        EvidenceStateManager.OnReportLineSelected -= HandleReportLineSelected;
        EvidenceStateManager.OnFingerprintLabStatusChanged -= HandleFingerprintLabStatusChanged;
        ScenePhotographyManager.OnScenePhotoProgress -= HandleScenePhotoProgress;
        MeasurementStateManager.OnMeasurementProgress -= HandleMeasurementProgress;
        EvidenceStateManager.OnEvidenceSwabbed -= HandleEvidenceSwabbed;
        EvidenceStateManager.OnEvidenceFingerprinted -= HandleEvidenceFingerprinted;
        FindingStateManager.OnFindingConfirmed -= HandleFindingConfirmed;
        ObjectiveStateManager.OnObjectiveStateChanged -= HandleObjectiveStateChanged;
        SceneTurnover.OnInvestigationStarted -= HandleSceneTurnedOver;
    }

    // Existing investigation narrative waits throughout IOC; refresh and release at SOCO start.
    private void HandleSceneTurnedOver()
    {
        RefreshEligibility();
        TryPlayNext();
    }

    // Same "any relevant change, recheck everything pending" shape RelationshipStateManager
    // and HypothesisCheckpointManager's own event handlers already use. FindingConfirmed/
    // ObjectiveStateChanged were missing here before this task - a NarrativeEvent whose
    // trigger references RequirementType.FindingConfirmed or ObjectiveCompleted (the mission-
    // narration layer this task adds relies on both) previously only got re-checked
    // incidentally, on the NEXT raw evidence-state event, not the moment the finding/
    // objective itself actually changed - see FindingStateManager/InsightStateManager's own
    // equivalent subscription for the established precedent this mirrors.
    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus status) => RefreshEligibility();
    private void HandleReportLineSelected(string evidenceId, string lineId) => RefreshEligibility();
    private void HandleFingerprintLabStatusChanged(string evidenceId, FingerprintLabStatus status) => RefreshEligibility();
    // RequirementType.ScenePhotographyCompleted has no evidence event of its own.
    private void HandleScenePhotoProgress(string photoPointId) => RefreshEligibility();
    // RequirementType.MeasurementCompleted, same reason.
    private void HandleMeasurementProgress(string requirementId) => RefreshEligibility();
    private void HandleEvidenceSwabbed(string evidenceId) => RefreshEligibility();
    private void HandleEvidenceFingerprinted(string evidenceId) => RefreshEligibility();
    private void HandleFindingConfirmed(string findingId) => RefreshEligibility();
    private void HandleObjectiveStateChanged(string objectiveId, ObjectiveState state) => RefreshEligibility();

    /// <summary>
    /// Re-evaluates every Automatic-mode event not already queued/playing and, if not
    /// repeatable, not already fired - promotes anything whose `trigger` is now
    /// satisfied into the pending queue. Safe to call redundantly.
    /// </summary>
    public void RefreshEligibility()
    {
        foreach (var record in records.Values)
        {
            if (record.definition.triggerMode != NarrativeTriggerMode.Automatic)
            {
                continue;
            }

            if (record.isQueuedOrPlaying || (!record.definition.repeatable && record.timesFired > 0))
            {
                continue;
            }

            if (RequirementEvaluator.IsSatisfied(record.definition.trigger))
            {
                Enqueue(record);
            }
        }
    }

    /// <summary>
    /// Fires eventId unconditionally (trigger is not checked, regardless of triggerMode) -
    /// the same shape as STCSManager.Fire, for call sites that want "this exact thing just
    /// happened" semantics rather than reactive gating. No-op with a warning if eventId is
    /// unknown, already queued/playing, or already fired and not repeatable.
    /// </summary>
    public void Fire(string eventId)
    {
        if (!records.TryGetValue(eventId, out var record))
        {
            Debug.LogWarning($"[NarrativeEventManager] No NarrativeEvent found for '{eventId}'.");
            return;
        }

        if (record.isQueuedOrPlaying)
        {
            return;
        }

        if (!record.definition.repeatable && record.timesFired > 0)
        {
            Debug.LogWarning($"[NarrativeEventManager] '{eventId}' already fired and is not repeatable.");
            return;
        }

        Enqueue(record);
    }

    /// <summary>
    /// Whether eventId has completed playing at least once this session - the
    /// authoritative query RequirementType.NarrativeEventCompleted reads. False for an
    /// unknown eventId, never an exception - same "unresolved reads as not yet
    /// satisfied" convention every other requirement-backing query in this project
    /// follows.
    /// </summary>
    public bool IsCompleted(string eventId)
    {
        return !string.IsNullOrEmpty(eventId) && records.TryGetValue(eventId, out var record) && record.timesFired > 0;
    }

    private void Enqueue(NarrativeEventRecord record)
    {
        record.isQueuedOrPlaying = true;
        pending.Add(record);

        SessionLogger.Instance?.LogEvent(SessionEventType.NarrativeEventTriggered, record.definition.eventId, new Dictionary<string, string>
        {
            { "triggerMode", record.definition.triggerMode.ToString() },
            { "priority", record.definition.priority.ToString() }
        });

        TryPlayNext();
    }

    private void TryPlayNext()
    {
        // Professor/narrative guidance belongs to the investigation: events that become
        // eligible before the First Responder turns the scene over wait in the queue
        // (still logged as triggered) and play from HandleSceneTurnedOver.
        if (isPlaying || pending.Count == 0 || SceneTurnover.IsInvestigationBlocked)
        {
            return;
        }

        int bestIndex = 0;
        for (int i = 1; i < pending.Count; i++)
        {
            // Strictly greater, not >=, so the first-enqueued entry wins ties - a stable
            // priority order rather than an arbitrary one.
            if (pending[i].definition.priority > pending[bestIndex].definition.priority)
            {
                bestIndex = i;
            }
        }

        var record = pending[bestIndex];
        pending.RemoveAt(bestIndex);

        isPlaying = true;
        // A manual fixture completes past narrative gates without starting delayed
        // coroutines that could replay old lines over the procedure under test.
        if (SceneTestStart.IsPreparing)
        {
            record.timesFired++;
            record.isQueuedOrPlaying = false;
            OnNarrativeEventCompleted?.Invoke(record.definition.eventId);
            isPlaying = false;
            RefreshEligibility();
            TryPlayNext();
            return;
        }
        StartCoroutine(PlayRoutine(record));
    }

    private IEnumerator PlayRoutine(NarrativeEventRecord record)
    {
        var def = record.definition;

        if (def.delaySeconds > 0f)
        {
            yield return new WaitForSeconds(def.delaySeconds);
        }

        if (def.lineIds != null && def.lineIds.Length > 0)
        {
            // Catalog content (ViCSIDialogueCatalog): text, timing and priority come from there.
            if (!def.playAllLinesInSequence)
            {
                GameDialogue.SayLine(def.lineIds[Random.Range(0, def.lineIds.Length)], "Narrative", def.eventId, notificationUI);
            }
            else
            {
                for (int i = 0; i < def.lineIds.Length; i++)
                {
                    GameDialogue.SayLine(def.lineIds[i], "Narrative", def.eventId, notificationUI);
                    if (i < def.lineIds.Length - 1)
                    {
                        yield return new WaitForSeconds(lineDisplaySeconds);
                    }
                }
            }
        }
        else if (def.pool != null && def.pool.lines != null && def.pool.lines.Length > 0)
        {
            if (!def.playAllLinesInSequence)
            {
                ShowLine(def, def.pool.GetRandomLine());
            }
            else
            {
                for (int i = 0; i < def.pool.lines.Length; i++)
                {
                    ShowLine(def, def.pool.lines[i]);
                    if (i < def.pool.lines.Length - 1)
                    {
                        yield return new WaitForSeconds(lineDisplaySeconds);
                    }
                }
            }
        }

        PlayVoiceOver(def);

        record.timesFired++;
        record.isQueuedOrPlaying = false;
        isPlaying = false;

        OnNarrativeEventCompleted?.Invoke(def.eventId);

        // Retroactive re-check: this completion may satisfy another event's own trigger,
        // or a relationship/report-line/checkpoint's unlockRequirement via
        // RequirementType.NarrativeEventCompleted elsewhere in the project.
        RefreshEligibility();
        TryPlayNext();
    }

    private void ShowLine(NarrativeEvent def, string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        string text = string.IsNullOrEmpty(def.speaker) ? line : $"{def.speaker}: {line}";

        if (notificationUI != null)
        {
            // Spoken text stays prefix-free; Dialogue History keeps the speaker separately.
            notificationUI.ShowLine(line, def.speaker, "Narrative", def.eventId);
        }

        Debug.Log($"[Narrative] {text}");
    }

    private void PlayVoiceOver(NarrativeEvent def)
    {
        if (def.voiceOverClip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        audioSource.PlayOneShot(def.voiceOverClip);
    }
}
