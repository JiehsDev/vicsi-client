// Assets/_Project/Scripts/Insights/InsightStateManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton runtime tracker for every InsightDefinition in the active scenario - same
/// Instance/Awake/Dictionary-of-records shape every other state manager in this project
/// already uses (EvidenceStateManager, RelationshipStateManager, ObjectiveStateManager,
/// NarrativeEventManager, FindingStateManager). Lives on the same `_Managers`
/// GameObject as those, as a sibling component - not DontDestroyOnLoad.
///
/// WHERE THIS SITS IN THE WIDER CHAIN:
///   Evidence status  - what the player physically DID (EvidenceStateManager)
///   Finding          - what was LEARNED as a result (FindingStateManager)
///   Insight          - REASONING generated FROM one or more findings (THIS class)
///   Relationship     - the player's OWN deduction on the board (RelationshipStateManager)
///   Conclusion       - the higher-level interpretation (EndingManager)
/// An Insight is unlocked purely by its own unlockRequirement becoming satisfied - it
/// never reads the Deduction Board and nothing on the board reads Insights yet (see
/// this task's own explicit scope: relationship requirements/Submit Theory/Submit
/// Conclusion/endings/scoring are all untouched).
///
/// ORDER INDEPENDENCE, BY CONSTRUCTION, NOT BY SPECIAL-CASING: RefreshInsights()
/// re-evaluates every still-locked insight's FULL requirement tree from scratch on
/// every call - there is no "when finding X unlocks, check insight Y" per-finding
/// routing anywhere in this class. Whichever of two findings a multi-finding insight
/// depends on happens to confirm LAST, that finding's own OnFindingConfirmed event
/// triggers the SAME RefreshInsights() sweep that would have run either way, and
/// RequirementEvaluator reads current live state (FindingStateManager.IsConfirmed)
/// fresh each time - so "knife first" and "gloves first" reach the exact same unlocked
/// state through the exact same code path. This mirrors RelationshipStateManager/
/// ObjectiveStateManager/FindingStateManager's own "any relevant change, recheck
/// everything pending" idiom exactly.
///
/// INSIGHTS ARE MONOTONIC, same "once true, stays true" convention as Findings/
/// Relationships - RefreshInsights only ever promotes a locked record to unlocked,
/// never the reverse.
/// </summary>
public class InsightStateManager : MonoBehaviour
{
    public static InsightStateManager Instance { get; private set; }

    [Tooltip("Every InsightDefinition used in this scenario - drag every asset here, same convention as FindingStateManager.sceneFindingDefinitions.")]
    [SerializeField] private List<InsightDefinition> sceneInsightDefinitions;

    private readonly Dictionary<string, InsightRecord> records = new();

    /// <summary>Fired exactly once per insight, the moment it first becomes unlocked - never again for that insight.</summary>
    public static event System.Action<string> OnInsightUnlocked;

    private void Awake()
    {
        Instance = this;

        if (sceneInsightDefinitions == null)
        {
            return;
        }

        foreach (var def in sceneInsightDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.insightId))
            {
                Debug.LogWarning("[InsightStateManager] Skipped an InsightDefinition with no insightId.", this);
                continue;
            }

            if (records.ContainsKey(def.insightId))
            {
                Debug.LogWarning($"[InsightStateManager] Duplicate insightId '{def.insightId}' - only the first registered instance is tracked.", this);
                continue;
            }

            records[def.insightId] = new InsightRecord { definition = def };
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
        // Findings are the primary driver - see class comment. The rest are subscribed
        // for the same reason FindingStateManager itself listens broadly: an insight's
        // unlockRequirement is a RequirementNode and could in principle reference
        // anything the shared evaluator supports, not only FindingConfirmed leaves.
        FindingStateManager.OnFindingConfirmed += HandleFindingConfirmed;
        EvidenceStateManager.OnEvidenceStatusChanged += HandleGenericChange;
        EvidenceStateManager.OnReportLineSelected += HandleGenericChange;
        EvidenceStateManager.OnFingerprintLabStatusChanged += HandleGenericChange;
        ScenePhotographyManager.OnScenePhotoProgress += HandleScenePhotoProgress;
        MeasurementStateManager.OnMeasurementProgress += HandleMeasurementProgress;
        EvidenceStateManager.OnEvidenceSwabbed += HandleGenericChange;
        EvidenceStateManager.OnEvidenceFingerprinted += HandleGenericChange;
        RelationshipStateManager.OnRelationshipStateChanged += HandleGenericChange;
        ObjectiveStateManager.OnObjectiveStateChanged += HandleGenericChange;
        NarrativeEventManager.OnNarrativeEventCompleted += HandleGenericChange;

