// Assets/_Project/Scripts/Interaction/SwabTool.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

/// <summary>
/// Single combined tool for collecting a blood sample: the cotton bud attaches to
/// the right hand and its sample container attaches to the left hand from one wheel
/// equip - not two separately-equipped tools. Interaction is click-based throughout:
/// touch the tip to ready evidence and press the right trigger once to take a
/// sample, bring the sampled tip to the temporary carrier and press again to collect.
/// Prepare it at the outside drying/packaging station before sealing and storage.
/// No hold-and-release sequencing, no haptic/tone feedback - a
/// model/material swap on the tip (the real cotton-swab model replaced by a real
/// blood-stained one, see cleanTipMesh/sampledTipMesh) and a plain notification are
/// the only feedback this needs.
///
/// Tracks SampledFrom as a real EvidenceProp reference, not a hardcoded id - this is
/// what lets multiple independent swabbable items (e.g. blood spatter, EVD-015)
/// coexist without a sample taken from one ever being credited to another.
/// TryContain/TrySeal both operate on whichever item TrySwab actually recorded,
/// never on an assumed id.
///
/// A SECOND, separate contact type also lives here: EvidenceSwabRequirement marks a
/// physical trace (blood on a knife blade) that must be swabbed before its PARENT
/// item can be collected, without being an independent evidence item of its own -
/// see that class's own comment for why a co-located independent item didn't work
/// (PhotographTool's camera-distance targeting couldn't disambiguate the two).
/// It goes through the exact same take-sample -> carry-to-container -> insert
/// gesture as a real EvidenceProp sample (TrySampleRequiredSwab mirrors TrySwab),
/// tracked via the parallel sampledRequirement field so TryContain/TrySeal know to
/// report EvidenceStateManager.MarkSwabbed against the marker's parent id instead of
/// MarkCollected/MarkSealed. Touching the PARENT item's own body/aim collider is
/// deliberately refused by TrySwab (see its own comment) - only the marker itself
/// ever produces a sample for this item.
/// </summary>
public class SwabTool : PlayerTool
{
    public static SwabTool Instance { get; private set; }

