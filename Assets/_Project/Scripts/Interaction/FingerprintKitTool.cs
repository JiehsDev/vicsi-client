// Assets/_Project/Scripts/Interaction/FingerprintKitTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// On-scene latent-print powder/brush processing, paired with FingerprintTapeTool.
/// Dusting progress is owned by the touched EvidenceFingerprintMarker. Lifting
/// transfers the print to a derived, source-linked inventory card and holsters the
/// tape tool. Explicit B/storage completes the parent fingerprint procedure and
/// identification. The source knife remains in the scene for its remaining steps.
/// </summary>
public class FingerprintKitTool : PlayerTool
{
    public static FingerprintKitTool Instance { get; private set; }

    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Brush tip (right hand)")]
    [Tooltip("Trigger collider on a dedicated child positioned at the visible bristle tip - NOT the whole brush body/model's own bounding collider (that was the confirmed root cause of dust registering from far away: the previous reference pointed at the brush's full 1m-cube default BoxCollider). Detects both the jar's receiving zone and print markers' touch zones. PlayerTool.SetEquippedVisualState disables every collider under this GameObject in both equip states, so this has to be explicitly re-enabled/disabled in step with EquipToHand/Holster, same as SwabTool.tipCollider.")]
    [SerializeField] private Collider brushTipCollider;
    [Tooltip("Renderer whose mesh/material swap between clean and dust-loaded. Its MeshFilter is resolved from this same GameObject in Awake, not a separate serialized field, since the two are always on the brush together.")]
    [SerializeField] private Renderer brushRenderer;

    [Header("Brush - real models (optional)")]
    [Tooltip("The real clean brush model (POWDER_BRUSH_30K) and its material. Leave blank to keep whatever mesh is already on brushRenderer with only a generated colour swap.")]
    [SerializeField] private Mesh cleanBrushMesh;
    [SerializeField] private Material cleanBrushMaterial;
    [Tooltip("Optional real dust-loaded brush model/material. No distinct 'loaded' mesh exists in the art pack today, so this is normally left blank and the loaded state falls back to a generated colour tint on the same mesh - see class comment on not forcing a visual the assets don't support.")]
    [SerializeField] private Mesh loadedBrushMesh;
    [SerializeField] private Material loadedBrushMaterial;

    [Header("Jar (left hand)")]
    [Tooltip("Separate object for the dust jar - not a PlayerTool itself. Reparented to the left hand alongside this tool's own right-hand equip, and back to its authored position on holster. Mirrors SwabTool.containerVisual exactly.")]
    [SerializeField] private Transform jarVisual;
    [Tooltip("The jar's own trigger collider - detects the brush tip entering. Mirrors SwabTool.containerReceivingZone.")]
    [SerializeField] private Collider jarReceivingZone;
    [SerializeField] private Vector3 jarEquipLocalPosition;
    [SerializeField] private Vector3 jarEquipLocalEulerAngles;

    private static Material dustedMaterial;
    private static Material cleanBrushFallbackMaterial;
    private static Material loadedBrushFallbackMaterial;

    // Whether the brush currently has dust on it - set only by TryLoadDust (real
    // jar contact + trigger), never auto-consumed. A fact about the physical brush,
    // not about any one print - see class comment.
    private bool dustLoaded;

    // Whether the brush's trigger collider currently overlaps the jar's receiving
    // zone - tracked by HandleBrushTriggerEnter/Exit, read once per frame in Update,
    // the exact same shape as SwabTool.touchingContainer.
    private bool touchingJar;

    // Whichever print marker the brush tip is CURRENTLY overlapping, or null - tracked
    // by HandleBrushTriggerEnter/Exit the same way touchingJar is. Contact alone no
    // longer applies dust (see TryDust/Update) - this only records where the tip
    // physically is right now, read every held frame to decide whether to accumulate
    // progress.
    private EvidenceFingerprintMarker touchingMarker;

    private MeshFilter brushMeshFilter;
    private Transform jarHomeParent;
    private Vector3 jarHomeLocalPosition;
    private Quaternion jarHomeLocalRotation;
    private OVRCameraRig cameraRig;

    public override ToolType ToolRole => ToolType.FingerprintKit;

    /// <summary>Exposed for GreyboxFlowTest reporting only - not used by any gameplay logic.</summary>
    public bool DustLoaded => dustLoaded;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;

        if (brushRenderer != null)
        {
            brushMeshFilter = brushRenderer.GetComponent<MeshFilter>();
        }

        if (jarVisual != null)
        {
            jarHomeParent = jarVisual.parent;
            jarHomeLocalPosition = jarVisual.localPosition;
            jarHomeLocalRotation = jarVisual.localRotation;
            SetJarVisible(false);
        }

