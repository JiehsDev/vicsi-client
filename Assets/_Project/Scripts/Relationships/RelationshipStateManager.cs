// Assets/_Project/Scripts/Relationships/RelationshipStateManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton runtime tracker for every RelationshipDefinition in the active scenario -
/// the relationship-system counterpart to EvidenceStateManager, same Instance/Awake/
/// Dictionary-of-records shape. Owns unlock re-evaluation (via RequirementEvaluator),
/// Selected/Completed transitions, and session logging for relationship events; never
/// touches EvidenceStateManager's own records and is never touched by it.
///
/// IMPORTANT: this class does not gate the physical drag-connect gesture on the board.
/// EvidenceBoardController's corkboard has no mechanism to refuse a connection between
/// two arbitrary cards, and this class doesn't add one - unlockRequirement/state describe
/// whether a candidate relationship is a legitimate, "ready" possibility for UI/narrative
/// purposes, not a runtime permission check on what the player is physically allowed to
/// draw. MarkSelectedForPair therefore registers a Selected connection for every matching
/// definition regardless of its current state, Locked included - the player really did
/// draw that line, and that fact belongs in the log unconditionally. See
/// EvidenceStateManager.TryReclaimMarker's own "log full history, not final snapshot"
/// rule for the same reasoning applied here.
///
/// States only ever move forward (Locked -> Available -> Selected -> Completed, with
/// Selected able to revert to Available on disconnect) - same "once true, stays true"
/// convention EvidenceRecord.swabbingDone/fingerprintingDone/selectedReportLineIds
/// already follow; RefreshUnlocks never re-locks a record that has already unlocked,
/// even if the requirement would no longer evaluate true.
/// </summary>
public class RelationshipStateManager : MonoBehaviour
{
    public static RelationshipStateManager Instance { get; private set; }

    [Tooltip("Every RelationshipDefinition used in this scenario - drag every asset here, same convention as EvidenceStateManager.sceneEvidenceDefinitions.")]
    [SerializeField] private List<RelationshipDefinition> sceneRelationshipDefinitions;

    private readonly Dictionary<string, RelationshipRecord> records = new();
    private readonly Dictionary<string, List<string>> pairIndex = new();

    /// <summary>Fired on every real state change (never for a no-op re-set to the same state).</summary>
    public static event System.Action<string, RelationshipState> OnRelationshipStateChanged;

    private void Awake()
    {
        Instance = this;
        EvidenceBoardController.ResetForSession();
        if (GetComponent<EvidenceRelationshipsReview>() == null) gameObject.AddComponent<EvidenceRelationshipsReview>();

        if (sceneRelationshipDefinitions == null)
        {
            return;
        }

        foreach (var def in sceneRelationshipDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.relationshipId))
            {
                Debug.LogWarning("[RelationshipStateManager] Skipped a RelationshipDefinition with no relationshipId.", this);
                continue;
            }

            if (records.ContainsKey(def.relationshipId))
            {
                Debug.LogWarning($"[RelationshipStateManager] Duplicate relationshipId '{def.relationshipId}' - only the first registered instance is tracked.", this);
                continue;
            }

            records[def.relationshipId] = new RelationshipRecord { definition = def };

            string pairKey = MakePairKey(def.sourceId, def.targetId);
            if (!pairIndex.TryGetValue(pairKey, out var idsForPair))
            {
                idsForPair = new List<string>();
                pairIndex[pairKey] = idsForPair;
            }
            idsForPair.Add(def.relationshipId);
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

        // Findings, not just raw evidence state, now directly gate several
        // RelationshipDefinition.unlockRequirement trees (RequirementType.FindingConfirmed
        // leaves) - see the Relationship Finding Migration task. Subscribing here directly,
        // rather than relying on RelationshipStateManager's raw-evidence handlers above
        // happening to re-check after FindingStateManager's own same-event handler has
        // already run, makes the dependency explicit and correct regardless of component
        // init order on _Managers.
        FindingStateManager.OnFindingConfirmed += HandleFindingConfirmed;

