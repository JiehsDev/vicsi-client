// Assets/_Project/Scripts/Objectives/ObjectiveStateManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton runtime tracker for every Objective in the active scenario - same
/// Instance/records shape as EvidenceStateManager/RelationshipStateManager/
/// NarrativeEventManager. Sits "above" those three: an objective's unlockRequirement/
/// completionRequirement/failureRequirement can reference evidence state, report lines,
/// fingerprint lab status, narrative-event completion, relationship completion, AND other
/// objectives (RequirementType.ObjectiveCompleted) all through the one shared evaluator -
/// so this is the one manager that listens broadly, across all three other systems'
/// change events, rather than just EvidenceStateManager's own.
/// </summary>
public class ObjectiveStateManager : MonoBehaviour
{
    public static ObjectiveStateManager Instance { get; private set; }

    [Tooltip("Every Objective used in this scenario - drag an ObjectiveSet asset here.")]
    [SerializeField] private ObjectiveSet objectiveSet;

    private readonly Dictionary<string, ObjectiveRecord> records = new();

    /// <summary>Fired on every real state change (never for a no-op re-set to the same state).</summary>
    public static event System.Action<string, ObjectiveState> OnObjectiveStateChanged;

    private void Awake()
    {
        Instance = this;

        if (objectiveSet == null || objectiveSet.objectives == null)
        {
            return;
        }

        foreach (var def in objectiveSet.objectives)
        {
            if (def == null || string.IsNullOrEmpty(def.objectiveId))
            {
                Debug.LogWarning("[ObjectiveStateManager] Skipped an Objective with no objectiveId.", this);
                continue;
            }

            if (records.ContainsKey(def.objectiveId))
            {
                Debug.LogWarning($"[ObjectiveStateManager] Duplicate objectiveId '{def.objectiveId}' - only the first registered instance is tracked.", this);
                continue;
            }

            records[def.objectiveId] = new ObjectiveRecord { definition = def };
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
        RelationshipStateManager.OnRelationshipStateChanged += HandleRelationshipStateChanged;
        NarrativeEventManager.OnNarrativeEventCompleted += HandleNarrativeEventCompleted;
        FindingStateManager.OnFindingConfirmed += HandleFindingConfirmed;
        SceneTurnover.OnPhaseChanged += HandlePhaseChanged;

        RefreshAll();
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
        RelationshipStateManager.OnRelationshipStateChanged -= HandleRelationshipStateChanged;
        NarrativeEventManager.OnNarrativeEventCompleted -= HandleNarrativeEventCompleted;
        FindingStateManager.OnFindingConfirmed -= HandleFindingConfirmed;
        SceneTurnover.OnPhaseChanged -= HandlePhaseChanged;
    }

    // Same "any relevant change, recheck everything pending" shape RelationshipStateManager
    // and NarrativeEventManager's own event handlers already use. FindingConfirmed was
    // missing here before this task - the mission-objective layer this task adds gates
    // objectives on findings (RequirementType.FindingConfirmed), which previously only got
    // re-checked incidentally on the next unrelated evidence event - see
    // FindingStateManager/InsightStateManager's own equivalent subscription.
    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus status) => RefreshAll();
    private void HandleReportLineSelected(string evidenceId, string lineId) => RefreshAll();
    private void HandleFingerprintLabStatusChanged(string evidenceId, FingerprintLabStatus status) => RefreshAll();
    // RequirementType.ScenePhotographyCompleted has no evidence event of its own.
    private void HandleScenePhotoProgress(string photoPointId) => RefreshAll();
    // RequirementType.MeasurementCompleted, same reason.
    private void HandleMeasurementProgress(string requirementId) => RefreshAll();
    private void HandleEvidenceSwabbed(string evidenceId) => RefreshAll();
    private void HandleEvidenceFingerprinted(string evidenceId) => RefreshAll();
    private void HandleRelationshipStateChanged(string relationshipId, RelationshipState state) => RefreshAll();
    private void HandleNarrativeEventCompleted(string eventId) => RefreshAll();
    private void HandleFindingConfirmed(string findingId) => RefreshAll();

    private void HandlePhaseChanged(ScenarioPhase phase) => RefreshAll();

    public ObjectiveRecord GetRecord(string objectiveId)
    {
        return records.TryGetValue(objectiveId, out var record) ? record : null;
    }

    /// <summary>Whether objectiveId has reached Completed - the authoritative query RequirementType.ObjectiveCompleted reads. False for an unknown id, never an exception.</summary>
    public bool IsCompleted(string objectiveId)
    {
        return !string.IsNullOrEmpty(objectiveId) && records.TryGetValue(objectiveId, out var record) && record.state == ObjectiveState.Completed;
    }

    /// <summary>Every tracked record, for editor tooling / a future objective-list UI. Read-only - do not mutate via this enumeration.</summary>
    public IEnumerable<ObjectiveRecord> AllRecords => records.Values;

    /// <summary>Phase group query for objective presentation and unlock evaluation.</summary>
    public bool IsAvailableInCurrentPhase(Objective objective)
    {
        if (objective == null) return false;
        if (!SceneTurnover.IsGated) return true;
        // Preserve the existing FR objective asset without reauthoring scenario content.
        if (objective.objectiveId == SceneTurnover.Controller.TurnoverObjectiveId)
            return SceneTurnover.IsPending;
        return objective.phaseScope switch
        {
            ObjectivePhaseScope.AnyPhase => true,
            ObjectivePhaseScope.ExactPhase => SceneTurnover.Phase == objective.requiredPhase,
            _ => !SceneTurnover.IsInvestigationBlocked
        };
    }

