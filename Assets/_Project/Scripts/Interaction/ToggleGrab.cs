// Assets/_Project/Scripts/Interaction/ToggleGrab.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Converts hold-to-grab into click-to-grab / click-to-release for whatever
/// GrabInteractable/HandGrabInteractable this sits alongside. On the first
/// real grab, the interactor that grabbed it is forced to keep selecting
/// (Meta's own "allowManualRelease: false" sticky-select) so physically
/// opening the hand or releasing the grip button doesn't drop it. Pressing
/// the grip/select button again while stuck calls ForceRelease() on that
/// same interactor to let go. Lives on GrabbableEvidenceBase so it applies
/// to every grabbable built on that prefab.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class ToggleGrab : MonoBehaviour
{
    [Header("Input (XRI Input Reader pattern) - the grip/select button used to release")]
    [SerializeField] private InputActionReference leftSelectAction;
    [SerializeField] private InputActionReference rightSelectAction;

    private Grabbable grabbable;
    private HandGrabInteractable handGrabInteractable;
    private GrabInteractable grabInteractable;

    private bool isStuck;
    private int stuckSinceFrame = -1;
    private HandGrabInteractor stuckHandInteractor;
    private GrabInteractor stuckGrabInteractor;

    private void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        handGrabInteractable = GetComponent<HandGrabInteractable>();
        grabInteractable = GetComponent<GrabInteractable>();
    }

    private void OnEnable()
    {
        grabbable.WhenPointerEventRaised += HandlePointerEvent;
        leftSelectAction?.action.Enable();
        rightSelectAction?.action.Enable();
    }

    private void OnDisable()
    {
        grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        leftSelectAction?.action.Disable();
        rightSelectAction?.action.Disable();
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type != PointerEventType.Select || isStuck)
        {
            return;
        }

        // A real grab just started - immediately make it sticky so letting
        // go of the physical grip doesn't drop it; only a second deliberate
        // press (handled in Update) releases it.
        var handInteractor = FindInteractorByIdentifier<HandGrabInteractor>(evt.Identifier);
        if (handInteractor != null && handGrabInteractable != null)
        {
            handInteractor.ForceSelect(handGrabInteractable, false);
            stuckHandInteractor = handInteractor;
            isStuck = true;
            stuckSinceFrame = Time.frameCount;
            return;
        }

        var grabInteractor = FindInteractorByIdentifier<GrabInteractor>(evt.Identifier);
        if (grabInteractor != null && grabInteractable != null)
        {
            grabInteractor.ForceSelect(grabInteractable);
            stuckGrabInteractor = grabInteractor;
            isStuck = true;
            stuckSinceFrame = Time.frameCount;
        }
    }

    private void Update()
    {
        if (!isStuck || Time.frameCount == stuckSinceFrame)
        {
            return;
        }

        bool releasePressed =
            (leftSelectAction != null && leftSelectAction.action.WasPressedThisFrame()) ||
            (rightSelectAction != null && rightSelectAction.action.WasPressedThisFrame());

        if (!releasePressed)
        {
            return;
        }

        stuckHandInteractor?.ForceRelease();
        stuckGrabInteractor?.ForceRelease();
        stuckHandInteractor = null;
        stuckGrabInteractor = null;
        isStuck = false;
    }

    private static T FindInteractorByIdentifier<T>(int identifier) where T : Component, IInteractorView
    {
        var candidates = FindObjectsByType<T>(FindObjectsSortMode.None);
        foreach (var candidate in candidates)
        {
            if (candidate.Identifier == identifier)
            {
                return candidate;
            }
        }
        return null;
    }
}
