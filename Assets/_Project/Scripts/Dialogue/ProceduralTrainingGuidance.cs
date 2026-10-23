using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Observes completed training milestones; never changes evidence state, gates or scores.
/// Content and presentation are authored in the existing dialogue catalog. A new scene
/// or logger session starts a fresh set of lessons, independent of assessment payloads.
/// </summary>
public sealed class ProceduralTrainingGuidance : MonoBehaviour
{
    public static ProceduralTrainingGuidance Instance { get; private set; }
    public const string VendorLeadId = "LEAD-FINGERPRINT-VENDOR";
    // This post-assessment sequence must not wait indefinitely at a nearby
    // evidence action prompt. Immediate tooltips and modal input still take priority.
    public static bool IsReadyVendorGuidance(string id) => (id == "TRAIN-FINGERPRINT" || id == VendorLeadId)
        && VendorFollowupInterview.Instance?.FollowupUnlocked == true;
    private readonly HashSet<string> scheduled = new();
    private readonly Queue<string> pending = new();
    private EvidenceInventory inventory;
    private string sessionId;
    public int PendingCount => pending.Count;
    public bool HasScheduled(string id) => scheduled.Contains(id);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        SceneManager.sceneLoaded -= SceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
        EnsureOwner();
    }

    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureOwner();
    private static void EnsureOwner()
    {
        if (Instance != null || EvidenceStateManager.Instance == null || STCSNotificationUI.Instance == null) return;
        new GameObject("Procedural Training Guidance").AddComponent<ProceduralTrainingGuidance>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        sessionId = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
    }

    private void OnEnable()
    {
        SceneTurnover.OnPhaseChanged += PhaseChanged;
        ScenePhotographyManager.OnScenePhotoProgress += PhotographyProgress;
        EvidenceStateManager.OnEvidenceStatusChanged += StatusChanged;
        EvidenceStateManager.OnEvidenceFingerprinted += Fingerprinted;
        MeasurementStateManager.OnMeasurementProgress += MeasurementProgress;
        SwabTool.OnSampleContained += SampleContained;
        inventory = EvidenceInventory.Instance;
        if (inventory != null) inventory.OnCustodyChanged += CustodyChanged;
    }

    private void Start() => PhaseChanged(SceneTurnover.Phase);
    private void OnDisable()
    {
        SceneTurnover.OnPhaseChanged -= PhaseChanged;
        ScenePhotographyManager.OnScenePhotoProgress -= PhotographyProgress;
        EvidenceStateManager.OnEvidenceStatusChanged -= StatusChanged;
        EvidenceStateManager.OnEvidenceFingerprinted -= Fingerprinted;
        MeasurementStateManager.OnMeasurementProgress -= MeasurementProgress;
        SwabTool.OnSampleContained -= SampleContained;
        if (inventory != null) inventory.OnCustodyChanged -= CustodyChanged;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Queue a catalog lesson once. Repeated actions cannot grow the queue.</summary>
    public static void Schedule(string id)
    {
        var owner = Instance;
        if (owner == null || !owner.isActiveAndEnabled) return;
        owner.CheckSession();
        if (!owner.scheduled.Add(id)) return;
        if (SceneTestStart.IsPreparing) return;
        // Preserve closeout's existing dialogue timing/queue ownership: its short
        // phase-entry explanation precedes the review/survey affordance as before.
        if (IsStageLesson(id))
        {
            GameDialogue.SayLine(id, "Training", id);
            return;
        }
        owner.pending.Enqueue(id);
    }

    private void CheckSession()
    {
        string current = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
        if (current == sessionId) return;
        sessionId = current;
        scheduled.Clear();
        pending.Clear();
        PhaseChanged(SceneTurnover.Phase);
    }

    private void LateUpdate()
    {
        CheckSession();
        // Derived from the existing locked response and confirmed finding. No second
        // unlock flag or assessment policy: every valid response earns the same lead.
        if (VendorFollowupInterview.Instance?.FollowupUnlocked == true && !VendorFollowupInterview.Instance.IsDiscovered)
        {
            Schedule("TRAIN-FINGERPRINT");
            Schedule(VendorLeadId);
        }
        if (pending.Count == 0 || PlayerUIGate.IsBlocked) return;
        var upper = STCSNotificationUI.Instance;
        var lower = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
        if (upper == null || !upper.isActiveAndEnabled || upper.IsShowing || upper.PendingCount > 0) return;
        if (lower != null && !string.IsNullOrEmpty(lower.CurrentTooltip)) return;
        // An assessment snapshot precedes explanations that teach its answer.
        // Rotate held lessons without dropping them or starving unrelated concepts.
        for (int remaining = pending.Count; remaining > 0; remaining--)
        {
            if (ReasoningCheckpointManager.Instance?.HoldsTraining(pending.Peek()) != true) break;
            pending.Enqueue(pending.Dequeue());
        }
        string id = pending.Peek();
        if (id == VendorLeadId && VendorFollowupInterview.Instance?.IsDiscovered == true)
        { pending.Dequeue(); return; }
        if (ReasoningCheckpointManager.Instance?.HoldsTraining(id) == true) return;
        if (lower != null && lower.HasAmbientPrompt && !IsReadyVendorGuidance(id)) return;
        pending.Dequeue();
        GameDialogue.SayLine(id, "Training", id);
    }

    private static bool IsStageLesson(string id) => id == "closeout-start" || id == "closeout-survey-guidance";

    private void PhaseChanged(ScenarioPhase phase)
    {
        if (phase == ScenarioPhase.SOCOInvestigation) Schedule("TRAIN-SOCO-TEAM");
        if (phase == ScenarioPhase.SOCOCloseout) Schedule("closeout-start");
        if (phase == ScenarioPhase.FinalSceneSurvey) Schedule("closeout-survey-guidance");
    }
    private void PhotographyProgress(string id)
    {
        if (ScenePhotographyManager.Instance != null && ScenePhotographyManager.Instance.IsScenePhotographyComplete)
            Schedule("TRAIN-PHOTOGRAPHY");
    }
    private void StatusChanged(string id, EvidenceStatus status)
    {
        var definition = EvidenceStateManager.Instance?.GetRecord(id)?.definition;
        if (definition == null) return;
        if (status == EvidenceStatus.Marked)
        {
            Schedule("TRAIN-MARKER");
            if (definition.requiresTraceMarker) Schedule("TRAIN-TRACE-SOURCE");
        }
        if (status == EvidenceStatus.Sealed && !definition.collectedBySwab && !definition.examinedInPlace)
            Schedule("TRAIN-PACKAGING");
    }
    private void Fingerprinted(string id) => Schedule("TRAIN-FINGERPRINT");
    private void MeasurementProgress(string id)
    {
        if (!string.IsNullOrEmpty(id) && MeasurementStateManager.Instance?.GetRecord(id)?.IsComplete == true)
            Schedule("TRAIN-MEASUREMENT");
    }
    private void SampleContained(string id) => Schedule("TRAIN-BIOLOGICAL-SAMPLE");
    private void CustodyChanged(string id, EvidenceCustodyLocation previous, EvidenceCustodyLocation next)
    {
        if (previous == EvidenceCustodyLocation.Held && next == EvidenceCustodyLocation.Inventory
            && (inventory.IsExplicitlyStoredSample(id) || inventory.IsExplicitlyStoredBag(id)))
            Schedule("TRAIN-CUSTODY");
    }
}