    /// <summary>Which evidence item the current/most recent sample was actually taken from, or null if the tip is clean. Set by TrySwab, read by TryContain/TrySeal instead of any assumed id - this is the whole fix for cross-item attribution.</summary>
    public EvidenceProp SampledFrom { get; private set; }
    public bool HasFreshSwab { get; private set; }
    public bool SupplyRequested { get; private set; }
    public void RequestFreshSupply() { SupplyRequested=true; }
    private string lastSampleSourceId;
    public enum PreparationStage { None, NeedsDrying, Drying, Dried, Packaged }
    public PreparationStage Preparation { get; private set; }
    public float DryingProgress { get; private set; }
    public const float SimulatedDryingSeconds = 5f;
    public string PreparationSourceId => awaitingSeal ? lastSampleSourceId : null;
    public string PendingSampleSourceId => hasSample ? sampledRequirement?.evidenceId ?? SampledFrom?.evidenceId : PreparationSourceId;
    public bool NeedsPreparation => awaitingSeal && Preparation != PreparationStage.Packaged;
    public bool IsInPreparationCabinet => Preparation == PreparationStage.Drying || Preparation == PreparationStage.Dried;
    internal Transform PreparationCarrier => heldSampleId != null ? EvidenceInventory.Instance?.HeldVisual?.transform : containerVisual;
    internal GameObject CreateDryingVisual(Transform parent)
    {
        if(sampledTipMesh==null || sampledTipMaterial==null)return null;
        var visual=new GameObject("Exposed swab in protected drying holder",typeof(MeshFilter),typeof(MeshRenderer));
        visual.transform.SetParent(parent,false);visual.GetComponent<MeshFilter>().sharedMesh=sampledTipMesh;
        visual.GetComponent<MeshRenderer>().sharedMaterial=sampledTipMaterial;
        var bounds=sampledTipMesh.bounds;var size=bounds.size;
        Vector3 axis=size.x>=size.y&&size.x>=size.z?Vector3.right:size.y>=size.z?Vector3.up:Vector3.forward;
        float scale=.16f/Mathf.Max(.0001f,Mathf.Max(size.x,Mathf.Max(size.y,size.z)));
        visual.transform.localRotation=Quaternion.FromToRotation(axis,Vector3.up);visual.transform.localScale=Vector3.one*scale;
        visual.transform.localPosition=Vector3.up*.05f-visual.transform.localRotation*(bounds.center*scale);
        return visual;
    }
    internal bool BeginDrying(SamplePreparationStation station)
    {
        if (station == null || station != SamplePreparationStation.Instance || !station.CanInteract
            || Preparation != PreparationStage.NeedsDrying || !awaitingSeal) return false;
        Preparation = PreparationStage.Drying; DryingProgress = 0;
        SessionLogger.Instance?.LogEvent(SessionEventType.SampleDryingStarted, PreparationSourceId);
        return true;
    }
    internal void AdvanceDrying(SamplePreparationStation station, float seconds)
    {
        if (station == null || station != SamplePreparationStation.Instance || !station.CanInteract
            || Preparation != PreparationStage.Drying) return;
        DryingProgress = Mathf.Clamp01(DryingProgress + Mathf.Max(0, seconds) / SimulatedDryingSeconds);
        if (DryingProgress < 1) return;
        Preparation = PreparationStage.Dried;
        SessionLogger.Instance?.LogEvent(SessionEventType.SampleDryingCompleted, PreparationSourceId);
    }
    internal bool ConfirmPreparedPackage(SamplePreparationStation station)
    {
        if (station == null || station != SamplePreparationStation.Instance || !station.CanInteract
            || Preparation != PreparationStage.Dried) return false;
        Preparation = PreparationStage.Packaged;
        InteractionSfx.Play(InteractionSound.Paper,station.transform.position);
        SessionLogger.Instance?.LogEvent(SessionEventType.SamplePackaged, PreparationSourceId);
        return true;
    }
    public bool AwaitingSampleStorage => lastSampleSourceId != null && (EvidenceInventory.Instance == null
        || !EvidenceInventory.Instance.AllEntryIds.Any(id=>EvidenceInventory.Instance.GetSampleSource(id)==lastSampleSourceId
            && !EvidenceInventory.Instance.IsFingerprintSample(id) && EvidenceInventory.Instance.IsSampleSealed(id)
            && EvidenceInventory.Instance.IsExplicitlyStoredSample(id)));
    public bool NeedsFreshSupply => !HasFreshSwab && !HasUnfinishedSample && !AwaitingSampleStorage;
    public bool CanEquipCurrent => HasFreshSwab || hasSample || (awaitingSeal && heldSampleId == null);
    public string AvailabilityDialogueId => hasSample?"swab-finish-loaded"
        :NeedsPreparation?"sample-finish-preparation"
        :HasUnfinishedSample||AwaitingSampleStorage?"swab-finish-container":"swab-supply-required";
    public string AvailabilityReason => GameDialogue.Text(AvailabilityDialogueId);
    internal bool LoadFreshSwab(ArrivalPPEStation station)
    {
        if(station==null || station!=ArrivalPPEStation.Instance || !station.CanLoadSwabPack || !NeedsFreshSupply) return false;
        HasFreshSwab=true; SupplyRequested=false; lastSampleSourceId=null; ApplyCleanVisual();
        if(tipRenderer!=null)tipRenderer.enabled=IsHeld;
        SessionLogger.Instance?.LogEvent(SessionEventType.FreshSwabLoaded,"swab-supply");
        return true;
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void PrepareDevelopmentSwab()
    {
        if(!AutomatedTestSandbox.IsActive)throw new System.InvalidOperationException("Swab preparation requires the sandbox.");
        if(HasUnfinishedSample || AwaitingSampleStorage)throw new System.InvalidOperationException("Cannot replace a pending sample.");
        HasFreshSwab=true;SupplyRequested=false;lastSampleSourceId=null;ApplyCleanVisual();
    }
#endif

    [Header("Cotton bud tip")]
    [Tooltip("Trigger collider on the tip - detects both the blood spatter surface and the container's receiving zone. PlayerTool.SetEquippedVisualState disables every collider under this GameObject in both equip states, so this has to be explicitly re-enabled/disabled in step with EquipToHand/Holster.")]
    [SerializeField] private Collider tipCollider;
    [Tooltip("Renderer whose mesh/material swap between clean and sampled. Its MeshFilter is resolved from this same GameObject in Awake, not a separate serialized field, since the two are always on the tip together.")]
    [SerializeField] private Renderer tipRenderer;

    [Header("Cotton bud tip - real models (optional)")]
    [Tooltip("The real clean cotton-swab model (SWAB_30K) and its material. Leave blank to fall back to the generic placeholder colour swap (GetCleanMaterial/GetSampledMaterial) with no mesh change - e.g. for a scene with no art wired yet.")]
    [SerializeField] private Mesh cleanTipMesh;
    [SerializeField] private Material cleanTipMaterial;
    [Tooltip("The real blood-stained cotton-swab model (SWAB_BLOOD) and its material, swapped in the moment a sample is taken - the blood visibly appears ON the swab itself, not just a tint on the placeholder shape.")]
    [SerializeField] private Mesh sampledTipMesh;
    [SerializeField] private Material sampledTipMaterial;

    private MeshFilter tipMeshFilter;

    [Header("Container (left hand)")]
    [Tooltip("Separate object for the sample container - not a PlayerTool itself. Reparented to the left hand alongside this tool's own right-hand equip, and back to its authored position on holster.")]
    [SerializeField] private Transform containerVisual;
    [Tooltip("The container's own trigger collider - detects the tip entering.")]
    [SerializeField] private Collider containerReceivingZone;
    [Tooltip("Existing held container prefab, backed by CONTAINER_WITH_SWAB.glb; used only after valid insertion.")]
    [SerializeField] private GameObject completedContainerPrefab;
    private string heldSampleId;
    public string PendingContainerId => heldSampleId;
    [SerializeField] private Vector3 containerEquipLocalPosition;
    [SerializeField] private Vector3 containerEquipLocalEulerAngles;

    [Header("Input (XRI Input Reader pattern)")]
    [Tooltip("Right trigger, read as a discrete press (WasPressedThisFrame) rather than a hold. One click swabs, contains, or seals depending on what the tip is touching and the sample's current stage.")]
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Audio")]
    [Tooltip("Plays on a successful TrySwab/TrySampleRequiredSwab - the moment a sample is actually taken.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swabbingClip;
    [Tooltip("Plays on a successful TrySeal - closing the container.")]
    [SerializeField] private AudioClip containerCloseClip;

    private static Material cleanMaterial;
    private static Material sampledMaterial;

    private Transform containerHomeParent;
    private Vector3 containerHomeLocalPosition;
    private Quaternion containerHomeLocalRotation;

    // Where the current sample is in its own tiny lifecycle - not a general evidence
    // status, just enough to know what the next click should do.
    private bool hasSample;
    private bool awaitingSeal;

    /// <summary>Exposed for GreyboxFlowTest reporting only - not used by any gameplay logic.</summary>
    public bool HasSample => hasSample;
    public bool HasUnfinishedSample => hasSample || awaitingSeal;

    /// <summary>Successful containment, after the held container exists; not mere swab contact.</summary>
    public static event System.Action<string> OnSampleContained;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTrainingEvent() => OnSampleContained = null;

    // Whatever EvidenceProp the tip is currently touching, regardless of which item
    // it is - the state-machine gate inside TrySwab is what actually decides
    // eligibility (must be ReadyForCollection or later), not an allowlist here. Same
    // "the gate decides, not the tool" principle every other tool in this project
    // already follows.
    private EvidenceProp tipContact;

    // A required-swab marker the tip is currently touching, if any - see
    // EvidenceSwabRequirement's own comment.
    private EvidenceSwabRequirement requiredSwabContact;
    private bool touchingContainer;

    // Which required-swab marker the tip's CURRENT sample came from, or null - the
    // required-swab mirror of SampledFrom. Exactly one of SampledFrom/sampledRequirement
    // is non-null whenever hasSample is true; TryContain/TrySeal branch on whichever is
    // set to know whether to report MarkCollected/MarkSealed (a real EvidenceProp) or
    // MarkSwabbed (a precondition marker's parent item).
    private EvidenceSwabRequirement sampledRequirement;

    private OVRCameraRig cameraRig;

    public override ToolType ToolRole => ToolType.Swab;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;

        if (tipRenderer != null)
        {
            tipMeshFilter = tipRenderer.GetComponent<MeshFilter>();
        }

        if (containerVisual != null)
        {
            containerHomeParent = containerVisual.parent;
            containerHomeLocalPosition = containerVisual.localPosition;
            containerHomeLocalRotation = containerVisual.localRotation;
            SetContainerVisible(false);
        }

        ApplyCleanVisual();
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

    private void EnsureCameraRig()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
    }

    /// <summary>Attaches the cotton bud to whichever hand the wheel offers (right, per PlayerTool's default), and the container to the left hand in the same gesture - one equip, both hands.</summary>
    public override void EquipToHand(Transform handAnchor)
    {
        if(!CanEquipCurrent) return;
        base.EquipToHand(handAnchor);
        if(tipRenderer!=null)tipRenderer.enabled=HasFreshSwab||hasSample||awaitingSeal;

        if (tipCollider != null)
        {
            tipCollider.enabled = true;
        }

        EnsureCameraRig();
        if (containerVisual != null && cameraRig != null && cameraRig.leftHandAnchor != null)
        {
            containerVisual.SetParent(cameraRig.leftHandAnchor, false);
            containerVisual.localPosition = containerEquipLocalPosition;
            containerVisual.localRotation = Quaternion.Euler(containerEquipLocalEulerAngles);
            SetContainerVisible(true);
        }
    }

    public override void Holster()
    {
        if (tipCollider != null)
        {
            tipCollider.enabled = false;
        }

        if (containerVisual != null)
        {
            containerVisual.SetParent(containerHomeParent, false);
            containerVisual.localPosition = containerHomeLocalPosition;
            containerVisual.localRotation = containerHomeLocalRotation;
            SetContainerVisible(false);
        }

        base.Holster();
    }

    private void SetContainerVisible(bool visible)
    {
        if (containerVisual == null)
        {
            return;
        }

        foreach (var renderer in containerVisual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }

        if (containerReceivingZone != null)
        {
            containerReceivingZone.enabled = visible;
        }
    }

    /// <summary>Called by SwabTipReceiver whenever the tip's trigger overlap changes. Tracks membership only - the actual click check happens once per frame in Update, not here, so a discrete WasPressedThisFrame read is never at the mercy of physics-callback timing.</summary>
    public void HandleTipTriggerEnter(Collider other)
    {
        if (other == containerReceivingZone)
        {
            touchingContainer = true;
            return;
        }

        var swabRequirement = other.GetComponentInParent<EvidenceSwabRequirement>();
        if (swabRequirement != null && !string.IsNullOrEmpty(swabRequirement.evidenceId))
        {
            requiredSwabContact = swabRequirement;
            return;
        }

        var evidence = other.GetComponentInParent<EvidenceProp>();
        if (evidence != null && !string.IsNullOrEmpty(evidence.evidenceId) && IsEverSwabbable(evidence.evidenceId))
        {
            tipContact = evidence;
        }
    }

    /// <summary>
    /// Whether evidenceId could ever be relevant to this tool under any circumstance -
    /// either its OWN collection method is a swab (EvidenceDefinition.collectedBySwab,
    /// e.g. EVD-015) or it has a swabbed precondition on some OTHER path
    /// (EvidenceDefinition.requiresSwabbing, e.g. the knife/gloves - touching their own
    /// body, as opposed to their EvidenceSwabRequirement marker, still needs to reach
    /// TrySwab's existing silent "swab the marker instead" refusal, so this stays true
    /// for them too). False only for an item that could never be swabbed by SwabTool
    /// under any circumstance (e.g. the phone, the bottle) - see this method's own call
    /// site for why gating registration here, not just the eventual TrySwab call,
    /// matters: SwabTool is the only tip-contact-based tool in this project (every
    /// other tool aims via raycast), so brushing the tip against an unrelated prop
    /// while moving around is a real, frequent occurrence a raycast tool would never
    /// produce - it must never register as tipContact at all, or a press would run it
    /// through the real gate and surface a "Not yet ready" toast for an item the player
    /// never intended to touch. A genuine swab target (either kind) still needs that
    /// exact toast when touched too early - see TrySwab/TrySampleRequiredSwab's own
    /// "not yet ready" branches, both still reachable, unchanged, once eligibility
    /// passes here.
    /// </summary>
    private static bool IsEverSwabbable(string evidenceId)
    {
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        return record?.definition != null && (record.definition.requiresSwabbing || record.definition.collectedBySwab);
    }

    public void HandleTipTriggerExit(Collider other)
    {
        if (other == containerReceivingZone)
        {
            touchingContainer = false;
            return;
        }

        if (requiredSwabContact != null && other.GetComponentInParent<EvidenceSwabRequirement>() == requiredSwabContact)
        {
            requiredSwabContact = null;
            return;
        }

        if (tipContact != null && other.GetComponentInParent<EvidenceProp>() == tipContact)
        {
            tipContact = null;
        }
    }

    private void Update()
    {
        if(IsInPreparationCabinet) return;
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLockedFor(ToolRole))
        {
            return;
        }

        bool heldContainer = heldSampleId != null && EvidenceInventory.Instance?.CurrentHeldEvidenceId == heldSampleId;
        if ((!IsHeld && !heldContainer) || rightActivateAction == null || !rightActivateAction.action.WasPressedThisFrame())
        {
            return;
        }

        if ((touchingContainer || heldContainer) && awaitingSeal)
        {
            TrySeal();
        }
        else if (touchingContainer && hasSample)
        {
            TryContain();
        }
        else if (requiredSwabContact != null)
        {
            TrySampleRequiredSwab(requiredSwabContact);
        }
        else if (tipContact != null)
        {
            TrySwab(tipContact);
        }
    }

    /// <summary>
    /// The real, gated sampling action: refuses via the same procedural gate every
    /// other tool uses, then swaps the visual and records which item the sample came
    /// from (SampledFrom). Public so GreyboxFlowTest can exercise this exact logic
    /// directly.
    /// </summary>
    public bool TrySwab(EvidenceProp evidence)
    {
        if (evidence == null || string.IsNullOrEmpty(evidence.evidenceId) || hasSample || awaitingSeal)
        {
            return false;
        }

        // Items with a required biological trace (EvidenceDefinition.requiresSwabbing,
        // e.g. the blood on the kitchen knife's blade) are swabbed exclusively at their
        // own EvidenceSwabRequirement marker via TrySampleRequiredSwab - never generically
        // off the item's own body/collider here. Without this guard, touching the knife's
        // own (much larger, easier to hit) collider produced a fake "sampled" tip -
        // hasSample true, red visual - that could never actually satisfy swabbingDone,
        // since only MarkSwabbed does that, not MarkCollected. The player would then get
        // refused at the container with "must be swabbed first" immediately after the game
        // told them, visually, that they just had.
        //
        // Deliberately a silent refusal (Debug.Log only, no NotificationManager toast):
        // the marker's own collider now covers most of the item's reachable surface (see
        // EvidenceSwabRequirement's own comment on hit-area sizing), so this branch should
        // rarely fire in normal play - a player still lining up their aim near the item
        // shouldn't get an interrupting popup for every frame they're not yet exactly on
        // the marker.
        var record = EvidenceStateManager.Instance?.GetRecord(evidence.evidenceId);
        if (record?.definition != null && record.definition.requiresSwabbing)
        {
            Debug.Log($"[SwabTool] Can't take a generic sample from {evidence.evidenceId}: its trace must be swabbed at its own marker, not the item body.");
            return false;
        }

        if (EvidenceStateManager.Instance == null
            || !EvidenceStateManager.Instance.IsAtOrPastStatus(evidence.evidenceId, EvidenceStatus.ReadyForCollection))
        {
            string reason = GetNotReadyReason(evidence.evidenceId);
            Debug.Log($"[SwabTool] Can't swab {evidence.evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return false;
        }

        if (!CleanGlovesForSampling() || !FreshSwabForSampling()) return false;
        hasSample = true;
        SampledFrom = evidence;
        ConsumeFreshSwab(evidence.evidenceId);
        ApplySampledVisual();
        PlaySwabbingSound();
        string sampleName = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetDisplayName(evidence.evidenceId) : evidence.evidenceId;
        GameDialogue.Say("swab-sample-taken", sampleName);
        ArrivalPPEStation.Instance?.MarkUsedForSample(evidence.evidenceId);
        return true;
    }

    /// <summary>
    /// The real, gated required-swab sampling action - the marker-specific mirror of
    /// TrySwab. Refuses via the same "not yet ready" check, then swaps the visual and
    /// records which marker the sample came from (sampledRequirement), exactly the way
    /// TrySwab records SampledFrom. Deliberately does NOT report anything to
    /// EvidenceStateManager yet - that happens at TryContain, once the sample actually
    /// reaches the container, so this trace goes through the identical
    /// sample-then-contain gesture as a real EvidenceProp swab rather than completing
    /// on first touch. Public so GreyboxFlowTest can exercise this exact logic directly.
    /// </summary>
    public bool TrySampleRequiredSwab(EvidenceSwabRequirement requirement)
    {
        if (requirement == null || string.IsNullOrEmpty(requirement.evidenceId) || hasSample || awaitingSeal)
        {
            return false;
        }

        // A procedure rule on sampling (e.g. overall scene photography first): refused
        // before the swab touches anything. One blocked action logged.
        string samplingBlocked = EvidenceStateManager.Instance?.GetActionBlockReason(requirement.evidenceId, ProcedureAction.BiologicalSampling);
        if (samplingBlocked != null)
        {
            Debug.Log($"[SwabTool] Can't swab required trace for {requirement.evidenceId}: {samplingBlocked}");
            NotificationManager.Notify(samplingBlocked, DialogueKind.Error);
            InteractionFeedback.Blocked();
            EvidenceStateManager.Instance.ReportProcedureBlocked(requirement.evidenceId, EvidenceStatus.Logged);
            return false;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(requirement.evidenceId);
        if (record == null || record.definition == null || !record.definition.requiresSwabbing || record.swabbingDone
            || record.status != EvidenceStatus.PhotographedFinal)
        {
            string reason = GetNotReadyReason(requirement.evidenceId);
            Debug.Log($"[SwabTool] Can't swab required trace for {requirement.evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return false;
        }

        if (!CleanGlovesForSampling() || !FreshSwabForSampling()) return false;
        hasSample = true;
        sampledRequirement = requirement;
        ConsumeFreshSwab(requirement.evidenceId);
        ApplySampledVisual();
        PlaySwabbingSound();
        string traceName = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetDisplayName(requirement.evidenceId) : requirement.evidenceId;
        GameDialogue.Say("swab-trace-sampled", traceName);
        ArrivalPPEStation.Instance?.MarkUsedForSample(requirement.evidenceId);
        return true;
    }

    /// <summary>
    /// The real, gated collection action: deposits the sample in the container.
    /// Branches on which of SampledFrom (a real EvidenceProp, e.g. EVD-015) or
    /// sampledRequirement (a required-swab marker, e.g. the knife's blood trace) the
    /// current sample actually came from - exactly one is ever set at a time - and
    /// reports MarkCollected or MarkSwabbed accordingly, never a hardcoded id or
    /// transition. Public for the same reason as TrySwab.
    /// </summary>
    public TransitionResult TryContain()
    {
        if (!hasSample)
        {
            return TransitionResult.Violation;
        }

        if (sampledRequirement != null)
        {
            string requirementId = sampledRequirement.evidenceId;
            EnsureCameraRig();
            if (completedContainerPrefab == null || cameraRig?.rightHandAnchor == null || HeldItemCoordinator.Instance == null
                || EvidenceInventory.Instance == null || !EvidenceInventory.Instance.CanAcceptSample(requirementId)
                || PlayerUIGate.IsBlocked || ToolInputLock.IsLockedFor(ToolRole)) return TransitionResult.Violation;
            var result = EvidenceStateManager.Instance.MarkSwabbed(requirementId, ToolRole);
            if (result == TransitionResult.Violation)
            {
                string reason = GetNotReadyReason(requirementId);
                Debug.Log($"[SwabTool] Can't swab required trace for {requirementId}: {reason}");
                NotificationManager.Notify(reason, DialogueKind.Error);
                return TransitionResult.Violation;
            }

            // Unlike SampledFrom's own branch just below (MarkCollected, which fires
            // OnEvidenceStatusChanged and so already gets a generic "{id} collected"
            // toast from EvidenceNotifier for free), MarkSwabbed fires
            // OnEvidenceSwabbed instead - swabbingDone is a flag, not a
            // RequiredSequence status - and EvidenceNotifier doesn't listen to that
            // event. Without this, successfully containing a required-swab sample
            // (the knife/gloves blood trace) was completely silent on success - no
            // toast at all, unlike every other real action this tool takes. Explicit
            // here for that reason, not because the pattern differs from TrySwab's
            // own success notification.
            string collectName = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetDisplayName(requirementId) : requirementId;
            GameDialogue.Say("swab-trace-collected", collectName);

            hasSample = false;
            awaitingSeal = true;
            Preparation = PreparationStage.NeedsDrying; DryingProgress = 0;
            heldSampleId = HeldItemCoordinator.Instance.HoldContainedSample(requirementId, completedContainerPrefab, cameraRig.rightHandAnchor);
            if (heldSampleId == null) return TransitionResult.Violation;
            OnSampleContained?.Invoke(requirementId);
            return TransitionResult.Applied;
        }

        if (SampledFrom == null)
        {
            return TransitionResult.Violation;
        }

        string evidenceId = SampledFrom.evidenceId;

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanCollect(evidenceId))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidenceId);
            Debug.Log($"[SwabTool] Can't collect {evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return TransitionResult.Violation;
        }

        ReportEvidence(evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkCollected(id, tool), "collected");

        hasSample = false;
        awaitingSeal = true;
        Preparation = PreparationStage.NeedsDrying; DryingProgress = 0;
        return TransitionResult.Applied;
    }

    /// <summary>
    /// The real, gated sealing action: seals the individually packaged, dried sample.
    /// A new swab still requires supply pickup. Branches the
    /// same way TryContain does: a required-swab sample has nothing left to report
    /// (MarkSwabbed at TryContain already fully satisfied the precondition) and just
    /// resets state; a real EvidenceProp sample still reports MarkSealed against
    /// SampledFrom. Public for the same reason as TrySwab.
    /// </summary>
    public TransitionResult TrySeal()
    {
        if (!awaitingSeal)
        {
            return TransitionResult.Violation;
        }

        if (NeedsPreparation)
        {
            GameDialogue.Say("sample-prepare-first");
            InteractionFeedback.Blocked();
            return TransitionResult.Violation;
        }
        if (PlayerUIGate.IsBlocked || ToolInputLock.HasUIOwners) return TransitionResult.Violation;

        if (sampledRequirement != null)
        {
            // Records the sealed sample (matters for a collectedBySwab item, whose record
            // can't be completed until its sample is sealed - EvidenceStateManager.
            // IsBlockedBySwabbing). Harmless for the knife's trace.
            if (EvidenceStateManager.Instance == null || EvidenceStateManager.Instance.MarkSwabSampleSealed(sampledRequirement.evidenceId, ToolRole) == TransitionResult.Violation)
                return TransitionResult.Violation;
            Debug.Log($"[SwabTool] Sealed swab container after collecting the required trace for {sampledRequirement.evidenceId}.");
            awaitingSeal = false;
            sampledRequirement = null;
            heldSampleId = null;
            HideSpentTip();
            PlayContainerCloseSound();
            return TransitionResult.Applied;
        }

        if (SampledFrom == null)
        {
            return TransitionResult.Violation;
        }

        string evidenceId = SampledFrom.evidenceId;

        if (ProceduralGateValidator.Instance != null
            && !ProceduralGateValidator.Instance.CanTransition(evidenceId, EvidenceStatus.Sealed))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidenceId, EvidenceStatus.Sealed);
            Debug.Log($"[SwabTool] Can't seal {evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return TransitionResult.Violation;
        }

        ReportEvidence(evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkSealed(id, tool), "sealed");

        // Sealing itself is now complete (MarkSealed above already applied). This item
        // (e.g. EVD-015, a swab sample) must enter the evidence inventory exactly like
        // a bagged item does - it must not remain an abstract status-only record. Same
        // partial-success handling as EvidenceBagTool.TrySeal: awaitingSeal/SampledFrom
        // are deliberately NOT cleared on failure, so the container keeps holding this
        // sample (still visibly sampled, still recoverable) rather than silently
        // resetting while admission never actually completed.
        if (EvidenceInventory.Instance == null || !EvidenceInventory.Instance.Add(evidenceId))
        {
            Debug.LogError($"[SwabTool] {evidenceId} was sealed but could not be admitted to the evidence inventory (no EvidenceInventory in the scene, or admission was refused - see the warning above). The sample remains in the container rather than disappearing.");
            GameDialogue.Say("swab-seal-failed");
            return TransitionResult.Violation;
        }

        // Same "packing evidence puts it in your hand" behaviour as
        // EvidenceBagTool.TrySeal - see that method's own comment for why this must go
        // through HeldItemCoordinator rather than EvidenceInventory.EquipHeld directly,
        // and why holstering this tool itself (SwabTool occupies the right hand) is the
        // accepted tradeoff.
        EnsureCameraRig();
        var rightHandAnchor = cameraRig != null ? cameraRig.rightHandAnchor : null;
        if (rightHandAnchor != null && HeldItemCoordinator.Instance != null)
        {
            HeldItemCoordinator.Instance.EquipEvidence(evidenceId, rightHandAnchor);
            if (Preparation == PreparationStage.Packaged && EvidenceInventory.Instance.HeldVisual != null)
                PaperSwabPackage.Apply(EvidenceInventory.Instance.HeldVisual.transform, evidenceId);
        }

        awaitingSeal = false;
        SampledFrom = null;
        HideSpentTip();
        PlayContainerCloseSound();
        return TransitionResult.Applied;
    }

    private void HideSpentTip() { if(tipRenderer!=null)tipRenderer.enabled=false; }
    private bool FreshSwabForSampling()
    {
        if(HasFreshSwab)return true;
        RequestFreshSupply();
        GameDialogue.Say("swab-supply-required");InteractionFeedback.Blocked();return false;
    }
    private void ConsumeFreshSwab(string source)
    {
        HasFreshSwab=false;SupplyRequested=true;lastSampleSourceId=source;
        SessionLogger.Instance?.LogEvent(SessionEventType.FreshSwabUsed,source);
    }
    private bool CleanGlovesForSampling()
    {
        if (ArrivalPPEStation.Instance != null && ArrivalPPEStation.Instance.HasCleanGloves) return true;
        // Ungated diagnostic scenes without the arrival workflow retain their original tools.
        if (!SceneTurnover.IsGated && ArrivalPPEStation.Instance == null) return true;
        GameDialogue.Say("gloves-sampling-blocked"); InteractionFeedback.Blocked(); return false;
    }

    private void PlaySwabbingSound()
    {
        if (audioSource != null && swabbingClip != null)
        {
            audioSource.PlayOneShot(swabbingClip);
        }
    }

    private void PlayContainerCloseSound()
    {
        if(Preparation==PreparationStage.Packaged && InteractionSfx.Instance!=null)
        {
            var held=EvidenceInventory.Instance!=null ? EvidenceInventory.Instance.HeldVisual : null;
            InteractionSfx.Play(InteractionSound.Tape,held!=null ? held.transform.position : containerVisual!=null ? containerVisual.position : transform.position);
            return;
        }
        if (audioSource != null && containerCloseClip != null)
        {
            audioSource.PlayOneShot(containerCloseClip);
        }
    }

    private static string GetNotReadyReason(string evidenceId)
    {
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record == null)
        {
            return GameDialogue.Text("sys-unknown-evidence");
        }

        return EvidenceStateManager.DescribeNextStepForPlayer(record);
    }

