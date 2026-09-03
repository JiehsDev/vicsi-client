// Assets/_Project/Scripts/Feedback/FeedbackDirector.cs
using UnityEngine;

/// <summary>
/// Routes the evidence state machine's existing events to InteractionFeedback, the
/// same way EvidenceNotifier routes them to toasts: no tool script needs to know
/// haptics or audio exist. Drop this on a scene that has an EvidenceStateManager and
/// an InteractionFeedback.
///
/// Kept separate from EvidenceNotifier rather than folded into it because they are
/// different concerns with different failure modes - a toast is information, a pulse
/// is acknowledgement - and because this one has a rule EvidenceNotifier does not:
/// see the Marked exclusion below.
/// </summary>
public class FeedbackDirector : MonoBehaviour
{
    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleStatusChanged;
        EvidenceStateManager.OnEvidenceTransitionBlocked += HandleBlocked;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleStatusChanged;
        EvidenceStateManager.OnEvidenceTransitionBlocked -= HandleBlocked;
    }

    private void HandleStatusChanged(string evidenceId, EvidenceStatus status)
    {
        // Marked is deliberately NOT acknowledged here. DO NOT ADD IT.
        //
        // This method only ever runs for registered evidence, because only registered
        // evidence has a status to change. A mark on a decoy or on bare floor would
        // therefore be silent, and that silence would tell the player in real time
        // that they had identified wrongly - revealing identification correctness
        // through a side channel, which is exactly what this scenario must not do.
        //
        // EvidenceTentTool owns the Marked cue instead and raises it unconditionally,
        // so both outcomes feel the same. See the long note at that call site.
        if (status == EvidenceStatus.Marked)
        {
            return;
        }

        InteractionFeedback.Confirm();
    }

    private void HandleBlocked(string evidenceId, EvidenceStatus attempted, EvidenceStatus current)
    {
        // A refusal, not a wrong answer: the procedure was performed out of order, or
        // a marker was pulled off something already documented. Telling the player
        // the game declined leaks nothing about which items matter.
        InteractionFeedback.Blocked();
    }
}
