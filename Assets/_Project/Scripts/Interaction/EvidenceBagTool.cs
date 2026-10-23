// Assets/_Project/Scripts/Interaction/EvidenceBagTool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;

/// <summary>
/// Replaces the old raycast-based EvidenceCollectorTool outright. Collection and
/// sealing are no longer aim-and-press on a single button; they are a physically
/// embodied two-handed gesture - right hand closes its GRIP on the evidence (not the
/// trigger - a separate, distinct input, see rightGripAction), left hand holds this
/// bag with its TRIGGER held open, and bringing them together bags the item. Closing
/// the bag (releasing the left trigger) is what seals it. One gesture cycle produces
/// both existing state transitions (MarkCollected, then MarkSealed) in their existing
/// order; RequiredSequence is untouched, this is interaction-layer only.
///
/// Hand assignment is enforced, not incidental: PreferredHand + EquipToHand refuse
/// anything but the left anchor for this tool, and RightHandOnlyFilter (attached to
/// each evidence prop's Grab/HandGrabInteractable) refuses any interactor that isn't
/// the right hand's, so evidence can never be grabbed with the hand meant to be
/// holding the bag.
///
/// The role/capability this represents (ToolType.EvidenceCollector) hasn't changed,
/// only the physical object and gesture behind it - a bag, not a magnifying glass.
///
/// TryInsert/TrySeal are public specifically so GreyboxFlowTest can call the real
/// gated state-changing logic directly - the test still doesn't simulate VR input
/// (see GreyboxFlowTest's own class comment), but it must not go around this tool's
/// logic by calling EvidenceStateManager.MarkCollected/MarkSealed on its own.
/// </summary>
public class EvidenceBagTool : PlayerTool
{
    public static EvidenceBagTool Instance { get; private set; }

    [Header("Input (XRI Input Reader pattern)")]
    [Tooltip("Left trigger held = bag open.")]
    [SerializeField] private InputActionReference leftActivateAction;
    [Tooltip("Right GRIP (not trigger - a separate, distinct input) held = the right hand is closing on evidence. XRI Right Interaction/Select, the same action ToggleGrab already reads for its own release button - reused rather than inventing a new one.")]
    [SerializeField] private InputActionReference rightGripAction;

    [Header("Grasp proximity")]
    [Tooltip("How close the right hand anchor must be to the nearest point on an evidence item's solid colliders for it to count as genuinely grasped - deliberately small (real reach-and-touch), not the interactionRadius-scale volume used for Found/Marked.")]
    [SerializeField] private float grabReachDistance = 0.15f;

    [Header("Receiving zone")]
    [Tooltip("The bag's opening - a trigger collider on a child GameObject (see EvidenceBagReceiver). PlayerTool.SetEquippedVisualState disables EVERY collider under this GameObject (root included) whether equipped or not, so this one has to be explicitly re-enabled/disabled here in step with EquipToHand/Holster, or the bag would never detect anything, equipped or not.")]
    [SerializeField] private Collider receivingZoneCollider;