    /// <summary>
    /// Re-evaluates every objective and advances any whose current gate is now satisfied.
    /// Loops to a fixed point (bounded by a small safety cap) rather than a single pass,
    /// so a same-tick chain - objective A completing, unlocking objective B via a
    /// RequirementType.ObjectiveCompleted leaf in B's own unlockRequirement - resolves
    /// within one RefreshAll call instead of waiting for an unrelated later event to
    /// happen to trigger a second pass. The cap only guards against a genuinely
    /// misconfigured circular dependency (A depends on B depends on A); a normal chain
    /// of any real length settles in well under it, and a real cycle simply stops making
    /// progress on its own (see the "no changes this pass" break) long before the cap
    /// would matter.
    /// </summary>
    public void RefreshAll()
    {
        const int maxPasses = 20;

        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool anyChanged = false;

            foreach (var record in records.Values)
            {
                if (TryAdvance(record))
                {
                    anyChanged = true;
                }
            }

            if (!anyChanged)
            {
                break;
            }
        }
    }

    private bool TryAdvance(ObjectiveRecord record)
    {
        switch (record.state)
        {
            case ObjectiveState.Hidden:
                if (!IsAvailableInCurrentPhase(record.definition)) return false;
                if (!RequirementEvaluator.IsSatisfied(record.definition.unlockRequirement))
                {
                    return false;
                }

                SetState(record, ObjectiveState.Active);
                FireNarrativeSideEffect(record.definition.narrativeEventOnUnlockId);
                LogEvent(SessionEventType.ObjectiveUnlocked, record);
                return true;

            case ObjectiveState.Active:
                // Failure checked before completion: a scenario where both conditions
                // happen to be true in the same instant should read as the player having
                // broken the objective, not quietly completed it anyway.
                if (HasRealFailureCondition(record.definition.failureRequirement) && RequirementEvaluator.IsSatisfied(record.definition.failureRequirement))
                {
                    SetState(record, ObjectiveState.Failed);
                    LogEvent(SessionEventType.ObjectiveFailed, record);
                    return true;
                }

                if (RequirementEvaluator.IsSatisfied(record.definition.completionRequirement))
                {
                    SetState(record, ObjectiveState.Completed);
                    FireNarrativeSideEffect(record.definition.narrativeEventOnCompletionId);
                    LogEvent(SessionEventType.ObjectiveCompleted, record);
                    return true;
                }

                return false;

            default:
                // Completed/Failed are terminal - see ObjectiveState's own class comment.
                return false;
        }
    }

    /// <summary>
    /// Whether failureRequirement represents a real, authored condition rather than
    /// "none configured". A true C# null cannot be trusted to survive a save/load
    /// round-trip: Objective is a plain [Serializable] class (not a UnityEngine.Object),
    /// and Unity's serializer has no YAML representation for a null nested reference
    /// field - a null failureRequirement on a freshly-constructed Objective silently
    /// becomes a DEFAULT-CONSTRUCTED RequirementNode the moment the asset is saved and
    /// reloaded. Since RequirementNode's own default is now an empty ALL group
    /// (vacuously true - see RequirementNode's own class comment on why that default was
    /// chosen), that reload would make every objective's "no failure condition" silently
    /// become "always fails immediately". Confirmed as a real, live consequence during
    /// this project's own content-authoring pass, not a hypothetical - caught because
    /// several real, saved Objectives all showed Failed instead of Active/Completed the
    /// first time this content was actually played, despite none of them having a
    /// failureRequirement assigned in the authoring script.
    ///
    /// The fix: treat the trivially-empty ALL shape (Group/All/no children) - which is
    /// BOTH RequirementNode's true default AND what a null field degrades to on
    /// serialization - as "unconfigured", regardless of whether it arrived via a true
    /// in-memory null or a round-tripped default instance. A deliberately-authored empty
    /// ANY group (unusual, but valid, and always false) is NOT treated as unconfigured -
    /// only the specific default shape is.
    /// </summary>
    private static bool HasRealFailureCondition(RequirementNode node)
    {
        if (node == null)
        {
            return false;
        }

        bool isDefaultEmptyAll = node.kind == RequirementNodeKind.Group
            && node.operatorType == RequirementLogicalOperator.All
            && (node.children == null || node.children.Count == 0);

        return !isDefaultEmptyAll;
    }

    private void SetState(ObjectiveRecord record, ObjectiveState newState)
    {
        if (record.state == newState)
        {
            return;
        }

        record.state = newState;
        OnObjectiveStateChanged?.Invoke(record.definition.objectiveId, newState);
    }

    private static void FireNarrativeSideEffect(string narrativeEventId)
    {
        if (!string.IsNullOrEmpty(narrativeEventId))
        {
            NarrativeEventManager.Instance?.Fire(narrativeEventId);
        }
    }

    private void LogEvent(SessionEventType type, ObjectiveRecord record)
    {
        SessionLogger.Instance?.LogEvent(type, record.definition.objectiveId);
    }
}
