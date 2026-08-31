// Assets/_Project/Scripts/Interaction/AtticLadderLever.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Wall-mounted lever that toggles a FoldingStairToggle (e.g. the attic
/// ladder near the chimney) open or closed. Same proximity + right
/// controller B prompt pattern as EvidenceTentPickup - shows a
/// "[B] Toggle Attic Ladder" prompt via NotificationManager while the player
/// is near, and defers to PlayerUIGate so it stays quiet while the camera
/// viewfinder, utility menu, or tool wheel already has the player's
/// attention. leverHandle (if assigned) visually snaps between two
/// rotations to sell the flip - purely cosmetic, doesn't drive the stairs.
/// </summary>
public class AtticLadderLever : MonoBehaviour
{
    private const string PromptText = "[B] Toggle Attic Ladder";

    [SerializeField] private FoldingStairToggle stairToggle;
    [SerializeField] private Transform leverHandle;
    [SerializeField] private float pickupRadius = 1.2f;
    [SerializeField] private Vector3 leverClosedLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 leverOpenLocalEuler = new Vector3(-60f, 0f, 0f);

    private InputAction toggleAction;
    private bool promptShown;

    private void Awake()
    {
        // Right controller's B button - same as EvidenceTentPickup, since a
        // wall lever and a placed tent are never realistically in range at
        // the same time, and this still defers to PlayerUIGate for the
        // camera/menu/wheel collisions that actually matter.
        toggleAction = new InputAction("AtticLadderLever_B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
    }

    private void OnEnable()
    {
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        toggleAction.Disable();
    }

    private void OnDestroy()
    {
        toggleAction?.Dispose();

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

        // Horizontal-only distance, same reasoning as EvidenceTentPickupCoordinator -
        // the lever sits well above floor height, so a straight-line distance from
        // a standing head would make it read as "in range" only when far too close.
        Vector3 offset = transform.position - povCamera.transform.position;
        offset.y = 0f;
        bool inRange = offset.sqrMagnitude <= pickupRadius * pickupRadius;

        bool shouldShowPrompt = inRange && !PlayerUIGate.IsBlocked;
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

        if (promptShown && toggleAction.WasPressedThisFrame())
        {
            stairToggle?.Toggle();
            UpdateLeverVisual();
        }
    }

    private void UpdateLeverVisual()
    {
        if (leverHandle == null || stairToggle == null)
        {
            return;
        }

        leverHandle.localEulerAngles = stairToggle.IsOpen ? leverOpenLocalEuler : leverClosedLocalEuler;
    }
}
