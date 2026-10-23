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
///
/// Also enforces REACH. The SDK's GrabInteractor picks the closest grab-enabled item in
/// the whole scene with no distance limit (GrabInteractor.ComputeCandidate scores every
/// registered interactable, inside or not), so once an item was ready for collection a
/// grip press anywhere pulled it into the hand. The interactor's grab point must be within
/// maxReachDistance of the item's solid surface - the same measure and distance
/// EvidenceBagTool uses for "firmly grasped" - so only the item actually being touched can
/// be grabbed, and two ready items never compete across the room. Filters are applied
/// while choosing a candidate, not while an item is already held, so carrying it away is
/// unaffected.
/// </summary>
public class RightHandOnlyFilter : MonoBehaviour, IGameObjectFilter
{
    [Tooltip("Max distance (m) from the hand's grab point to this item's solid surface for it to be grabbable. Matches EvidenceBagTool's grab reach.")]
    [SerializeField, Min(0.01f)] private float maxReachDistance = 0.15f;

    public float MaxReachDistance => maxReachDistance;

    public bool Filter(GameObject interactor)
    {
        var controller = interactor.GetComponentInParent<IController>();
        if (controller == null || controller.Handedness != Handedness.Right)
        {
            return false;
        }
        return DistanceToSurface(GrabPoint(interactor)) <= maxReachDistance;
    }

    /// <summary>Where the interactor grabs from: its rigidbody for a controller GrabInteractor, else its own transform.</summary>
    public static Vector3 GrabPoint(GameObject interactor)
    {
        var grab = interactor.GetComponent<GrabInteractor>();
        if (grab != null && grab.Rigidbody != null)
        {
            return grab.Rigidbody.transform.position;
        }
        return interactor.transform.position;
    }

    /// <summary>Distance from point to this item's solid geometry (not its pivot) - same rule as EvidenceBagTool.</summary>
    public float DistanceToSurface(Vector3 point)
    {
        float best = Vector3.Distance(point, transform.position);
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || (collider is MeshCollider mesh && !mesh.convex))
            {
                continue;
            }
            best = Mathf.Min(best, Vector3.Distance(point, collider.ClosestPoint(point)));
        }
        return best;
    }
}