        ApplyCleanBrushVisual();
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

    /// <summary>Attaches the brush to whichever hand the wheel offers (right, per PlayerTool's default), and the jar to the left hand in the same gesture - one equip, both hands. Mirrors SwabTool.EquipToHand exactly.</summary>
    public override void EquipToHand(Transform handAnchor)
    {
        base.EquipToHand(handAnchor);

        if (brushTipCollider != null)
        {
            brushTipCollider.enabled = true;
        }

        EnsureCameraRig();
        if (jarVisual != null && cameraRig != null && cameraRig.leftHandAnchor != null)
        {
            jarVisual.SetParent(cameraRig.leftHandAnchor, false);
            jarVisual.localPosition = jarEquipLocalPosition;
            jarVisual.localRotation = Quaternion.Euler(jarEquipLocalEulerAngles);
            SetJarVisible(true);
        }
    }

    public override void Holster()
    {
        if (brushTipCollider != null)
        {
            brushTipCollider.enabled = false;
        }

        if (jarVisual != null)
        {
            jarVisual.SetParent(jarHomeParent, false);
            jarVisual.localPosition = jarHomeLocalPosition;
            jarVisual.localRotation = jarHomeLocalRotation;
            SetJarVisible(false);
        }

        base.Holster();
    }

    private void SetJarVisible(bool visible)
    {
        if (jarVisual == null)
        {
            return;
        }

        foreach (var renderer in jarVisual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }

        if (jarReceivingZone != null)
        {
            jarReceivingZone.enabled = visible;
        }
    }

    /// <summary>
    /// Called by BrushTipReceiver whenever the brush tip's trigger overlap begins.
    /// Two entirely different real contacts share this one entry point, exactly as
    /// SwabTool.HandleTipTriggerEnter distinguishes its own container zone from a
    /// swabbable item: the jar (tracked as a persistent touchingJar flag, since
    /// loading dust additionally needs a deliberate trigger press - see Update) and
    /// any print marker's own touch-zone collider (resolved fresh via
    /// GetComponentInParent&lt;EvidenceFingerprintMarker&gt; - not a single hardcoded
    /// id - and fires TryDust(marker) immediately on contact, no press needed - see
    /// class comment on why this one gesture differs from loading).
    /// </summary>
    public void HandleBrushTriggerEnter(Collider other)
    {
        if (other == jarReceivingZone)
        {
            touchingJar = true;
            return;
        }

        var marker = other.GetComponentInParent<EvidenceFingerprintMarker>();
        if (marker != null)
        {
            touchingMarker = marker;
        }
    }

    public void HandleBrushTriggerExit(Collider other)
    {
        if (other == jarReceivingZone)
        {
            touchingJar = false;
            return;
        }

        if (touchingMarker != null && other.GetComponentInParent<EvidenceFingerprintMarker>() == touchingMarker)
        {
            touchingMarker = null;
        }
    }

    private void Update()
    {
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        if (!IsHeld || rightActivateAction == null)
        {
            return;
        }

        if (touchingJar && rightActivateAction.action.WasPressedThisFrame())
        {
            TryLoadDust();
        }

        // Deliberate, continuous dusting: the tip must actually be inside the
        // target's own touch zone AND the trigger held down, every frame progress
        // accumulates - see TryDust/EvidenceFingerprintMarker.AddDustProgress. Using
        // IsPressed() (held), not WasPressedThisFrame() (one-shot) - a single frame
        // of accidental contact can no longer complete a reveal, and releasing the
        // trigger simply stops accumulating rather than undoing anything.
        if (touchingMarker != null && rightActivateAction.action.IsPressed())
        {
            TryDust(touchingMarker);
        }
        else if (!rightActivateAction.action.IsPressed())
        {
            blockReportedThisPress = false;
        }
    }

    /// <summary>
    /// Loads dust onto the brush from real jar contact + trigger press. Persists once
    /// loaded - not consumed per dust-attempt (see class comment). Public so
    /// GreyboxFlowTest can exercise this exact logic directly.
    /// </summary>
    public bool TryLoadDust()
    {
        dustLoaded = true;
        ApplyLoadedBrushVisual();
        NotificationManager.Notify(GameDialogue.Text("fingerprint-brush-loaded"), DialogueKind.Confirmation);
        return true;
    }

