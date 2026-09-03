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
///
/// This is the presentation layer and only the presentation layer. Nothing here
/// affects the state machine or the session log: a status that goes unannounced was
/// still recorded, still gates the next step, and still reaches the scorer.
/// </summary>
public class EvidenceNotifier : MonoBehaviour
{
    /// <summary>
    /// Whether reaching EvidenceStatus.Found announces itself to the player.
    ///
    /// PER-SCENARIO POLICY, NOT A GLOBAL RULE. It is a serialized field precisely so
    /// that it cannot become one: a hardcoded "Found is always silent" would break
    /// Tutorial_ToolTest, which is a tool testbed where naming the item you just walked
    /// up to is a legitimate teaching aid rather than a leak.
    ///
    /// Defaults TRUE so that every existing scene keeps the behaviour it already had.
    /// CSI_Environment sets it FALSE, and that scene is the reason this field exists:
    /// it is an assessment-grade, no-hints scenario, and a toast reading "EVD-014
    /// found" fires on real evidence while non-evidence objects produce nothing. A
    /// player can therefore map the entire evidence roster by walking the room and
    /// reading toasts - without searching, without judging anything, and without
    /// taking a single deliberate action.
    ///
    /// That is the same leak class as the marking cue (see the DELIBERATELY IDENTICAL
    /// note in EvidenceTentTool.PlaceTent and DO NOT ADD IT in FeedbackDirector), one
    /// level earlier in the sequence and cheaper still to exploit, because marking at
    /// least required the player to commit to a claim.
    ///
    /// Deliberately a bool rather than an enum. A generic, non-naming presence cue
    /// ("something nearby") is a plausible third option for some future scenario, but
    /// it is NOT what CSI_Environment wants - directional information the player did
    /// not generate themselves is still a hint - so there is no Generic case here to
    /// leave unused and rot.
    /// </summary>
    [Tooltip("Announce evidence discovery (Found) to the player by name. Leave OFF for assessment scenarios: only real evidence raises this toast, so a player can map the whole roster by walking the room and reading it — without searching or judging anything. Same leak class as the marking cue, one step earlier. ON is appropriate for tool testbeds like Tutorial_ToolTest.")]
    [SerializeField] private bool announceDiscovery = true;

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
        // Presentation-only, and only for this one status. MarkFound has already run:
        // the record advanced, OnEvidenceStatusChanged fired, SessionLogger wrote its
        // EvidenceStatusChanged entry, and the procedural gate now allows Marked. All
        // that is suppressed here is the sentence on screen.
        //
        // Suppressed ENTIRELY when off - no substitute cue, not even a nameless one.
        // Found is a passive backend signal with no required player-facing
        // consequence, unlike marking or a blocked gate, which the player has to be
        // able to read. Replacing the toast with "something nearby" would still be
        // telling them where to look.
        if (status == EvidenceStatus.Found && !announceDiscovery)
        {
            return;
        }

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
