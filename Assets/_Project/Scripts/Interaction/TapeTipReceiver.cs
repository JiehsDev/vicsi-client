// Assets/_Project/Scripts/Interaction/TapeTipReceiver.cs
using UnityEngine;

/// <summary>
/// Sits on the fingerprint tape's own application-zone trigger-collider, forwarding
/// overlap events up to FingerprintTapeTool. A thin forwarder rather than putting
/// OnTrigger callbacks directly on FingerprintTapeTool, matching BrushTipReceiver/
/// SwabTipReceiver's own reasoning: the tip is a smaller child volume distinct from
/// whatever else the tool carries (nothing else, for the tape, but the same shape is
/// kept for consistency).
///
/// Both OnTriggerEnter and OnTriggerExit are forwarded now - lifting used to be a
/// one-shot contact action with no persistent "still touching" state to track, but it
/// now requires a deliberate trigger press while genuinely still inside the zone (see
/// FingerprintTapeTool's own class comment), so the tool needs to know the moment
/// contact ends, the same way BrushTipReceiver already does for FingerprintKitTool.
/// </summary>
public class TapeTipReceiver : MonoBehaviour
{
    [SerializeField] private FingerprintTapeTool fingerprintTapeTool;

    private void OnTriggerEnter(Collider other) => fingerprintTapeTool?.HandleTipTriggerEnter(other);
    private void OnTriggerExit(Collider other) => fingerprintTapeTool?.HandleTipTriggerExit(other);
}
