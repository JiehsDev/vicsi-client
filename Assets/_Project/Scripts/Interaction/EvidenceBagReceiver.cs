// Assets/_Project/Scripts/Interaction/EvidenceBagReceiver.cs
using UnityEngine;

/// <summary>
/// Sits on the bag's own trigger-collider child (the "opening"), separate from the bag
/// prop's own solid BoxCollider used for grabbing it, and forwards overlap events up to
/// EvidenceBagTool. A thin forwarder rather than putting OnTrigger callbacks directly
/// on EvidenceBagTool because the receiving zone is deliberately a smaller child volume
/// positioned at the bag's opening, not the whole bag body.
/// </summary>
public class EvidenceBagReceiver : MonoBehaviour
{
    [SerializeField] private EvidenceBagTool bagTool;

    private void OnTriggerEnter(Collider other) => bagTool?.HandleReceiverOverlap(other);

    // Checked every physics tick while overlapping, not just on first contact - the
    // three insertion conditions (bag open, evidence firmly grasped, item ready) can
    // each become true after the item is already sitting in the zone, e.g. the player
    // carries the item in first and only then closes their grip on the trigger.
    private void OnTriggerStay(Collider other) => bagTool?.HandleReceiverOverlap(other);
}
