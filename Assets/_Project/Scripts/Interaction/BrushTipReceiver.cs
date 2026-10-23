// Assets/_Project/Scripts/Interaction/BrushTipReceiver.cs
using UnityEngine;

/// <summary>
/// Sits on the fingerprint brush's own tip trigger-collider, forwarding overlap
/// events up to FingerprintKitTool. A thin forwarder rather than putting OnTrigger
/// callbacks directly on FingerprintKitTool, matching SwabTipReceiver/
/// EvidenceBagReceiver's own reasoning: the tip is a smaller child volume distinct
/// from whatever else the tool carries.
/// </summary>
public class BrushTipReceiver : MonoBehaviour
{
    [SerializeField] private FingerprintKitTool fingerprintKitTool;

    private void OnTriggerEnter(Collider other) => fingerprintKitTool?.HandleBrushTriggerEnter(other);

    private void OnTriggerExit(Collider other) => fingerprintKitTool?.HandleBrushTriggerExit(other);
}
