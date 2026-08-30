// Assets/_Project/Scripts/UI/EvidenceNotifier.cs
using UnityEngine;

/// <summary>
/// Bridges EvidenceStateManager's existing OnEvidenceStatusChanged event to
/// NotificationManager, so every evidence status change already flowing through
/// this project - photographed, sketched, logged, collected, whichever tool
/// caused it - surfaces a toast without any of those tools (PhotographTool,
/// SketchTool, EvidenceCollectorTool, ...) needing to know notifications exist.
/// Sample wiring for the reusable NotificationManager/NotificationUI pair; drop
/// this on any GameObject in a scene that has both an EvidenceStateManager and a
/// NotificationManager to enable it.
/// </summary>
public class EvidenceNotifier : MonoBehaviour
{
    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleStatusChanged;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleStatusChanged;
    }

    private void HandleStatusChanged(string evidenceId, EvidenceStatus status)
    {
        string verb = status switch
        {
            EvidenceStatus.Found => "found",
            EvidenceStatus.Photographed => "photographed",
            EvidenceStatus.Sketched => "sketched",
            EvidenceStatus.Logged => "logged",
            EvidenceStatus.ReadyForCollection => "ready for collection",
            EvidenceStatus.Collected => "collected",
            EvidenceStatus.Processed => "processed",
            _ => null,
        };

        if (verb == null)
        {
            return;
        }

        NotificationManager.Notify($"{evidenceId} {verb}");
    }
}
