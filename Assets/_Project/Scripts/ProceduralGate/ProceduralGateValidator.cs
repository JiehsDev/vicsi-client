// Assets/_Project/Scripts/ProceduralGate/ProceduralGateValidator.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One log entry for a single evidence status transition, kept in memory for
/// later procedural-compliance scoring. Phase 8 will mirror these into the
/// Supabase evidence_events table.
/// </summary>
[System.Serializable]
public struct ProceduralGateLogEntry
{
    public string evidenceId;
    public EvidenceStatus status;
    public ToolType tool;
    public float time;
}

/// <summary>
/// Generic gate for any evidence status transition, not just "collect" - anything
/// that wants to perform a transition (photograph, sketch, log, collect, ...) should
/// check CanTransition() first and use GetBlockReason() to explain why it's blocked.
/// Delegates the actual sequence knowledge to EvidenceStateManager's
/// IsValidNextStep/GetNextRequiredStatus (see EvidenceStateManager.RequiredSequence)
/// so there is exactly one place the required order is defined - this class used to
/// carry its own hand-written copy of that order as English strings (ReasonFor), which
/// could silently drift from the real sequence; that's gone now.
/// </summary>
public class ProceduralGateValidator : MonoBehaviour
{
    public static ProceduralGateValidator Instance { get; private set; }

    private readonly List<ProceduralGateLogEntry> eventLog = new();

    /// <summary>Running log of every evidence status transition seen so far.</summary>
    public IReadOnlyList<ProceduralGateLogEntry> EventLog => eventLog;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleEvidenceStatusChanged;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleEvidenceStatusChanged;
    }

    /// <summary>Whether the given evidence item may currently transition to target (i.e. target is exactly the next required step).</summary>
    public bool CanTransition(string evidenceId, EvidenceStatus target)
    {
        if (EvidenceStateManager.Instance == null)
        {
            return false;
        }

        var record = EvidenceStateManager.Instance.GetRecord(evidenceId);
        return record != null && EvidenceStateManager.IsValidNextStep(record.status, target);
    }

    /// <summary>Backward-compatible convenience wrapper - equivalent to CanTransition(evidenceId, EvidenceStatus.Collected).</summary>
    public bool CanCollect(string evidenceId) => CanTransition(evidenceId, EvidenceStatus.Collected);

    /// <summary>
    /// Short human-readable reason CanTransition(evidenceId, target) is currently false, e.g.
    /// "Not yet Photographed." Returns null if the transition is currently valid.
    /// </summary>
    public string GetBlockReason(string evidenceId, EvidenceStatus target)
    {
        if (EvidenceStateManager.Instance == null)
        {
            return "Evidence system not available.";
        }

        var record = EvidenceStateManager.Instance.GetRecord(evidenceId);
        if (record == null)
        {
            return "Unknown evidence item.";
        }

        if (EvidenceStateManager.IsValidNextStep(record.status, target))
        {
            return null;
        }

        var nextRequired = EvidenceStateManager.GetNextRequiredStatus(record.status);
        return nextRequired.HasValue ? $"Not yet {nextRequired}." : "No further steps required.";
    }

    /// <summary>Backward-compatible convenience wrapper - equivalent to GetBlockReason(evidenceId, EvidenceStatus.Collected).</summary>
    public string GetBlockReason(string evidenceId) => GetBlockReason(evidenceId, EvidenceStatus.Collected);

    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus newStatus)
    {
        ToolType tool = ToolType.None;
        var record = EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(evidenceId)
            : null;
        if (record != null)
        {
            tool = record.lastToolUsed;
        }

        eventLog.Add(new ProceduralGateLogEntry
        {
            evidenceId = evidenceId,
            status = newStatus,
            tool = tool,
            time = Time.time
        });

        Debug.Log($"[ProceduralGateValidator] {evidenceId} -> {newStatus} (via {tool})");
    }
}