    /// <summary>
    /// The real, gated dusting action: refuses via the same procedural gate every
    /// other tool uses (checked against marker.evidenceId's own record, never the
    /// print's own - it has none). Not a state transition - see class comment.
    /// Requires dustLoaded (a real brush-jar touch must have happened first), the
    /// item to be at least PhotographedFinal, and - for an item with
    /// EvidenceDefinition.requiresFlipBeforeFingerprinting (the gloves) - to already
    /// be flipped, mirroring MarkFingerprinted's own extra precondition for the exact
    /// same reason: a surface not yet reachable can't be dusted either.
    ///
    /// Deliberately called every frame the tip is genuinely inside the marker's touch
    /// zone AND the trigger is held (see Update) - a single call therefore only ever
    /// adds one frame's worth of progress (EvidenceFingerprintMarker.AddDustProgress),
    /// never an instant reveal. A brief, accidental one-frame overlap can no longer
    /// complete a print; sustained, deliberate contact is required. On the one call
    /// that actually crosses the threshold, permanently locks the print visible
    /// regardless of the UV beam (see UVRevealable.LockVisible) and posts the "ready
    /// to lift" toast - both fire exactly once, gated on marker.IsDusted's own
    /// false-to-true transition, not on every call. Public so GreyboxFlowTest (and a
    /// direct test driving several calls to simulate held frames) can exercise this
    /// exact logic directly.
    /// </summary>
    // See TryDust: the sample-first refusal is reported once per trigger press.
    private bool blockReportedThisPress;

    public bool TryDust(EvidenceFingerprintMarker marker)
    {
        if (marker == null || marker.IsDusted || EvidenceStateManager.Instance == null)
        {
            return false;
        }

        if (!dustLoaded)
        {
            Debug.Log($"[FingerprintKitTool] Can't dust the print on {marker.evidenceId}: brush has no dust loaded.");
            NotificationManager.Notify(GameDialogue.Text("fingerprint-load-powder-first"), DialogueKind.Error);
            return false;
        }

        // Procedure rules on fingerprint processing itself (stepRequirements, e.g. overall
        // scene photography first) come before the item's own readiness: they name the
        // root cause, and the refusal is logged once per press.
        string procedureFirst = EvidenceStateManager.Instance.GetActionBlockReason(marker.evidenceId, ProcedureAction.FingerprintProcessing);
        if (procedureFirst != null)
        {
            if (!blockReportedThisPress)
            {
                blockReportedThisPress = true;
                Debug.Log($"[FingerprintKitTool] Can't dust the print on {marker.evidenceId}: {procedureFirst}");
                NotificationManager.Notify(procedureFirst, DialogueKind.Error);
                InteractionFeedback.Blocked();
                EvidenceStateManager.Instance.ReportFingerprintBlocked(marker.evidenceId);
            }
            return false;
        }

        var record = EvidenceStateManager.Instance.GetRecord(marker.evidenceId);
        if (record == null || !EvidenceStateManager.Instance.IsAtOrPastStatus(marker.evidenceId, EvidenceStatus.PhotographedFinal))
        {
            string reason = record != null ? GetNotReadyReason(record) : GameDialogue.Text("sys-unknown-evidence");
            Debug.Log($"[FingerprintKitTool] Can't dust the print on {marker.evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return false;
        }

        // Biological sampling first (EvidenceDefinition.swabBeforeFingerprinting): refused
        // before any powder lands, so the print is never revealed early. Reported once per
        // trigger press - this runs every frame the brush is held on the print.
        string sampleFirst = EvidenceStateManager.Instance.GetFingerprintBlockReason(marker.evidenceId);
        if (sampleFirst != null)
        {
            if (!blockReportedThisPress)
            {
                blockReportedThisPress = true;
                Debug.Log($"[FingerprintKitTool] Can't dust the print on {marker.evidenceId}: {sampleFirst}");
                NotificationManager.Notify(sampleFirst, DialogueKind.Error);
                InteractionFeedback.Blocked();
                EvidenceStateManager.Instance.ReportFingerprintBlocked(marker.evidenceId);
            }
            return false;
        }

        if (record.definition != null && record.definition.requiresFlipBeforeFingerprinting && !record.isFlipped)
        {
            Debug.Log($"[FingerprintKitTool] Can't dust the print on {marker.evidenceId}: not flipped yet.");
            NotificationManager.Notify(GameDialogue.Text("fingerprint-examine-other-surface-first"), DialogueKind.Error);
            return false;
        }

        marker.AddDustProgress(Time.deltaTime);

        if (marker.IsDusted)
        {
            ApplyDustedVisual(marker);

            // A real dusted print stays visible once revealed by powder - it no longer
            // depends on the UV beam still being pointed at it. See UVRevealable.LockVisible.
            marker.GetComponent<UVRevealable>()?.LockVisible();

            // Confirms the contact actually registered - without this, dusting looked
            // identical to a whiff (brush passing near the print with no effect), and a
            // player had nothing but the marker's own material swap to notice by.
            string dustName = EvidenceStateManager.Instance.GetDisplayName(marker.evidenceId);
            GameDialogue.Say("fingerprint-dusted-ready-to-lift", dustName);
        }

        return true;
    }

    /// <summary>Transfer the dusted print into a source-linked card in the existing
    /// held-evidence inventory. The explicit B action completes fingerprint collection
    /// and identification; lifting alone does not complete recording or RC-03.</summary>
    public TransitionResult TryLift(EvidenceFingerprintMarker marker)
    {
        if (marker == null || !marker.IsDusted)
        {
            // Every OTHER refusal in this class notifies - touching an undusted
            // print with the tape (a separately-equipped item a player can genuinely
            // reach for before ever dusting) should too.
            Debug.Log($"[FingerprintKitTool] Can't lift the print for {marker?.evidenceId}: not dusted yet.");
            NotificationManager.Notify(GameDialogue.Text("fingerprint-reveal-before-lift"), DialogueKind.Error);
            return TransitionResult.Violation;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(marker.evidenceId);
        if (record == null)
        {
            return TransitionResult.Violation;
        }

        if (record.status != EvidenceStatus.PhotographedFinal || record.fingerprintingDone || record.fingerprintLifted)
        {
            string reason = record.fingerprintingDone ? GameDialogue.Text("fingerprint-already-done") : GetNotReadyReason(record);
            Debug.Log($"[FingerprintKitTool] Can't lift the print for {marker.evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return TransitionResult.Violation;
        }

        var state = EvidenceStateManager.Instance;
        if (!state.CanLiftFingerprint(marker.evidenceId) || PlayerUIGate.IsBlocked || ToolInputLock.IsLocked
            || FingerprintTapeTool.Instance == null || !FingerprintTapeTool.Instance.TryHoldLiftedPrint(marker.evidenceId))
        {
            GameDialogue.Say("fingerprint-lift-unavailable");
            return TransitionResult.Violation;
        }

        state.MarkFingerprintLifted(marker.evidenceId);
        InteractionSfx.Play(InteractionSound.Tape,marker.transform.position);
        marker.GetComponent<UVRevealable>()?.Remove();
        // The held-card prompt and waypoint lead to comparison before B storage.
        // Identification and contextual findings still require the correct reference.

        return TransitionResult.Applied;
    }

    private static string GetNotReadyReason(EvidenceRecord record)
    {
        return EvidenceStateManager.DescribeNextStepForPlayer(record);
    }

    private void ApplyDustedVisual(EvidenceFingerprintMarker marker)
    {
        if (marker.ApplyPowderedVisual()) return;
        var renderer = marker.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            renderer.sharedMaterial = GetDustedMaterial();
        }
    }

    // Generic placeholder colour, generated rather than shipped - nothing here
    // depends on an art asset existing.
    private static Material GetDustedMaterial()
    {
        if (dustedMaterial == null)
        {
            dustedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.88f, 0.86f, 0.8f)
            };
        }
        return dustedMaterial;
    }

