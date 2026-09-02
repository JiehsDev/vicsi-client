// Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outcome of a SetStatus/MarkX call, distinguished so a tool script can tell the
/// difference between "nothing to report" and "block this action and tell the
/// player why": Applied (the transition happened), Duplicate (a harmless
/// repeat/backward call - e.g. a proximity trigger re-firing, or a tool re-reporting
/// a step already completed - deliberately not logged as a violation), and Violation
/// (a required step was skipped, or the evidenceId isn't registered - blocked, and
/// logged clearly).
/// </summary>
public enum TransitionResult
{
    Applied,
    Duplicate,
    Violation
}

public class EvidenceStateManager : MonoBehaviour
{
    public static EvidenceStateManager Instance { get; private set; }

    [Tooltip("All evidence definitions used in this scenario — drag every EvidenceDefinition asset here.")]
    [SerializeField] private List<EvidenceDefinition> sceneEvidenceDefinitions;

    private readonly Dictionary<string, EvidenceRecord> records = new();

    // Anything (STCS, ProceduralGate, DeductionBoard, Logger) can subscribe to this
        public static event Action<string, EvidenceStatus> OnEvidenceStatusChanged;

    /// <summary>
    /// Fired when a real skip-violation is blocked (evidenceId, attemptedStatus,
    /// currentStatus) - a genuine attempt to skip a required step. Deliberately NOT
    /// fired for harmless duplicate/backward calls (see TransitionResult.Duplicate -
    /// those are expected and not diagnostically interesting), and not fired for an
    /// unregistered evidenceId (that's a data/config problem, already surfaced via
    /// the LogWarning above it, not a gameplay block).
    /// </summary>
    public static event Action<string, EvidenceStatus, EvidenceStatus> OnEvidenceTransitionBlocked;

    // Explicit, ordered required sequence for every evidence item's lifecycle -
    // written out independently of EvidenceStatus's own declaration order (even
    // though the two currently match) so a future reorder/insert on the enum can't
    // silently change gating behaviour with no compiler warning.
    private static readonly EvidenceStatus[] RequiredSequence =
    {
        EvidenceStatus.NotFound,
        EvidenceStatus.Found,
        EvidenceStatus.Photographed,
        EvidenceStatus.Sketched,
        EvidenceStatus.Logged,
        EvidenceStatus.ReadyForCollection,
        EvidenceStatus.Collected,
        EvidenceStatus.Processed
    };

    /// <summary>True only when target is exactly the next required status after current.</summary>
    public static bool IsValidNextStep(EvidenceStatus current, EvidenceStatus target)
    {
        int currentIndex = Array.IndexOf(RequiredSequence, current);
        int targetIndex = Array.IndexOf(RequiredSequence, target);
        return currentIndex >= 0 && targetIndex == currentIndex + 1;
    }

    /// <summary>What should happen next after current, or null if current is already the last step in the sequence.</summary>
    public static EvidenceStatus? GetNextRequiredStatus(EvidenceStatus current)
    {
        int currentIndex = Array.IndexOf(RequiredSequence, current);
        if (currentIndex < 0 || currentIndex + 1 >= RequiredSequence.Length)
        {
            return null;
        }
        return RequiredSequence[currentIndex + 1];
    }

    private void Awake()
    {
        Instance = this;

        foreach (var def in sceneEvidenceDefinitions)
        {
            records[def.evidenceId] = new EvidenceRecord { definition = def };
        }
    }

    public EvidenceRecord GetRecord(string evidenceId)
    {
        return records.TryGetValue(evidenceId, out var record) ? record : null;
    }

    public bool IsReadyForCollection(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        return record != null && record.status == EvidenceStatus.ReadyForCollection;
    }

    /// <summary>
    /// How many registered evidence items have reached at least threshold in the
    /// canonical sequence. A read-only aggregate so external systems
    /// (HypothesisCheckpointManager's count-threshold triggers) can ask "how much has
    /// the player actually gathered" without being handed the records dictionary.
    /// Compares positions in RequiredSequence rather than casting EvidenceStatus to
    /// int, for the same reason IsValidNextStep does: the enum's declaration order is
    /// not the authority here, RequiredSequence is, and a future enum edit must not
    /// silently change what "at or above" means.
    /// </summary>
    public int CountAtOrAbove(EvidenceStatus threshold)
    {
        int thresholdIndex = Array.IndexOf(RequiredSequence, threshold);
        if (thresholdIndex < 0)
        {
            Debug.LogWarning($"[EvidenceStateManager] CountAtOrAbove({threshold}): status is not in RequiredSequence; returning 0.");
            return 0;
        }

        int count = 0;
        foreach (var record in records.Values)
        {
            if (Array.IndexOf(RequiredSequence, record.status) >= thresholdIndex)
            {
                count++;
            }
        }
        return count;
    }

    // --- Transition methods: each one is called by the tool script that performed the action.
    // Return value lets the caller distinguish "nothing to report" from "blocked, tell the player
    // why" - see TransitionResult. Existing callers that ignore the return value are unaffected. ---

    public TransitionResult MarkFound(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Found, tool);
    public TransitionResult MarkPhotographed(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Photographed, tool);
    public TransitionResult MarkSketched(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Sketched, tool);

    public TransitionResult MarkLogged(string evidenceId, ToolType tool)
    {
        var result = SetStatus(evidenceId, EvidenceStatus.Logged, tool);
        CheckReadyForCollection(evidenceId);
        return result;
    }

    public TransitionResult MarkCollected(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Collected, tool);
    public TransitionResult MarkProcessed(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Processed, tool);

    private void CheckReadyForCollection(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null) return;

        // For MVP: "ready" once Logged has happened (which implies Photographed + Sketched
        // already occurred, since SetStatus below now enforces that order on the way in).
        if (record.status == EvidenceStatus.Logged)
        {
            SetStatus(evidenceId, EvidenceStatus.ReadyForCollection, record.lastToolUsed);
        }
    }

    private TransitionResult SetStatus(string evidenceId, EvidenceStatus newStatus, ToolType tool)
    {
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (!IsValidNextStep(record.status, newStatus))
        {
            int currentIndex = Array.IndexOf(RequiredSequence, record.status);
            int targetIndex = Array.IndexOf(RequiredSequence, newStatus);

            if (targetIndex <= currentIndex)
            {
                // Duplicate or backward call - harmless (a proximity trigger re-firing,
                // a tool re-reporting a step already completed). Expected, happens often,
                // and deliberately NOT logged as a violation.
                return TransitionResult.Duplicate;
            }

            // A required step was skipped - block it, leave record.status untouched.
            var nextRequired = GetNextRequiredStatus(record.status);
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {record.status} -> {newStatus} skips a required step (needs {nextRequired} first).");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        record.status = newStatus;
        record.lastToolUsed = tool;
        record.statusChangedAtTime = Time.time;

        OnEvidenceStatusChanged?.Invoke(evidenceId, newStatus);
        Debug.Log($"[EvidenceStateManager] {evidenceId} → {newStatus} (via {tool})");
        return TransitionResult.Applied;
    }
}