// Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class EvidenceStateManager : MonoBehaviour
{
    public static EvidenceStateManager Instance { get; private set; }

    [Tooltip("All evidence definitions used in this scenario — drag every EvidenceDefinition asset here.")]
    [SerializeField] private List<EvidenceDefinition> sceneEvidenceDefinitions;

    private readonly Dictionary<string, EvidenceRecord> records = new();

    // Anything (STCS, ProceduralGate, DeductionBoard, Logger) can subscribe to this
        public static event Action<string, EvidenceStatus> OnEvidenceStatusChanged;

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

    // --- Transition methods: each one is called by a role's interaction script ---

    public void MarkFound(string evidenceId, RoleId role) => SetStatus(evidenceId, EvidenceStatus.Found, role);
    public void MarkPhotographed(string evidenceId, RoleId role) => SetStatus(evidenceId, EvidenceStatus.Photographed, role);
    public void MarkSketched(string evidenceId, RoleId role) => SetStatus(evidenceId, EvidenceStatus.Sketched, role);

    public void MarkLogged(string evidenceId, RoleId role)
    {
        SetStatus(evidenceId, EvidenceStatus.Logged, role);
        CheckReadyForCollection(evidenceId);
    }

    public void MarkCollected(string evidenceId, RoleId role) => SetStatus(evidenceId, EvidenceStatus.Collected, role);
    public void MarkProcessed(string evidenceId, RoleId role) => SetStatus(evidenceId, EvidenceStatus.Processed, role);

    private void CheckReadyForCollection(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null) return;

        // For MVP: "ready" once Logged has happened (which implies Photographed + Sketched
        // already occurred, if your interaction scripts enforce that order upstream).
        if (record.status == EvidenceStatus.Logged)
        {
            SetStatus(evidenceId, EvidenceStatus.ReadyForCollection, record.lastInteractedBy);
        }
    }

    private void SetStatus(string evidenceId, EvidenceStatus newStatus, RoleId role)
    {
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return;
        }

        record.status = newStatus;
        record.lastInteractedBy = role;
        record.statusChangedAtTime = Time.time;

        OnEvidenceStatusChanged?.Invoke(evidenceId, newStatus);
        Debug.Log($"[EvidenceStateManager] {evidenceId} → {newStatus} (by {role})");
    }
}