    private void ApplyCleanBrushVisual()
    {
        if (brushMeshFilter != null && cleanBrushMesh != null)
        {
            brushMeshFilter.sharedMesh = cleanBrushMesh;
        }

        if (brushRenderer != null)
        {
            brushRenderer.sharedMaterial = cleanBrushMaterial != null ? cleanBrushMaterial : GetCleanBrushFallbackMaterial();
        }
    }

    private void ApplyLoadedBrushVisual()
    {
        if (brushMeshFilter != null && loadedBrushMesh != null)
        {
            brushMeshFilter.sharedMesh = loadedBrushMesh;
        }

        if (brushRenderer != null)
        {
            brushRenderer.sharedMaterial = loadedBrushMaterial != null ? loadedBrushMaterial : GetLoadedBrushFallbackMaterial();
        }
    }

    // Fallbacks used only when no real material is assigned for that state - same
    // "nothing here depends on an art asset existing" convention as SwabTool's own
    // GetCleanMaterial/GetSampledMaterial.
    private static Material GetCleanBrushFallbackMaterial()
    {
        if (cleanBrushFallbackMaterial == null)
        {
            cleanBrushFallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.3f, 0.2f, 0.1f)
            };
        }
        return cleanBrushFallbackMaterial;
    }

    private static Material GetLoadedBrushFallbackMaterial()
    {
        if (loadedBrushFallbackMaterial == null)
        {
            loadedBrushFallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.88f, 0.86f, 0.8f)
            };
        }
        return loadedBrushFallbackMaterial;
    }
}