        // Initial pass: any InsightDefinition whose unlockRequirement is already
        // satisfied at scene start should not have to wait for the first event.
        RefreshInsights();
    }

    private void OnDisable()
    {
        FindingStateManager.OnFindingConfirmed -= HandleFindingConfirmed;
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleGenericChange;
        EvidenceStateManager.OnReportLineSelected -= HandleGenericChange;
        EvidenceStateManager.OnFingerprintLabStatusChanged -= HandleGenericChange;
        ScenePhotographyManager.OnScenePhotoProgress -= HandleScenePhotoProgress;
        MeasurementStateManager.OnMeasurementProgress -= HandleMeasurementProgress;
        EvidenceStateManager.OnEvidenceSwabbed -= HandleGenericChange;
        EvidenceStateManager.OnEvidenceFingerprinted -= HandleGenericChange;
        RelationshipStateManager.OnRelationshipStateChanged -= HandleGenericChange;
        ObjectiveStateManager.OnObjectiveStateChanged -= HandleGenericChange;
        NarrativeEventManager.OnNarrativeEventCompleted -= HandleGenericChange;
    }

    // The one place a just-confirmed finding's id is threaded through, purely so
    // InsightRecord.triggeringFindingId can record it - see that field's own comment.
    // This is NOT per-finding routing logic (no "if findingId == X, check insight Y"
    // anywhere) - every call still re-evaluates every locked insight's full tree.
    private void HandleFindingConfirmed(string findingId) => RefreshInsights(findingId);

    // Every other subscribed event ignores its own arguments and just re-checks
    // everything not yet unlocked - same shape RelationshipStateManager/
    // ObjectiveStateManager/FindingStateManager's own handlers already use.
    private void HandleGenericChange(string a, EvidenceStatus b) => RefreshInsights();
    private void HandleGenericChange(string a, string b) => RefreshInsights();
    private void HandleGenericChange(string a, FingerprintLabStatus b) => RefreshInsights();
    // RequirementType.ScenePhotographyCompleted has no evidence event of its own.
    private void HandleScenePhotoProgress(string photoPointId) => RefreshInsights();
    // RequirementType.MeasurementCompleted, same reason.
    private void HandleMeasurementProgress(string requirementId) => RefreshInsights();
    private void HandleGenericChange(string a) => RefreshInsights();
    private void HandleGenericChange(string a, RelationshipState b) => RefreshInsights();
    private void HandleGenericChange(string a, ObjectiveState b) => RefreshInsights();

    public InsightRecord GetRecord(string insightId)
    {
        return records.TryGetValue(insightId, out var record) ? record : null;
    }

    /// <summary>Whether insightId is currently unlocked. False (never throws) for an unknown id.</summary>
    public bool IsUnlocked(string insightId)
    {
        return !string.IsNullOrEmpty(insightId) && records.TryGetValue(insightId, out var record) && record.isUnlocked;
    }

    /// <summary>Every currently-unlocked insight's definition, in no particular order - for a future/present UI to read. Read-only - do not mutate via this enumeration. Never includes a locked insight - see class comment on why locked insights must never be exposed.</summary>
    public IEnumerable<InsightDefinition> GetUnlockedInsights()
    {
        foreach (var record in records.Values)
        {
            if (record.isUnlocked)
            {
                yield return record.definition;
            }
        }
    }

    /// <summary>Every tracked record, regardless of unlock state - for a future debug view only. Read-only.</summary>
    public IEnumerable<InsightRecord> AllRecords => records.Values;

    /// <summary>
    /// Re-evaluates every still-locked record's unlockRequirement and promotes it to
    /// unlocked the moment it's satisfied - read-only against RequirementEvaluator,
    /// never mutates any other manager. Safe to call redundantly - an already-unlocked
    /// record is skipped, so a repeated call can never re-fire OnInsightUnlocked for
    /// the same insight. triggeringFindingId is purely descriptive (see
    /// InsightRecord's own comment) - passing it or leaving it null never changes
    /// which insights unlock or in what order they're evaluated.
    /// </summary>
    public void RefreshInsights(string triggeringFindingId = null)
    {
        foreach (var record in records.Values)
        {
            if (record.isUnlocked)
            {
                continue;
            }

            if (RequirementEvaluator.IsSatisfied(record.definition.unlockRequirement))
            {
                record.isUnlocked = true;
                record.triggeringFindingId = triggeringFindingId;
                LogEvent(record);

                // Short, generic, no raw id - see this feature's own explicit
                // "do not repeatedly display the full paragraph as a popup" rule.
                GameDialogue.Say("insight-added");

                OnInsightUnlocked?.Invoke(record.definition.insightId);
            }
        }
    }

    private void LogEvent(InsightRecord record)
    {
        SessionLogger.Instance?.LogEvent(SessionEventType.InsightUnlocked, record.definition.insightId, new Dictionary<string, string>
        {
            { "displayName", record.definition.displayName ?? string.Empty },
            { "triggeringFindingId", record.triggeringFindingId ?? string.Empty }
        });
    }
}
