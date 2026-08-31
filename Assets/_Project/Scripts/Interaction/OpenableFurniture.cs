// Assets/_Project/Scripts/Interaction/OpenableFurniture.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sits on a drawer or door prop. Once the player's head comes within range, an
/// "Open"/"Close" prompt shows (via the reusable NotificationManager, same
/// pattern as EvidenceTentPickup's "[X] Pick Up Evidence Tent" prompt) -
/// pressing either controller's trigger toggles it between its closed pose and
/// an open pose offset from it, sliding for a drawer or swinging for a hinged
/// door depending on openMode. The open pose is computed once at Awake from
/// whatever closed pose the object was placed at in the scene, so the same
/// component works on any drawer/door instance without per-object setup beyond
/// picking openMode and, if the default offset doesn't match the prop's size
/// or hinge side, tuning slideLocalOffset/openLocalEulerOffset.
/// </summary>
public class OpenableFurniture : MonoBehaviour
{
    private enum OpenMode
    {
        Slide,
        Rotate
    }

    [SerializeField] private OpenMode openMode = OpenMode.Slide;
    [SerializeField] private float interactionRadius = 1.2f;

    [Header("Slide (drawers) - local position offset from the closed pose when open")]
    [SerializeField] private Vector3 slideLocalOffset = new Vector3(0f, 0f, 0.35f);
    [SerializeField] private float moveSpeed = 0.6f;

    [Header("Rotate (hinged doors) - local Euler offset from the closed pose when open")]
    [SerializeField] private Vector3 openLocalEulerOffset = new Vector3(0f, 90f, 0f);
    [SerializeField] private float rotateSpeed = 120f;

    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private Vector3 openLocalPosition;
    private Quaternion openLocalRotation;

    private bool isOpen;
    private bool inRange;
    private InputAction leftTriggerAction;
    private InputAction rightTriggerAction;

    private void Awake()
    {
        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
        openLocalPosition = closedLocalPosition + slideLocalOffset;
        openLocalRotation = closedLocalRotation * Quaternion.Euler(openLocalEulerOffset);

        // Raw XR controller path, same pattern as EvidenceTentPickup/PhotographTool's
        // secondary button reads - no dedicated "trigger" action exists in the
        // shared XRI Default Input Actions asset for this per-prop use case.
        leftTriggerAction = new InputAction(name + "_LeftTrigger", InputActionType.Button, "<XRController>{LeftHand}/trigger");
        rightTriggerAction = new InputAction(name + "_RightTrigger", InputActionType.Button, "<XRController>{RightHand}/trigger");
    }

    private void OnEnable()
    {
        leftTriggerAction.Enable();
        rightTriggerAction.Enable();
    }

    private void OnDisable()
    {
        leftTriggerAction.Disable();
        rightTriggerAction.Disable();

        if (inRange)
        {
            NotificationManager.HidePrompt();
        }
    }

    private void OnDestroy()
    {
        leftTriggerAction?.Dispose();
        rightTriggerAction?.Dispose();
    }

    private void Update()
    {
        UpdateRangeAndPrompt();

        if (inRange && (leftTriggerAction.WasPressedThisFrame() || rightTriggerAction.WasPressedThisFrame()))
        {
            isOpen = !isOpen;
            NotificationManager.ShowPrompt(PromptText());
        }

        AnimateTowardTarget();
    }

    private void UpdateRangeAndPrompt()
    {
        var povCamera = Camera.main;
        if (povCamera == null)
        {
            return;
        }

        bool nowInRange = Vector3.Distance(transform.position, povCamera.transform.position) <= interactionRadius;
        if (nowInRange == inRange)
        {
            return;
        }

        inRange = nowInRange;
        if (inRange)
        {
            NotificationManager.ShowPrompt(PromptText());
        }
        else
        {
            NotificationManager.HidePrompt();
        }
    }

    private string PromptText() => isOpen ? "[Trigger] Close" : "[Trigger] Open";

    private void AnimateTowardTarget()
    {
        Vector3 targetPosition = isOpen ? openLocalPosition : closedLocalPosition;
        Quaternion targetRotation = isOpen ? openLocalRotation : closedLocalRotation;

        if (openMode == OpenMode.Slide)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPosition, moveSpeed * Time.deltaTime);
        }
        else
        {
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRotation, rotateSpeed * Time.deltaTime);
        }
    }
}
