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
            // Marked is deliberately absent from this list. A toast here would read
            // "EVD-018 marked" when the tent lands on registered evidence and show
            // NOTHING when it lands on anything else - which tells the player, at the
            // instant they place it, whether the thing they just identified was real.
            // EvidenceTentTool raises its own toast instead, worded identically on both
            // branches and naming no evidence id. See the note there before changing this.
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
