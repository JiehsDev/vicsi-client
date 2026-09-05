// Assets/_Project/Scripts/Interaction/RightHandOnlyFilter.cs
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Attach to an evidence prop and add it to GrabInteractable's (and, for
/// rig-configuration-agnostic consistency, HandGrabInteractable's) interactorFilters
/// list. Rejects any interactor whose controller/hand isn't Right, so the left hand -
/// meant to be occupied holding the evidence bag - cannot physically grab evidence at
/// all, not just "isn't credited for it." Confirmed via ControllerGrabInteractor:
/// there are genuinely separate left- and right-hand interactor instances in this
/// rig's Comprehensive Interaction setup, not one shared instance.
///
/// Checks IController.Handedness (this project is controller-based, not hand-tracking,
/// per its existing input conventions) rather than matching by GameObject name/path,
/// since every interactor already carries a ControllerRef exposing this directly.
/// </summary>
public class RightHandOnlyFilter : MonoBehaviour, IGameObjectFilter
{
    public bool Filter(GameObject interactor)
    {
        var controller = interactor.GetComponentInParent<IController>();
        return controller != null && controller.Handedness == Handedness.Right;
    }
}