        // Initial pass: any RelationshipDefinition whose unlockRequirement is already
        // satisfied at scene start (an empty ALL group, e.g.) should not have to wait
        // for the first evidence-state event to unlock.
        RefreshUnlocks();
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
    }

    // Every handler below ignores its own arguments and re-checks everything not yet
    // unlocked - same "any relevant change, recheck everything pending" shape
    // HypothesisCheckpointManager's own event handlers already use, rather than trying
    // to figure out which specific records could possibly care about which specific
    // event (a RequirementNode tree can combine leaf types in ways that make a precise
    // per-event filter more complex than it's worth for a re-check this cheap).
    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus status) => RefreshUnlocks();
    private void HandleReportLineSelected(string evidenceId, string lineId) => RefreshUnlocks();
    private void HandleFingerprintLabStatusChanged(string evidenceId, FingerprintLabStatus status) => RefreshUnlocks();
    // RequirementType.ScenePhotographyCompleted has no evidence event of its own.
    private void HandleScenePhotoProgress(string photoPointId) => RefreshUnlocks();
    // RequirementType.MeasurementCompleted, same reason.
    private void HandleMeasurementProgress(string requirementId) => RefreshUnlocks();
    private void HandleEvidenceSwabbed(string evidenceId) => RefreshUnlocks();
    private void HandleEvidenceFingerprinted(string evidenceId) => RefreshUnlocks();
    private void HandleFindingConfirmed(string findingId) => RefreshUnlocks();

    public RelationshipRecord GetRecord(string relationshipId)
    {
        return records.TryGetValue(relationshipId, out var record) ? record : null;
    }

    /// <summary>Every tracked record, for DeductionScorer's own read-only pass at Submit Theory time. Read-only - do not mutate via this enumeration.</summary>
    public IEnumerable<RelationshipRecord> AllRecords => records.Values;

    /// <summary>
    /// Every RelationshipDefinition sharing the unordered pair (idA, idB) - order-
    /// independent, same MakePair convention EvidenceBoardController already uses for
    /// its own connection dictionary. May return more than one definition: several
    /// candidate relationships (only some marked isCorrect) can share the same pair -
    /// see the class comment and RelationshipDefinition's own comment.
    /// </summary>
    public IReadOnlyList<RelationshipDefinition> FindByPair(string idA, string idB)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || !pairIndex.TryGetValue(MakePairKey(idA, idB), out var ids))
        {
            return System.Array.Empty<RelationshipDefinition>();
        }

        var result = new List<RelationshipDefinition>(ids.Count);
        foreach (var id in ids)
        {
            var record = GetRecord(id);
            if (record?.definition != null)
            {
                result.Add(record.definition);
            }
        }
        return result;
    }

    /// <summary>
    /// Re-evaluates every still-Locked record's unlockRequirement and promotes it to
    /// Available the moment it's satisfied - read-only against RequirementEvaluator,
    /// never mutates EvidenceStateManager. Safe to call redundantly (e.g. once per
    /// subscribed event) - a record already past Locked is skipped, matching the
    /// "once true, stays true" rule in the class comment.
    /// </summary>
    public void RefreshUnlocks()
    {
        foreach (var record in records.Values)
        {
            if (record.state != RelationshipState.Locked)
            {
                continue;
            }

            if (RequirementEvaluator.IsSatisfied(record.definition.unlockRequirement))
            {
                // Visible and Available currently advance together - see RelationshipState's
                // own class comment on why both values still exist separately.
                SetState(record, RelationshipState.Available);
                LogEvent(SessionEventType.RelationshipUnlocked, record);
            }
        }
    }

    /// <summary>
    /// Registers a drawn connection between idA and idB: every RelationshipDefinition
    /// matching that pair (there may be zero, one, or several) is marked Selected, and -
    /// the first time only - a RelationshipAttempted event is logged and wasEverSelected
    /// is set permanently true. Called from EvidenceBoardController.TryConnect's "made a
    /// new connection" branch. See the class comment for why this does not check or
    /// require RelationshipState.Available first.
    /// </summary>
    public void MarkSelectedForPair(string idA, string idB)
    {
        foreach (var def in FindByPair(idA, idB))
        {
            if (EvidenceRelationshipsReview.Instance?.HasResponse(def.relationshipId) == true) continue;
            // Discovered-only witness pairs cannot be selected through a direct API
            // call before their statement is obtained. Other pairs retain their behavior.
            if (def.unlockRequirement?.requirementType == RequirementType.WitnessStatementDiscovered
                && !RequirementEvaluator.IsSatisfied(def.unlockRequirement)) continue;
            var record = GetRecord(def.relationshipId);
            if (record == null)
            {
                continue;
            }

            bool firstAttempt = !record.wasEverSelected;
            record.wasEverSelected = true;
            SetState(record, RelationshipState.Selected);

            if (firstAttempt)
            {
                LogEvent(SessionEventType.RelationshipAttempted, record);
            }
        }
    }

    /// <summary>
    /// The player pulled a drawn connection back apart. Reverts every matching
    /// definition's state from Selected back to Available (a legitimate pre-submission
    /// correction, same shape as EvidenceStateManager.TryReclaimMarker) - wasEverSelected
    /// and the original RelationshipAttempted log entry are NOT touched, per the class's
    /// "log full history" rule. Called from EvidenceBoardController.TryConnect's "broke
    /// an existing connection" branch.
    /// </summary>
    public void MarkDeselectedForPair(string idA, string idB)
    {
        foreach (var def in FindByPair(idA, idB))
        {
            if (EvidenceRelationshipsReview.Instance?.HasResponse(def.relationshipId) == true) continue;
            var record = GetRecord(def.relationshipId);
            if (record == null || record.state != RelationshipState.Selected)
            {
                continue;
            }

            SetState(record, RelationshipState.Available);
            LogEvent(SessionEventType.RelationshipRemoved, record);
        }
    }

    /// <summary>
    /// Finalizes every currently-Selected relationship as Completed - called once from
    /// EvidenceBoardController.SubmitTheory, the "log-and-evaluate-after-submission"
    /// moment. Does not itself score anything (see DeductionScorer.Score, which reads
    /// Completed/Selected state and each definition's isCorrect/scoreWeight); this only
    /// finalizes state and leaves a record of exactly what was connected at submission
    /// time.
    /// </summary>
    public void CompleteAllSelected()
    {
        foreach (var record in records.Values)
        {
            if (record.state == RelationshipState.Selected)
            {
                if (EvidenceRelationshipsReview.Instance?.HasResponse(record.definition.relationshipId) == true) continue;
                SetState(record, RelationshipState.Completed);
            }
        }
    }

    public void RecordInterpretation(string relationshipId, bool supported)
    {
        var record = GetRecord(relationshipId);
        if (record == null) return;
        record.wasEverSelected = true;
        SetState(record, supported ? RelationshipState.Completed : RelationshipState.Selected);
    }

    private void SetState(RelationshipRecord record, RelationshipState newState)
    {
        if (record.state == newState)
        {
            return;
        }

        record.state = newState;
        OnRelationshipStateChanged?.Invoke(record.definition.relationshipId, newState);
    }

    private void LogEvent(SessionEventType type, RelationshipRecord record)
    {
        SessionLogger.Instance?.LogEvent(type, record.definition.relationshipId, new Dictionary<string, string>
        {
            { "sourceId", record.definition.sourceId },
            { "targetId", record.definition.targetId },
            { "relationshipType", record.definition.relationshipType }
        });
    }

    // Order-independent, matching EvidenceBoardController.MakePair's own convention -
    // (A,B) and (B,A) must resolve to the same pair key.
    private static string MakePairKey(string idA, string idB)
    {
        return string.CompareOrdinal(idA, idB) <= 0 ? $"{idA}|{idB}" : $"{idB}|{idA}";
    }
}
