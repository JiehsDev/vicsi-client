// Assets/_Project/Scripts/Findings/FindingStateManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton runtime tracker for every FindingDefinition in the active scenario - same
/// Instance/Awake/Dictionary-of-records shape every other state manager in this project
/// already uses (EvidenceStateManager, RelationshipStateManager, ObjectiveStateManager,
/// NarrativeEventManager). Lives on the same `_Managers` GameObject as those, as a
/// sibling component - not DontDestroyOnLoad, not a separate GameObject, matching this
/// project's one-scene-lifetime-per-manager convention throughout.
///
/// WHERE THIS SITS IN THE WIDER CHAIN (infrastructure-only in this task):
///   Evidence status  - what the player physically DID (EvidenceStateManager)
///   Finding          - what was LEARNED as a result (THIS class)
///   Insight          - reasoning BETWEEN findings (not built yet)
///   Relationship     - the player's OWN deduction on the board (RelationshipStateManager)
///   Conclusion       - the higher-level interpretation (EndingManager)
/// A Finding is confirmed purely by its own unlockRequirement becoming satisfied - it
/// never reads the Deduction Board, never reads EndingManager, and nothing in either of
/// those reads Findings yet. This task adds the layer and nothing that consumes it.
///
/// FINDINGS ARE MONOTONIC, DELIBERATELY, same "once true, stays true" convention
/// EvidenceRecord.swabbingDone/fingerprintingDone and RelationshipStateManager's own
/// Locked-only-ever-unlocks-forward rule already follow: RefreshFindings only ever
/// promotes an unconfirmed record to confirmed, never the reverse, even if the
/// underlying requirement would no longer evaluate true on a later re-check (e.g. it
/// referenced a relationship that was later disconnected). A finding represents
/// knowledge the investigator has already established - that knowledge doesn't un-happen
/// because some contributing fact's live state later changed.
///
/// Broad event subscription (evidence, relationship, objective, narrative-event changes),
/// mirroring ObjectiveStateManager's own reasoning rather than RelationshipStateManager's
/// narrower one: a finding's unlockRequirement can reference any of
/// RequirementType.{EvidenceStatusAtOrAbove, ReportLineSelected,
/// FingerprintLabStatusAtOrAbove, EvidenceSwabbed, EvidenceFingerprinted,
/// EvidenceFlipped, EvidenceDiscovered, NarrativeEventCompleted, ObjectiveCompleted,
/// RelationshipCompleted, RequirementAssetReference} - the same full leaf set every
/// other requirement-gated system in this project already draws from, nothing new
/// invented here.
/// </summary>
public class FindingStateManager : MonoBehaviour
{
    public static FindingStateManager Instance { get; private set; }

    [Tooltip("Every FindingDefinition used in this scenario - drag every asset here, same convention as EvidenceStateManager.sceneEvidenceDefinitions/RelationshipStateManager.sceneRelationshipDefinitions.")]
    [SerializeField] private List<FindingDefinition> sceneFindingDefinitions;

    private readonly Dictionary<string, FindingRecord> records = new();

    /// <summary>Fired exactly once per finding, the moment it first becomes confirmed - never again for that finding, and never for a redundant RefreshFindings call that finds nothing new.</summary>
    public static event System.Action<string> OnFindingConfirmed;

    private void Awake()
    {
        Instance = this;

        if (sceneFindingDefinitions == null)
        {
            return;
        }

        foreach (var def in sceneFindingDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.findingId))
            {
                Debug.LogWarning("[FindingStateManager] Skipped a FindingDefinition with no findingId.", this);
                continue;
            }

            if (records.ContainsKey(def.findingId))
            {
                Debug.LogWarning($"[FindingStateManager] Duplicate findingId '{def.findingId}' - only the first registered instance is tracked.", this);
                continue;
            }

            records[def.findingId] = new FindingRecord { definition = def };
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
        ObjectiveStateManager.OnObjectiveStateChanged += HandleObjectiveStateChanged;
        NarrativeEventManager.OnNarrativeEventCompleted += HandleNarrativeEventCompleted;