    [Header("Audio")]
    [Tooltip("Plays on a successful TryInsert - the item dropping into the bag.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip insertClip;

    // The item currently bagged but not yet sealed, or null. Only one at a time by
    // design - a second item can't be inserted while this is set.
    private EvidenceProp insertedItem;

    private OVRCameraRig cameraRig;
    private GrabInteractor rightGrabInteractor;

    public override ToolType ToolRole => ToolType.EvidenceCollector;

    /// <summary>The bag must occupy the left hand - the right needs to be free to grab evidence. See EquipToHand for the hard enforcement, not just this preference.</summary>
    public override Hand PreferredHand => Hand.Left;

    /// <summary>True while the bag is being held and its trigger is pressed - the bag reads as "open."</summary>
    public bool IsOpen => IsHeld && leftActivateAction != null && leftActivateAction.action.IsPressed();

    protected override void Awake()
    {
        base.Awake();
        Instance = this;

        // base.Awake() just disabled every collider under this GameObject via
        // SetEquippedVisualState(false), the receiving zone included - correct for
        // "don't catch evidence while holstered," so leave it off until EquipToHand.
    }

    private void EnsureCameraRig()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
        if (cameraRig != null && rightGrabInteractor == null)
        {
            foreach (var grab in cameraRig.GetComponentsInChildren<GrabInteractor>(true))
            {
                if (grab.GetComponentInParent<IController>()?.Handedness != Handedness.Right) continue;
                rightGrabInteractor = grab;
                break;
            }
        }
    }

    /// <summary>
    /// Hard enforcement, not just PreferredHand's preference: the bag refuses to
    /// attach to anything but the left hand anchor. PreferredHand is what makes
    /// ToolWheelController offer the left anchor in the first place; this is the
    /// backstop so a future caller that bypasses the wheel can't put it on the right
    /// hand and quietly break "the right hand is always free to grab evidence."
    /// </summary>
    public override void EquipToHand(Transform handAnchor)
    {
        EnsureCameraRig();
        if (cameraRig != null && handAnchor == cameraRig.rightHandAnchor)
        {
            Debug.LogWarning("[EvidenceBagTool] Refused to equip to the right hand anchor - the bag is left-hand only.", this);
            return;
        }

        base.EquipToHand(handAnchor);

        // base.EquipToHand only re-enables renderers, not colliders (PlayerTool keeps
        // every tool's own physical collider off deliberately - see its comment on
        // SetEquippedVisualState). The receiving zone is a functionally different
        // collider (a detector, not something a hand could grab), so it has to be
        // switched back on explicitly here rather than inheriting that rule.
        if (receivingZoneCollider != null)
        {
            receivingZoneCollider.enabled = true;
        }
    }

    public override void Holster()
    {
        if (receivingZoneCollider != null)
        {
            receivingZoneCollider.enabled = false;
        }

        base.Holster();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        leftActivateAction?.action.Enable();
        rightGripAction?.action.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        leftActivateAction?.action.Disable();
        rightGripAction?.action.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        // Sealing: releasing the left trigger while something is currently inserted.
        // Only while genuinely held - dropping/holstering the bag mid-carry isn't a
        // deliberate seal action.
        if (insertedItem != null && IsHeld && leftActivateAction != null && !leftActivateAction.action.IsPressed())
        {
            TrySeal();
        }
    }

    /// <summary>Called by EvidenceBagReceiver whenever something overlaps the bag's receiving zone.</summary>
    public void HandleReceiverOverlap(Collider other)
    {
        if (insertedItem != null || !IsOpen)
        {
            return;
        }

        var evidence = ResolveCarriedEvidence(other);
        if (insertedItem != null || PlayerUIGate.IsBlocked || ToolInputLock.IsLocked
            || evidence == null || string.IsNullOrEmpty(evidence.evidenceId))
        {
            return;
        }

        if (!IsFirmlyGrasped(evidence))
        {
            return;
        }

        TryInsert(evidence);
    }

    /// <summary>
    /// Right grip held AND the right grab point is genuinely close to this specific item -
    /// real reach-and-touch, not a wide capture zone. Deliberately NOT sourced from
    /// Oculus Interaction's own Grabbable.SelectingPointsCount: that state depends on
    /// ActiveStateTracker reporting a connected controller, which this project's own
    /// InputSystem-based simulated verification can never drive (confirmed - it's a
    /// device-presence gate underneath the grip button, not the grip button itself),
    /// so a check built on it could never be exercised by this project's own tooling.
    /// This reads the same XRI grip action and SDK grab point used by
    /// RightHandOnlyFilter, rather than the tracked anchor (which can be offset from
    /// the point actually holding the evidence), so both press and
    /// proximity are things a simulated InputSystem device press can genuinely satisfy,
    /// the same way the trigger already was for every other tool.
    ///
    /// Stateless by construction: recomputed fresh every call, nothing latched. Grip
    /// released means this returns false on the very next check - no toggle, no
    /// stickiness, matching "evidence occupies the hand for as long as it's held" as
    /// opposed to ToggleGrab's click-to-hold model (deliberately not used on evidence).
    /// </summary>
    private bool IsFirmlyGrasped(EvidenceProp evidence)
    {
        if (rightGripAction == null || !rightGripAction.action.IsPressed())
        {
            return false;
        }

        EnsureCameraRig();
        if (cameraRig == null || cameraRig.rightHandAnchor == null)
        {
            return false;
        }

        var grabPoint = rightGrabInteractor != null
            ? RightHandOnlyFilter.GrabPoint(rightGrabInteractor.gameObject)
            : cameraRig.rightHandAnchor.position;
        return DistanceToItemSurface(grabPoint, evidence) <= grabReachDistance;
    }

    // Nested EvidenceProps (e.g. a latent-print marker on a knife handle) share the
    // parent's rigidbody but aren't the portable item, so the carried body wins.
    private static EvidenceProp ResolveCarriedEvidence(Collider other)
    {
        var body = other.attachedRigidbody;
        var carried = body != null ? body.GetComponent<EvidenceProp>() : null;
        return carried != null ? carried : other.GetComponentInParent<EvidenceProp>();
    }

    // Measured to the item's solid geometry, not its pivot: a long item held by one
    // end (the knife by its handle) has its pivot well outside grabReachDistance.
    private static float DistanceToItemSurface(Vector3 point, EvidenceProp evidence)
    {
        float best = Vector3.Distance(point, evidence.transform.position);
        foreach (var collider in evidence.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || (collider is MeshCollider mesh && !mesh.convex))
            {
                continue;
            }

            best = Mathf.Min(best, Vector3.Distance(point, collider.ClosestPoint(point)));
        }
        return best;
    }

