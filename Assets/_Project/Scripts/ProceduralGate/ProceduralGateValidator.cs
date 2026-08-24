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
    public RoleId role;
    public float time;
}

/// <summary>
/// Generic gate for "collect" actions. Not tied to any specific role's
/// interaction script (there's no playable Collector yet) - anything that
/// wants to allow a collect action should check CanCollect() first and use
/// GetBlockReason() to explain why it's blocked.
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

    /// <summary>Whether the given evidence item may currently be collected.</summary>
    public bool CanCollect(string evidenceId)
    {
        return EvidenceStateManager.Instance != null
            && EvidenceStateManager.Instance.IsReadyForCollection(evidenceId);
    }

    /// <summary>
    /// Short human-readable reason CanCollect() is currently false, e.g. "Not yet logged."
    /// Returns null if the item is collectible or already collected/processed is not the concern here.
    /// </summary>
    public string GetBlockReason(string evidenceId)
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

        if (record.status == EvidenceStatus.ReadyForCollection)
        {
            return null;
        }

        return ReasonFor(record.status);
    }

    private static string ReasonFor(EvidenceStatus status)
    {
        switch (status)
        {
            case EvidenceStatus.NotFound: return "Not yet found.";
            case EvidenceStatus.Found: return "Not yet photographed.";
            case EvidenceStatus.Photographed: return "Not yet sketched.";
            case EvidenceStatus.Sketched: return "Not yet logged.";
            case EvidenceStatus.Logged: return "Not yet logged.";
            case EvidenceStatus.Collected: return "Already collected.";
            case EvidenceStatus.Processed: return "Already processed and collected.";
            default: return "Not ready for collection.";
        }
    }

    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus newStatus)
    {
        RoleId role = RoleId.None;
        var record = EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(evidenceId)
            : null;
        if (record != null)
        {
            role = record.lastInteractedBy;
        }

        eventLog.Add(new ProceduralGateLogEntry
        {
            evidenceId = evidenceId,
            status = newStatus,
            role = role,
            time = Time.time
        });

        Debug.Log($"[ProceduralGateValidator] {evidenceId} -> {newStatus} (by {role})");
    }
}