    private void ApplyCleanVisual()
    {
        if (tipMeshFilter != null && cleanTipMesh != null)
        {
            tipMeshFilter.sharedMesh = cleanTipMesh;
        }

        if (tipRenderer != null)
        {
            // Real material takes priority; the generated placeholder colour is only
            // ever reached when no real art is wired (cleanTipMaterial left blank) -
            // see the class comment on GetCleanMaterial/GetSampledMaterial.
            tipRenderer.sharedMaterial = cleanTipMaterial != null ? cleanTipMaterial : GetCleanMaterial();
        }
    }

    private void ApplySampledVisual()
    {
        if (tipMeshFilter != null && sampledTipMesh != null)
        {
            // The whole point of this swap: once a sample is taken, the swab's model
            // itself changes to the blood-stained one (SWAB_BLOOD) - the blood visibly
            // appears ON the swab, not just a tint over the clean model's shape.
            tipMeshFilter.sharedMesh = sampledTipMesh;
        }

        if (tipRenderer != null)
        {
            tipRenderer.sharedMaterial = sampledTipMaterial != null ? sampledTipMaterial : GetSampledMaterial();
        }
    }

    private static Material GetCleanMaterial()
    {
        if (cleanMaterial == null)
        {
            cleanMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.94f, 0.91f, 0.85f)
            };
        }
        return cleanMaterial;
    }

    // Generic placeholder colour, generated rather than shipped - nothing here
    // depends on an art asset existing.
    private static Material GetSampledMaterial()
    {
        if (sampledMaterial == null)
        {
            sampledMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.03f, 0.03f)
            };
        }
        return sampledMaterial;
    }
}
