// Assets/_Project/Scripts/Conclusions/ConclusionStateManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton runtime tracker for every ConclusionDefinition in the active scenario - same
/// Instance/Awake/Dictionary-of-records shape every other state manager in this project
/// already uses (FindingStateManager, InsightStateManager, RelationshipStateManager).
/// Lives on the same `_Managers` GameObject as those, as a sibling component - not
/// DontDestroyOnLoad.
///
/// WHERE THIS SITS IN THE WIDER CHAIN:
///   Evidence procedure -> Finding -> Insight -> Relationship -> ConclusionDefinition (THIS
///   class) -> (a later task) Submit Conclusion -> Ending.
/// A conclusion becomes Available purely by its own availabilityRequirement being
/// satisfied - it never reads or writes anything about the EXISTING EndingSet/EndingManager
/// picker/resolution flow, which this task deliberately does not touch. Nothing here is
/// shown to the player yet (see IMPORTANT below).
///
/// IMPORTANT - NOT PLAYER-FACING YET: GetAvailableConclusions/IsAvailable exist purely for
/// debug/Inspector/test inspection this pass. No UI in this project reads them. Do not
/// wire this into the Submit Conclusion picker, TrySubmitConclusion, or ResolveEnding
/// without a dedicated follow-up task - see this feature's own explicit scope.
///
/// AVAILABILITY IS EVALUATED BY RE-CHECKING EVERY REQUIREMENT TREE FRESH, same
/// order-independence-by-construction idiom InsightStateManager/FindingStateManager/
/// RelationshipStateManager already use - there is no per-finding or per-relationship
/// routing anywhere in this class. Whichever prerequisite happens to complete LAST,
/// its own event triggers the same RefreshConclusions() sweep that would have run either
/// way, and RequirementEvaluator reads current live state fresh each time.
///
/// AVAILABILITY IS MONOTONIC, same "once true, stays true" convention as Findings/
/// Insights/Relationships - RefreshConclusions only ever promotes a record to available,
/// never the reverse.
/// </summary>
public class ConclusionStateManager : MonoBehaviour
{
    public static ConclusionStateManager Instance { get; private set; }

    [Tooltip("Every ConclusionDefinition used in this scenario - drag every asset here, same convention as FindingStateManager.sceneFindingDefinitions.")]
    [SerializeField] private List<ConclusionDefinition> sceneConclusionDefinitions;

    private readonly Dictionary<string, ConclusionRecord> records = new();

    /// <summary>Fired exactly once per conclusion, the moment it first becomes available - never again for that conclusion.</summary>
    public static event System.Action<string> OnConclusionAvailable;

    private void Awake()
    {
        Instance = this;

        if (sceneConclusionDefinitions == null)
        {
            return;
        }

        foreach (var def in sceneConclusionDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.conclusionId))
            {
                Debug.LogWarning("[ConclusionStateManager] Skipped a ConclusionDefinition with no conclusionId.", this);
                continue;
            }

            if (records.ContainsKey(def.conclusionId))
            {
                Debug.LogWarning($"[ConclusionStateManager] Duplicate conclusionId '{def.conclusionId}' - only the first registered instance is tracked.", this);
                continue;
            }

            records[def.conclusionId] = new ConclusionRecord { definition = def };
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
        // Findings and completed Relationships are the primary drivers - see class
        // comment. The rest are subscribed for the same reason InsightStateManager/
        // FindingStateManager subscribe broadly: an availabilityRequirement is a
        // RequirementNode and could in principle reference anything the shared evaluator
        // supports, not only FindingConfirmed/RelationshipCompleted leaves.
        FindingStateManager.OnFindingConfirmed += HandleGenericChange;
        InsightStateManager.OnInsightUnlocked += HandleGenericChange;
        RelationshipStateManager.OnRelationshipStateChanged += HandleGenericChange;
        EvidenceStateManager.OnEvidenceStatusChanged += HandleGenericChange;
        EvidenceStateManager.OnReportLineSelected += HandleGenericChange;
        EvidenceStateManager.OnFingerprintLabStatusChanged += HandleGenericChange;
        ScenePhotographyManager.OnScenePhotoProgress += HandleScenePhotoProgress;
        MeasurementStateManager.OnMeasurementProgress += HandleMeasurementProgress;
        ObjectiveStateManager.OnObjectiveStateChanged += HandleGenericChange;
        NarrativeEventManager.OnNarrativeEventCompleted += HandleGenericChange;

