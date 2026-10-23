// Assets/_Project/Scripts/Interaction/LeftHandOnlyFilter.cs
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Mirror of RightHandOnlyFilter, for the evidence bag: rejects any interactor whose
/// controller/hand isn't Left, so the right hand - meant to be free for grabbing
/// evidence - cannot also pick up the bag. Without this, GrabInteractor's candidate
/// scan (which considers every registered GrabInteractable in the scene with no
/// distance cutoff, picking whichever scores closest) could resolve the right hand's
/// grab attempt to the bag instead of a small/distant evidence item whenever the bag
/// happens to be physically closer to the right controller at that moment.
/// </summary>
public class LeftHandOnlyFilter : MonoBehaviour, IGameObjectFilter
{
    public bool Filter(GameObject interactor)
    {
        var controller = interactor.GetComponentInParent<IController>();
        return controller != null && controller.Handedness == Handedness.Left;
    }
}
