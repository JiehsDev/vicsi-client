// Assets/_Project/Scripts/Interaction/EvidenceTentPickup.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sits on a tent prop after EvidenceTentTool places it in the world. Once the
/// player's head comes within range, a "[X] Pick Up Evidence Tent" prompt shows
/// (via the reusable NotificationManager) - pressing the left controller's X
/// button while in range reclaims the tent: it disappears and frees that number
/// back up on the dispenser it came from (the dispenser always offers the lowest
/// unused number next), so the total number of tents in play never exceeds how
/// many distinct tent models exist, but the player isn't permanently limited by
/// early placements either.
/// </summary>
public class EvidenceTentPickup : MonoBehaviour
{
    private const string PromptText = "[X] Pick Up Evidence Tent";

    private EvidenceTentTool owner;
    private float pickupRadius;
    private int tentIndex;
    private InputAction pickupAction;
    private bool inRange;

    public void Initialize(EvidenceTentTool owningTool, float radius, int index)
    {
        owner = owningTool;
        pickupRadius = radius;
        tentIndex = index;
    }

    private void Awake()
    {
        // Same "raw XR controller button" pattern PhotographTool uses for its
        // aim toggle - not asset-backed, since no dedicated "X button" action
        // exists in the shared XRI Default Input Actions asset.
        pickupAction = new InputAction("EvidenceTentPickup_X", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
    }

    private void OnEnable()
    {
        pickupAction.Enable();
    }

    private void OnDisable()
    {
        pickupAction.Disable();
    }

    private void OnDestroy()
    {
        pickupAction?.Dispose();

        if (inRange)
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

        bool nowInRange = Vector3.Distance(transform.position, povCamera.transform.position) <= pickupRadius;
        if (nowInRange != inRange)
        {
            inRange = nowInRange;
            if (inRange)
            {
                NotificationManager.ShowPrompt(PromptText);
            }
            else
            {
                NotificationManager.HidePrompt();
            }
        }

        if (inRange && pickupAction.WasPressedThisFrame())
        {
            NotificationManager.HidePrompt();
            owner?.ReclaimSlot(tentIndex);
            Destroy(gameObject);
        }
    }
}
