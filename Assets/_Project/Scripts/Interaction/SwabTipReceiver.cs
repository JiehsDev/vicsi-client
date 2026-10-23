// Assets/_Project/Scripts/Interaction/SwabTipReceiver.cs
using UnityEngine;

/// <summary>
/// Sits on the swab's own tip trigger-collider, forwarding overlap events up to
/// SwabTool. A thin forwarder rather than putting OnTrigger callbacks directly on
/// SwabTool, matching EvidenceBagReceiver's own reasoning: the tip is a smaller
/// child volume distinct from whatever else the tool carries.
/// </summary>
public class SwabTipReceiver : MonoBehaviour
{
    [SerializeField] private SwabTool swabTool;

    private void OnTriggerEnter(Collider other) => swabTool?.HandleTipTriggerEnter(other);

    private void OnTriggerExit(Collider other) => swabTool?.HandleTipTriggerExit(other);
}
