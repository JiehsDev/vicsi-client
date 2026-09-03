// Assets/_Project/Scripts/Interaction/EvidenceTentPickup.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sits on a tent prop after EvidenceTentTool places it in the world and
/// registers itself with EvidenceTentPickupCoordinator while active. Every
/// frame, whichever placed tent is nearest the player's head - out of all of
/// them, not just this one - and within pickup range shows a single
/// "[B] Pick Up Evidence Tent" prompt (via the reusable NotificationManager);
/// every other tent, including others also technically in range, stays
/// silent. Pressing the right controller's B button while that prompt is up
/// reclaims that one tent: it disappears and frees its number back up on the
/// dispenser it came from (the dispenser always offers the lowest unused
/// number next), so the total number of tents in play never exceeds how many
/// distinct tent models exist, but the player isn't permanently limited by
/// early placements either. Standing between several tents, or a whole pile
/// of them, only ever offers to pick up one at a time. The prompt (and the B
/// press it advertises) also defers to PlayerUIGate, since a screen like the
/// camera viewfinder, the utility menu, or the photo album already owning the
/// player's attention should take priority over this hint.
/// </summary>
public class EvidenceTentPickup : MonoBehaviour
{
    private const string PromptText = "[B] Pick Up Evidence Tent";

    private EvidenceTentTool owner;
    private float pickupRadius;
    private int tentIndex;
    private string markedEvidenceId;
    private InputAction pickupAction;
    private bool promptShown;

    /// <summary>pickupRadius squared, for the coordinator's cheap nearest-tent comparison.</summary>
    public float PickupRadiusSqr => pickupRadius * pickupRadius;

    /// <param name="evidenceId">
    /// The evidence item this tent was placed on, or null/empty if it was placed on
    /// something that isn't evidence. Determines whether reclaiming it has any state
    /// consequence - a tent on untagged geometry never advanced anything, so there is
    /// nothing to revert and nothing to block.
    /// </param>
    public void Initialize(EvidenceTentTool owningTool, float radius, int index, string evidenceId)
    {
        owner = owningTool;
        pickupRadius = radius;
        tentIndex = index;
        markedEvidenceId = evidenceId;
    }

    private void Awake()
    {
        // Right controller's B button - deliberately not the left controller's
        // X the camera viewfinder, utility menu, and tool wheel all already
        // fight over. B is also PhotoAlbumUI's close button, so that screen
        // (like every other exclusive UI here) registers with PlayerUIGate
        // too - the prompt below already defers to it, so closing the album
        // can never also pick up a tent underneath it.
        pickupAction = new InputAction("EvidenceTentPickup_B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
    }

    private void OnEnable()
    {
        pickupAction.Enable();
        EvidenceTentPickupCoordinator.Register(this);
    }

    private void OnDisable()
    {
        pickupAction.Disable();
        EvidenceTentPickupCoordinator.Unregister(this);
    }

    private void OnDestroy()
    {
        pickupAction?.Dispose();

        if (promptShown)
        {
            NotificationManager.HidePrompt();
        }
    }

    private void Update()
    {
        var povCamera = Camera.main;
        if (povCamera == null)
        {
            return;
        }

        bool isNearestCandidate =
            EvidenceTentPickupCoordinator.FindNearestInRange(povCamera.transform.position) == this;

        bool shouldShowPrompt = isNearestCandidate && !PlayerUIGate.IsBlocked;
        if (shouldShowPrompt != promptShown)
        {
            promptShown = shouldShowPrompt;
            if (promptShown)
            {
                NotificationManager.ShowPrompt(PromptText);
            }
            else
            {
                NotificationManager.HidePrompt();
            }
        }

        if (promptShown && pickupAction.WasPressedThisFrame())
        {
            TryReclaim();
        }
    }

    /// <summary>
    /// Reclaiming is status-dependent, not uniformly allowed or uniformly locked.
    ///
    /// While the item is still only Marked, pulling the tent is a legitimate
    /// correction - the player looked closer and decided this isn't evidence - so the
    /// status reverts to Found and the tent comes back. Once the item has been
    /// Photographed or later, the marker is already in the documentation and the
    /// reclaim is refused, matching the one-way gates used everywhere else here.
    ///
    /// SCORING DEPENDS ON THIS: the revert rewinds state only. The original
    /// MarkTented / NonEvidenceMarked event is never deleted or rewritten, and a
    /// MarkerReclaimed event is appended next to it, so the log still shows that the
    /// player marked this item and then changed their mind. Scoring must therefore read
    /// the full event history rather than a final-state snapshot - otherwise tenting
    /// everything, seeing what reacts, and quietly walking back the wrong ones would
    /// look identical to identifying correctly the first time. See
    /// EvidenceStateManager.TryReclaimMarker.
    /// </summary>
    private void TryReclaim()
    {
        // A tent on non-evidence never advanced any record, so there is nothing to
        // revert and nothing to protect - let it come back freely. The original
        // NonEvidenceMarked event already stands in the log regardless.
        if (string.IsNullOrEmpty(markedEvidenceId) || EvidenceStateManager.Instance == null)
        {
            CompleteReclaim(null);
            return;
        }

        var result = EvidenceStateManager.Instance.TryReclaimMarker(markedEvidenceId, ToolType.EvidenceMarker);

        if (result == ReclaimResult.BlockedDocumented)
        {
            // Refused, but never silently - a blocked attempt is diagnostic in its own
            // right, the same reasoning behind logging EvidenceTransitionBlocked.
            NotificationManager.Notify("Already documented — the marker stays.");
            SessionLogger.Instance?.LogEvent(
                SessionEventType.MarkerReclaimBlocked,
                markedEvidenceId,
                new Dictionary<string, string>
                {
                    { "tentNumber", (tentIndex + 1).ToString() }
                });
            return;
        }

        CompleteReclaim(result == ReclaimResult.Reverted ? markedEvidenceId : null);
    }

    /// <summary>Frees the tent number, logs the reclaim, and removes the prop.</summary>
    private void CompleteReclaim(string revertedEvidenceId)
    {
        SessionLogger.Instance?.LogEvent(
            SessionEventType.MarkerReclaimed,
            string.IsNullOrEmpty(markedEvidenceId) ? "(non-evidence)" : markedEvidenceId,
            new Dictionary<string, string>
            {
                { "tentNumber", (tentIndex + 1).ToString() },
                { "wasEvidence", (!string.IsNullOrEmpty(markedEvidenceId)).ToString() },
                { "statusReverted", (!string.IsNullOrEmpty(revertedEvidenceId)).ToString() }
            });

        // Only when nothing changed status. A genuine revert already raised
        // OnEvidenceStatusChanged(Found), which FeedbackDirector acknowledges - firing
        // here as well would double the cue on that path and on that path only.
        if (string.IsNullOrEmpty(revertedEvidenceId))
        {
            InteractionFeedback.Confirm();
        }

        NotificationManager.HidePrompt();
        promptShown = false;
        owner?.ReclaimSlot(tentIndex);
        Destroy(gameObject);
    }
}
