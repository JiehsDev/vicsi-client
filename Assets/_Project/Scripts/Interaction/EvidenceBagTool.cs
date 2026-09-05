// Assets/_Project/Scripts/Interaction/EvidenceBagTool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

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
/// gated state-changing logic directly, the same discipline already applied to
/// MasterSketchManager.RecordAnnotation: the test still doesn't simulate VR input (see
/// GreyboxFlowTest's own class comment), but it must not go around this tool's logic
/// by calling EvidenceStateManager.MarkCollected/MarkSealed on its own.
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
    [Tooltip("How close the right hand anchor must be to an evidence item's own position for it to count as genuinely grasped - deliberately small (real reach-and-touch), not the interactionRadius-scale volume used for Found/Marked.")]
    [SerializeField] private float grabReachDistance = 0.15f;

    [Header("Bagged placeholder")]
    [Tooltip("Local scale applied to the placeholder mesh once an item is bagged. Independent of whatever scale the item's real art was authored at.")]
    [SerializeField] private Vector3 baggedLocalScale = new Vector3(0.12f, 0.12f, 0.12f);

    [Header("Sealed items")]
    [Tooltip("Where a sealed item is auto-detached to once the bag closes, freeing the bag for the next item. Optional - if unset, the item is simply un-parented in place.")]
    [SerializeField] private Transform holdingCrate;

    [Header("Receiving zone")]
    [Tooltip("The bag's opening - a trigger collider on a child GameObject (see EvidenceBagReceiver). PlayerTool.SetEquippedVisualState disables EVERY collider under this GameObject (root included) whether equipped or not, so this one has to be explicitly re-enabled/disabled here in step with EquipToHand/Holster, or the bag would never detect anything, equipped or not.")]
    [SerializeField] private Collider receivingZoneCollider;

    private static Mesh baggedPlaceholderMesh;
    private static Material baggedPlaceholderMaterial;

    // The item currently bagged but not yet sealed, or null. Only one at a time by
    // design - a second item can't be inserted while this is set.
    private EvidenceProp insertedItem;
    private int sealedCount;

    private OVRCameraRig cameraRig;

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

        var evidence = other.GetComponentInParent<EvidenceProp>();
        if (evidence == null || string.IsNullOrEmpty(evidence.evidenceId))
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
    /// Right grip held AND the right hand is genuinely close to this specific item -
    /// real reach-and-touch, not a wide capture zone. Deliberately NOT sourced from
    /// Oculus Interaction's own Grabbable.SelectingPointsCount: that state depends on
    /// ActiveStateTracker reporting a connected controller, which this project's own
    /// InputSystem-based simulated verification can never drive (confirmed - it's a
    /// device-presence gate underneath the grip button, not the grip button itself),
    /// so a check built on it could never be exercised by this project's own tooling.
    /// This reads a project-owned signal instead - the same XRI grip action ToggleGrab
    /// already uses, plus a distance check we compute ourselves - so both press and
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

        float distance = Vector3.Distance(cameraRig.rightHandAnchor.position, evidence.transform.position);
        return distance <= grabReachDistance;
    }

    /// <summary>
    /// The real, gated insertion action: refuses via the same procedural gate every
    /// other tool uses, then swaps the visual, force-releases whatever hand grab is
    /// holding it, parents it to the bag, disables it from being independently
    /// re-grabbed, and reports MarkCollected. Public so GreyboxFlowTest can exercise
    /// this exact logic directly - see the class comment.
    /// </summary>
    public TransitionResult TryInsert(EvidenceProp evidence)
    {
        if (evidence == null || string.IsNullOrEmpty(evidence.evidenceId))
        {
            return TransitionResult.Violation;
        }

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanCollect(evidence.evidenceId))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidence.evidenceId);
            Debug.Log($"[EvidenceBagTool] Can't collect {evidence.evidenceId}: {reason}");
            NotificationManager.Notify(reason);
            InteractionFeedback.Blocked();
            return TransitionResult.Violation;
        }

        ApplyBaggedVisual(evidence);
        ForceReleaseAnyGrab(evidence.gameObject);
        DisablePhysicalPresence(evidence);
        evidence.transform.SetParent(transform, true);

        insertedItem = evidence;

        ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkCollected(id, tool), "collected");
        return TransitionResult.Applied;
    }

    /// <summary>
    /// The real, gated sealing action: seals whatever is currently inserted and
    /// detaches it to the holding crate, freeing the bag. Public for the same reason
    /// as TryInsert.
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
            NotificationManager.Notify(reason);
            InteractionFeedback.Blocked();
            return TransitionResult.Violation;
        }

        ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkSealed(id, tool), "sealed");

        DetachToCrate(evidence);
        insertedItem = null;
        return TransitionResult.Applied;
    }

    private void ApplyBaggedVisual(EvidenceProp evidence)
    {
        var meshFilter = evidence.GetComponentInChildren<MeshFilter>();
        var meshRenderer = evidence.GetComponentInChildren<MeshRenderer>();
        if (meshFilter == null || meshRenderer == null)
        {
            return;
        }

        meshFilter.sharedMesh = GetBaggedPlaceholderMesh();
        meshRenderer.sharedMaterial = GetBaggedPlaceholderMaterial();
        meshFilter.transform.localScale = baggedLocalScale;
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

    private void DetachToCrate(EvidenceProp evidence)
    {
        if (holdingCrate != null)
        {
            evidence.transform.SetParent(holdingCrate, false);
            // Simple grid offset so sealed items don't all stack exactly on top of one
            // another - cosmetic only, not a scoring signal.
            evidence.transform.localPosition = new Vector3((sealedCount % 3) * 0.12f, 0.05f, (sealedCount / 3) * 0.12f);
            evidence.transform.localRotation = Quaternion.identity;
        }
        else
        {
            evidence.transform.SetParent(null, true);
        }

        sealedCount++;
    }

    private static Mesh GetBaggedPlaceholderMesh()
    {
        if (baggedPlaceholderMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baggedPlaceholderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }
        return baggedPlaceholderMesh;
    }

    // Generic translucent "bagged" material, generated rather than shipped - same
    // reasoning as InteractionFeedback's procedurally generated audio clips: nothing
    // here depends on an art asset existing.
    private static Material GetBaggedPlaceholderMaterial()
    {
        if (baggedPlaceholderMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f); // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.color = new Color(0.85f, 0.85f, 0.8f, 0.55f);
            baggedPlaceholderMaterial = mat;
        }
        return baggedPlaceholderMaterial;
    }
}