    /// <summary>
    /// The real, gated insertion action: refuses via the same procedural gate every
    /// other tool uses, then hides the loose visual, force-releases whatever hand grab is
    /// holding it, parents it to the bag, disables it from being independently
    /// re-grabbed, and reports MarkCollected. Public so GreyboxFlowTest can exercise
    /// this exact logic directly - see the class comment.
    /// </summary>
    public TransitionResult TryInsert(EvidenceProp evidence)
    {
        if (insertedItem != null || PlayerUIGate.IsBlocked || ToolInputLock.IsLocked
            || evidence == null || string.IsNullOrEmpty(evidence.evidenceId))
        {
            return TransitionResult.Violation;
        }

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanCollect(evidence.evidenceId))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidence.evidenceId);
            Debug.Log($"[EvidenceBagTool] Can't collect {evidence.evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            InteractionFeedback.Blocked();
            return TransitionResult.Violation;
        }

        // Insertion contains the loose object; the bag remains open until the authored seal gesture.
        foreach (var renderer in evidence.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        ForceReleaseAnyGrab(evidence.gameObject);
        DisablePhysicalPresence(evidence);
        evidence.transform.SetParent(transform, true);

        insertedItem = evidence;

        if (audioSource != null && insertClip != null)
        {
            audioSource.PlayOneShot(insertClip);
        }

        ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkCollected(id, tool), "collected");
        return TransitionResult.Applied;
    }

    /// <summary>
    /// The real, gated sealing action: seals whatever is currently inserted, then
    /// holds its closed representation through the existing inventory owner. B stores custody separately.
    /// No longer detaches the item to a holding crate: that automatic box-teleport is
    /// gone by design (see this feature's own requirement that evidence only ever
    /// reaches the box via a deliberate EvidenceBoxDepositZone deposit). Public for the
    /// same reason as TryInsert.
    /// </summary>
    public TransitionResult TrySeal()
    {
        var evidence = insertedItem;
        if (evidence == null)
        {
            return TransitionResult.Violation;
        }

        if (ProceduralGateValidator.Instance != null
            && !ProceduralGateValidator.Instance.CanTransition(evidence.evidenceId, EvidenceStatus.Sealed))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidence.evidenceId, EvidenceStatus.Sealed);
            Debug.Log($"[EvidenceBagTool] Can't seal {evidence.evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            InteractionFeedback.Blocked();
            return TransitionResult.Violation;
        }

        // Validate custody/visual access before advancing the existing sealing step.
        EnsureCameraRig();
        var rightHandAnchor = cameraRig != null ? cameraRig.rightHandAnchor : null;
        if (PlayerUIGate.IsBlocked || ToolInputLock.IsLocked || rightHandAnchor == null
            || HeldItemCoordinator.Instance == null || EvidenceInventory.Instance == null
            || !EvidenceInventory.Instance.CanAcceptBaggedEvidence(evidence.evidenceId))
        {
            GameDialogue.Say("bag-seal-failed");
            return TransitionResult.Violation;
        }

        ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkSealed(id, tool), "sealed");
        if (!EvidenceStateManager.Instance.IsAtOrPastStatus(evidence.evidenceId, EvidenceStatus.Sealed)
            || !HeldItemCoordinator.Instance.HoldBaggedEvidence(evidence.evidenceId, rightHandAnchor))
        {
            // Retain the same contained item for retry; never silently store or discard it.
            GameDialogue.Say("bag-seal-failed");
            return TransitionResult.Violation;
        }

        insertedItem = null;
        return TransitionResult.Applied;
    }

    // Copied first, so ForceRelease (which mutates the live SelectingInteractors set)
    // never invalidates the enumeration it's running inside.
    private static void ForceReleaseAnyGrab(GameObject go)
    {
        var handGrabInteractable = go.GetComponent<HandGrabInteractable>();
        if (handGrabInteractable != null)
        {
            foreach (var interactor in new List<HandGrabInteractor>(handGrabInteractable.SelectingInteractors))
            {
                interactor.ForceRelease();
            }
        }

        var grabInteractable = go.GetComponent<GrabInteractable>();
        if (grabInteractable != null)
        {
            foreach (var interactor in new List<GrabInteractor>(grabInteractable.SelectingInteractors))
            {
                interactor.ForceRelease();
            }
        }
    }

    // A bagged item is no longer independently interactable - no more grabbing it back
    // out, no more Found-trigger, no more aim-raycast hits. EvidenceGrabGate's earlier
    // enable is superseded here rather than relied on to stay correct.
    private static void DisablePhysicalPresence(EvidenceProp evidence)
    {
        foreach (var collider in evidence.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        var handGrabInteractable = evidence.GetComponent<HandGrabInteractable>();
        if (handGrabInteractable != null)
        {
            handGrabInteractable.enabled = false;
        }

        var grabInteractable = evidence.GetComponent<GrabInteractable>();
        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        }
    }

}