        // Initial pass: any FindingDefinition whose unlockRequirement is already
        // satisfied at scene start (an empty ALL group, e.g.) should not have to wait
        // for the first evidence-state event to confirm.
        RefreshFindings();
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
        ObjectiveStateManager.OnObjectiveStateChanged -= HandleObjectiveStateChanged;
        NarrativeEventManager.OnNarrativeEventCompleted -= HandleNarrativeEventCompleted;
    }

    // Every handler below ignores its own arguments and re-checks everything not yet
    // confirmed - same "any relevant change, recheck everything pending" shape
    // RelationshipStateManager/ObjectiveStateManager's own event handlers already use.
    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus status) => RefreshFindings();
    private void HandleReportLineSelected(string evidenceId, string lineId) => RefreshFindings();
    private void HandleFingerprintLabStatusChanged(string evidenceId, FingerprintLabStatus status) => RefreshFindings();
    // RequirementType.ScenePhotographyCompleted has no evidence event of its own.
    private void HandleScenePhotoProgress(string photoPointId) => RefreshFindings();
    // RequirementType.MeasurementCompleted, same reason.
    private void HandleMeasurementProgress(string requirementId) => RefreshFindings();
    private void HandleEvidenceSwabbed(string evidenceId) => RefreshFindings();
    private void HandleEvidenceFingerprinted(string evidenceId) => RefreshFindings();
    private void HandleRelationshipStateChanged(string relationshipId, RelationshipState state) => RefreshFindings();
    private void HandleObjectiveStateChanged(string objectiveId, ObjectiveState state) => RefreshFindings();
    private void HandleNarrativeEventCompleted(string narrativeEventId) => RefreshFindings();

    public FindingRecord GetRecord(string findingId)
    {
        return records.TryGetValue(findingId, out var record) ? record : null;
    }

    /// <summary>Whether findingId is currently confirmed. False (never throws) for an unknown id, same fail-closed convention every other Is* query in this project follows.</summary>
    public bool IsConfirmed(string findingId)
    {
        return !string.IsNullOrEmpty(findingId) && records.TryGetValue(findingId, out var record) && record.isConfirmed;
    }

    /// <summary>Every currently-confirmed finding's id, for a future Findings UI or for DeductionScorer-style read-only passes. Read-only - do not mutate via this enumeration.</summary>
    public IEnumerable<string> GetConfirmedFindings()
    {
        foreach (var record in records.Values)
        {
            if (record.isConfirmed)
            {
                yield return record.definition.findingId;
            }
        }
    }

    /// <summary>Every tracked record, regardless of confirmation state - for a future Findings UI that wants to show locked entries too.</summary>
    public IEnumerable<FindingRecord> AllRecords => records.Values;

    /// <summary>
    /// Re-evaluates every still-unconfirmed record's unlockRequirement and promotes it
    /// to confirmed the moment it's satisfied - read-only against RequirementEvaluator,
    /// never mutates EvidenceStateManager/RelationshipStateManager/anything else. Safe
    /// to call redundantly (e.g. once per subscribed event) - a record already
    /// confirmed is skipped, matching the monotonic rule in the class comment, so a
    /// repeated call can never re-fire OnFindingConfirmed for the same finding.
    /// </summary>
    public void RefreshFindings()
    {
        foreach (var record in records.Values)
        {
            if (record.isConfirmed)
            {
                continue;
            }

            if (RequirementEvaluator.IsSatisfied(record.definition.unlockRequirement))
            {
                record.isConfirmed = true;
                LogEvent(record);
                OnFindingConfirmed?.Invoke(record.definition.findingId);
            }
        }
    }

    private void LogEvent(FindingRecord record)
    {
        SessionLogger.Instance?.LogEvent(SessionEventType.FindingConfirmed, record.definition.findingId, new Dictionary<string, string>
        {
            { "displayName", record.definition.displayName ?? string.Empty },
            { "sourceEvidenceId", record.definition.sourceEvidenceId ?? string.Empty }
        });
    }
}