        // Initial pass: any ConclusionDefinition whose availabilityRequirement is already
        // satisfied at scene start should not have to wait for the first event.
        RefreshConclusions();
    }

    private void OnDisable()
    {
        FindingStateManager.OnFindingConfirmed -= HandleGenericChange;
        InsightStateManager.OnInsightUnlocked -= HandleGenericChange;
        RelationshipStateManager.OnRelationshipStateChanged -= HandleGenericChange;
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleGenericChange;
        EvidenceStateManager.OnReportLineSelected -= HandleGenericChange;
        EvidenceStateManager.OnFingerprintLabStatusChanged -= HandleGenericChange;
        ScenePhotographyManager.OnScenePhotoProgress -= HandleScenePhotoProgress;
        MeasurementStateManager.OnMeasurementProgress -= HandleMeasurementProgress;
        ObjectiveStateManager.OnObjectiveStateChanged -= HandleGenericChange;
        NarrativeEventManager.OnNarrativeEventCompleted -= HandleGenericChange;
    }

    // Every subscribed event ignores its own arguments and just re-checks everything not
    // yet available - same shape InsightStateManager/RelationshipStateManager/
    // FindingStateManager's own handlers already use, resolved via C# method-group
    // overload resolution against each event's distinct delegate type.
    private void HandleGenericChange(string a) => RefreshConclusions();
    private void HandleGenericChange(string a, EvidenceStatus b) => RefreshConclusions();
    private void HandleGenericChange(string a, string b) => RefreshConclusions();
    private void HandleGenericChange(string a, FingerprintLabStatus b) => RefreshConclusions();
    // RequirementType.ScenePhotographyCompleted has no evidence event of its own.
    private void HandleScenePhotoProgress(string photoPointId) => RefreshConclusions();
    // RequirementType.MeasurementCompleted, same reason.
    private void HandleMeasurementProgress(string requirementId) => RefreshConclusions();
    private void HandleGenericChange(string a, RelationshipState b) => RefreshConclusions();
    private void HandleGenericChange(string a, ObjectiveState b) => RefreshConclusions();

    public ConclusionRecord GetRecord(string conclusionId)
    {
        return records.TryGetValue(conclusionId, out var record) ? record : null;
    }

    /// <summary>Whether conclusionId is currently available. False (never throws) for an unknown id.</summary>
    public bool IsAvailable(string conclusionId)
    {
        return !string.IsNullOrEmpty(conclusionId) && records.TryGetValue(conclusionId, out var record) && record.isAvailable;
    }

    /// <summary>Every currently-available conclusion's definition, in no particular order - debug/test inspection only, see class comment. Never includes an unavailable conclusion.</summary>
    public IEnumerable<ConclusionDefinition> GetAvailableConclusions()
    {
        foreach (var record in records.Values)
        {
            if (record.isAvailable)
            {
                yield return record.definition;
            }
        }
    }

    /// <summary>Every tracked record, regardless of availability - debug/test inspection only.</summary>
    public IEnumerable<ConclusionRecord> AllRecords => records.Values;

    /// <summary>
    /// Re-evaluates every still-unavailable record's availabilityRequirement and promotes
    /// it to available the moment it's satisfied - read-only against RequirementEvaluator,
    /// never mutates any other manager. Safe to call redundantly - an already-available
    /// record is skipped, so a repeated call can never re-fire OnConclusionAvailable for
    /// the same conclusion.
    /// </summary>
    public void RefreshConclusions()
    {
        foreach (var record in records.Values)
        {
            if (record.isAvailable)
            {
                continue;
            }

            if (RequirementEvaluator.IsSatisfied(record.definition.availabilityRequirement))
            {
                record.isAvailable = true;
                OnConclusionAvailable?.Invoke(record.definition.conclusionId);
            }
        }
    }
}
