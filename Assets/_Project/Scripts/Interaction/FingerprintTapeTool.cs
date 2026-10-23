// Assets/_Project/Scripts/Interaction/FingerprintTapeTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Separately-equippable fingerprint lifting tape - the second half of an on-scene
/// print sub-step (the knife, EVD-014; the gloves, EVD-019), after FingerprintKitTool's
/// own dust brush (see that class's own comment for the full flow, and for why it's
/// generalized off EvidenceFingerprintMarker rather than one hardcoded item).
/// Selecting this from the tool wheel
/// holsters whatever was equipped before it via the normal PlayerToolRegistry.
/// ToggleEquip/HolsterCurrent flow - if that was FingerprintKitTool, its own Holster()
/// override already un-equips the jar right along with the brush, so switching to the
/// tape empties BOTH hands with no special-casing needed here.
///
/// A single right-hand item, unlike the brush+jar's two-handed equip: there is no
/// second prop to hold opposite the tape (nothing plays the jar's role here), so this
/// only needs a tip collider on the tape itself, mirroring SwabTool.tipCollider/
/// FingerprintKitTool.brushTipCollider - PlayerTool.SetEquippedVisualState disables
/// every collider under this GameObject in both equip states, so this has to be
/// explicitly re-enabled/disabled in step with EquipToHand/Holster, same as those.
///
/// Lifting is DELIBERATE, not contact-alone: the tape's own application-zone trigger
/// must be genuinely inside the print's touch-zone collider (see FingerprintKitTool's
/// class comment on why that's a separate collider from the print's other, solid one)
/// AND the right trigger held down (see Update) - entering the collider by itself does
/// nothing anymore. Reuses the exact same right-trigger InputActionReference every
/// other tool in this project already binds (rightActivateAction), per this project's
/// own "reuse the established binding, don't invent a conflicting one" convention -
/// see FingerprintKitTool.rightActivateAction, SwabTool.rightActivateAction. TryLift's
/// actual implementation deliberately stays on FingerprintKitTool rather than moving
/// here - see that method's own comment for why - this class only tracks contact and,
/// on a deliberate press while inside the zone, delegates to it.
/// </summary>
public class FingerprintTapeTool : PlayerTool
{
    public static FingerprintTapeTool Instance { get; private set; }

    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Tape tip")]
    [Tooltip("Trigger collider covering the tape's own sticky application area - detects the print's touch-zone collider. Disabled by PlayerTool.SetEquippedVisualState in both equip states, so this is explicitly re-enabled/disabled in step with EquipToHand/Holster.")]
    [SerializeField] private Collider tipCollider;

    [Header("Tape and derived fingerprint card")]
    [Tooltip("Clean tape tool visual, restored when equipped for another lift.")]
    [SerializeField] private GameObject cleanVisual;
    [Tooltip("Existing lifted-print backing card model used by EvidenceInventory for the held sample.")]
    [SerializeField] private GameObject liftedVisualPrefab;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal GameObject DevelopmentLiftedVisual => liftedVisualPrefab;
#endif


    // Whichever print marker the tape's application zone is CURRENTLY overlapping, or
    // null - tracked by HandleTipTriggerEnter/Exit, read once per held frame in
    // Update. Mirrors FingerprintKitTool.touchingMarker exactly, for the same reason:
    // contact alone must never be enough, only contact PLUS a deliberate press.
    private EvidenceFingerprintMarker touchingMarker;

    public override ToolType ToolRole => ToolType.FingerprintTape;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        rightActivateAction?.action.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        rightActivateAction?.action.Disable();
    }

    public override void EquipToHand(Transform handAnchor)
    {
        base.EquipToHand(handAnchor);

        if (tipCollider != null)
        {
            tipCollider.enabled = true;
        }

        // A fresh strip every time the tape is (re-)equipped from the wheel - there is
        // no separate "seal" gesture here the way SwabTool has (lifting is a single
        // atomic action, see TryLift's own comment), so re-equipping is the natural
        // "you picked up a new one" moment instead.
        ApplyCleanVisual();
    }

    public override void Holster()
    {
        if (tipCollider != null)
        {
            tipCollider.enabled = false;
        }

        touchingMarker = null;
        base.Holster();
    }

    private void Update()
    {
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        if (!IsHeld || rightActivateAction == null || touchingMarker == null)
        {
            return;
        }

        // A discrete press, not held-to-accumulate: unlike dusting, lifting has no
        // partial-progress concept in the underlying state (IsDusted -> lifted is a
        // single TryLift call) - see FingerprintKitTool.TryLift's own comment. The
        // tip must still be genuinely inside the zone at the moment of the press.
        if (rightActivateAction.action.WasPressedThisFrame())
        {
            FingerprintKitTool.Instance?.TryLift(touchingMarker);
        }
    }

    /// <summary>
    /// Called by TapeTipReceiver whenever the tape's application-zone trigger overlap
    /// begins. Recognizes the print via the same EvidenceFingerprintMarker
    /// FingerprintKitTool itself resolves contact through, then only records contact -
    /// see class comment on why a deliberate press (Update) is required before this
    /// ever reaches FingerprintKitTool.TryLift.
    /// </summary>
    public void HandleTipTriggerEnter(Collider other)
    {
        var marker = other.GetComponentInParent<EvidenceFingerprintMarker>();
        if (marker != null)
        {
            touchingMarker = marker;
        }
    }

    /// <summary>Called by TapeTipReceiver whenever the tape's application-zone trigger overlap ends. Clears contact tracking only for the marker that actually left - a stale exit for a different marker (already superseded by a fresh enter) must never clobber the current one.</summary>
    public void HandleTipTriggerExit(Collider other)
    {
        if (touchingMarker != null && other.GetComponentInParent<EvidenceFingerprintMarker>() == touchingMarker)
        {
            touchingMarker = null;
        }
    }

    private void ApplyCleanVisual()
    {
        if (cleanVisual != null)
        {
            cleanVisual.SetActive(true);
        }

    }

    /// <summary>Convert the tape result into held evidence using the existing model
    /// and custody owner. No collider is needed on the card: B reads held custody.</summary>
    public bool TryHoldLiftedPrint(string sourceId)
    {
        var rig = FindFirstObjectByType<OVRCameraRig>();
        return liftedVisualPrefab != null && rig != null && rig.rightHandAnchor != null
            && HeldItemCoordinator.Instance != null
            && HeldItemCoordinator.Instance.HoldLiftedFingerprint(sourceId, liftedVisualPrefab, rig.rightHandAnchor);
    }
}
