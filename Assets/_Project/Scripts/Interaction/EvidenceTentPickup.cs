// Assets/_Project/Scripts/Interaction/EvidenceTentPickup.cs
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
    private InputAction pickupAction;
    private bool promptShown;

    /// <summary>pickupRadius squared, for the coordinator's cheap nearest-tent comparison.</summary>
    public float PickupRadiusSqr => pickupRadius * pickupRadius;

    public void Initialize(EvidenceTentTool owningTool, float radius, int index)
    {
        owner = owningTool;
        pickupRadius = radius;
        tentIndex = index;
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
            NotificationManager.HidePrompt();
            promptShown = false;
            owner?.ReclaimSlot(tentIndex);
            Destroy(gameObject);
        }
    }
}
