// Assets/_Project/Scripts/Interaction/PhotographTool.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction.Locomotion;

/// <summary>
/// Sits on a grabbable camera prop (built on GrabbableEvidenceBase). Holding
/// it isn't enough to shoot: the player must press the left controller's X
/// button to raise the camera to eye level first - this animates the
/// visual mesh into an aiming pose and shows the viewfinder HUD. While
/// aiming, every registered EvidenceProp is checked each frame for whether it is
/// genuinely visible in frame (see UpdateAimTarget / CanCapture / CurrentAimTarget
/// below) - other components (a viewfinder red/green dot) read that state via
/// events instead of duplicating the check. Pressing the assigned Activate input
/// only takes a photo - playing a flash/shutter cue and reporting whichever
/// photograph status is next for that item - while CanCapture is true; otherwise
/// the shutter is a no-op.
///
/// One camera, two outcomes: "photography is one job done twice," not two tools or
/// two actions. Aiming at an item that is currently Found produces PhotographedRaw
/// (the undisturbed scene); aiming at the same item once it is later Marked
/// produces PhotographedFinal (the documented scene, tent in frame). Which of the
/// two applies is decided purely by the item's current status via
/// ResolveNextPhotoStatus - never by which button, which mode, or anything else the
/// player picks. The camera's assisted ruler accessory uses A; selected items' final
/// photographs also require the item and the whole ruler within the saved frame.
///
/// IsConfirmedForCapture is a separate, narrower signal for feedback only
/// (see its own field comment) - it must never be substituted for CanCapture
/// in the shutter no-op check, and TakePhoto never reads it.
///
/// The Photographer capability itself - PlayerTool.ToolRole, registration,
/// held-state - lives in the PlayerTool base so every other tool (chalk, evidence
/// bag, recorder, ...) shares the exact same contract.
/// </summary>
public class PhotographTool : PlayerTool
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    [Header("Aiming (raise-to-eye)")]
    [Tooltip("The shutter only works while aiming. Press the left controller's X button to toggle raising the camera to eye level, like lifting it to look through the viewfinder.")]
    [SerializeField] private Transform visualPivot;
    [SerializeField] private Vector3 aimLocalPositionOffset = new Vector3(0f, 0.05f, 0.08f);
    [SerializeField] private Vector3 aimLocalEulerOffset = new Vector3(-15f, 0f, 0f);
    [SerializeField] private float aimTransitionSeconds = 0.25f;
    [SerializeField] private GameObject viewfinderOverlay;

    [Header("Zoom (POV field of view while aiming)")]
    [Tooltip("Camera whose field of view narrows while aiming, to sell the sense of looking through an optical viewfinder. Defaults to Camera.main (the player's HMD eye camera) if left unassigned.")]
    [SerializeField] private Camera povCamera;
    [SerializeField] private float zoomedFieldOfView = 45f;

    [Header("Movement (slow walk while aiming)")]
    [Tooltip("Player's locomotor, found automatically in the scene if left unassigned. Movement speed is multiplied by aimMoveSpeedMultiplier while aiming, so the player creeps instead of walking normally while focused on a shot.")]
    [SerializeField] private FirstPersonLocomotor locomotor;
    [SerializeField, Range(0.05f, 1f)] private float aimMoveSpeedMultiplier = 0.35f;

    [Header("Feedback")]
    [SerializeField] private Light flashLight;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private AudioSource shutterAudioSource;
    [SerializeField] private AudioClip shutterClip;

    public override ToolType ToolRole => ToolType.Photographer;

    /// <summary>The viewfinder canvas (aim reticle, 4:3 crop mask, HUD text) - exposed so a photo-capture listener can hide it for the instant it renders a shot, so the crop mask's black bars and HUD chrome never get baked into the saved photo.</summary>
    public GameObject ViewfinderOverlay => viewfinderOverlay;
    public bool IsAiming => isAiming;
    public bool IsCameraHeld => IsHeld;
    public Camera CapturePov => povCamera != null ? povCamera : Camera.main;
    public float CaptureAspect => viewfinderOverlay != null
        ? (viewfinderOverlay.GetComponentInChildren<ViewfinderFrameMask>(true)?.Aspect ?? 4f/3f) : 4f/3f;
    public PhotographicScaleAccessory PhotoScale { get; private set; }
    /// <summary>Set before the synchronous capture listener renders, reset at each shutter press.</summary>
    public bool LastCaptureUsedScale { get; private set; }
    public string LastCaptureSubjectId { get; private set; }
    public string LastCaptureSubjectName { get; private set; }
    public PhotoCaptureKind LastCaptureKind { get; private set; }

    /// <summary>True while aiming and the forward raycast is currently hitting a valid EvidenceProp within maxDistance. False (and the shutter is a no-op) at every other time, including while not aiming.</summary>
    public bool CanCapture { get; private set; }

    /// <summary>The procedure warning shown for the most recent shutter press, or null if none (reset on every press).</summary>
    public string LastProcedureWarning { get; private set; }

    /// <summary>The EvidenceProp currently in frame, or null. Only ever non-null while aiming and CanCapture is true.</summary>
    public EvidenceProp CurrentAimTarget { get; private set; }

    /// <summary>Fired whenever CanCapture changes, including transitions caused by starting/stopping aiming.</summary>
    public event Action<bool> OnAimValidityChanged;

    /// <summary>Fired whenever CurrentAimTarget changes (including to/from null).</summary>
    public event Action<EvidenceProp> OnAimTargetChanged;

    /// <summary>
    /// True only while a shutter press right now would actually apply one of the two
    /// photo statuses (CurrentAimTarget's status is exactly Found or exactly Marked -
    /// see ResolveNextPhotoStatus), not merely "wouldn't error." Deliberately narrower
    /// than CanCapture, which only means "a real EvidenceProp is in frame" regardless
    /// of its status - CanCapture stays exactly as it was (still drives the shutter
    /// no-op check in TakePhoto) so this is a pure additive read of existing gate
    /// state, never consulted by TakePhoto itself, which re-resolves the target status
    /// independently at the moment of the shutter press. Purely a feedback signal for
    /// "is this the moment to act."
    /// </summary>
    public bool IsConfirmedForCapture { get; private set; }

    /// <summary>Fired whenever IsConfirmedForCapture changes.</summary>
    public event Action<bool> OnConfirmedForCaptureChanged;

    /// <summary>Fired right after a photo is successfully taken (evidence was in frame) - drives shutter VFX/SFX that don't want to duplicate the flash/audio already in PlayShutterFeedback.</summary>
    public event Action OnPhotoCaptured;

    /// <summary>
    /// Fired on every shutter press while aiming, after the evidence-photo logic has run,
    /// whether or not evidence was captured. evidenceCaptured is true when that press
    /// already produced an evidence photo (and its flash/sound). Lets independent
    /// systems (ScenePhotographyManager) react to the same shutter without duplicating
    /// capture code.
    /// </summary>
    public event Action<PhotographTool, bool> OnShutterReleased;

    private InputAction leftAimAction;
    private bool isAiming;

    /// <summary>Frame the viewfinder last closed. Left X both closes the camera and opens the utility menu; whichever Update runs second would otherwise see the same press with the gate already free.</summary>
    public static int AimClosedFrame { get; private set; } = -1;
    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private Coroutine aimRoutine;
    private Coroutine fovRoutine;
    private Renderer[] cameraRenderers;
    private float restFieldOfView;
    private bool povCameraReady;
    private float restSpeedFactor;
    private float restRunningSpeedFactor;
    private bool locomotorSpeedCached;

    protected override void Awake()
    {
        base.Awake();
        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }

        if (visualPivot == null)
        {
            visualPivot = transform.Find("Visual");
        }
        if (visualPivot != null)
        {
            restLocalPosition = visualPivot.localPosition;
            restLocalRotation = visualPivot.localRotation;
            cameraRenderers = visualPivot.GetComponentsInChildren<Renderer>(true);
        }

        // Not asset-backed (no "A/X button" action exists in the shared XRI Default
        // Input Actions asset) - built directly against the generic XR controller
        // primary-button control. Left hand (X) raises the camera; right A belongs
        // to the camera's assisted photographic scale accessory.
        leftAimAction = new InputAction("PhotographTool_LeftAim", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        PhotoScale = GetComponent<PhotographicScaleAccessory>();
        if (PhotoScale == null) PhotoScale = gameObject.AddComponent<PhotographicScaleAccessory>();

        if (viewfinderOverlay != null)
        {
            viewfinderOverlay.SetActive(false);
        }

        if (locomotor == null)
        {
            locomotor = FindFirstObjectByType<FirstPersonLocomotor>();
        }
    }

    // povCamera resolves lazily instead of in Awake because Camera.main (the OVR
    // CenterEyeAnchor) may not have its "MainCamera" tag active yet that early.
    private void EnsurePovCamera()
    {
        if (povCameraReady)
        {
            return;
        }

        if (povCamera == null)
        {
            povCamera = Camera.main;
        }
        if (povCamera != null)
        {
            restFieldOfView = povCamera.fieldOfView;
            povCameraReady = true;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        leftActivateAction?.action.Enable();
        rightActivateAction?.action.Enable();
        leftAimAction.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        leftActivateAction?.action.Disable();
        rightActivateAction?.action.Disable();
        leftAimAction.Disable();
        PlayerUIGate.Exit(this);
        PhotoScale?.Remove("camera-disabled");
    }

    private void OnDestroy()
    {
        leftAimAction?.Dispose();
    }

    private void Update()
    {
        PhotoScale?.Refresh();
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        bool held = IsHeld;

        if (!held)
        {
            if (isAiming)
            {
                SetAiming(false);
            }
            return;
        }

        if (leftAimAction.WasPressedThisFrame())
        {
            bool turningOn = !isAiming;
            // Turning off is always allowed (it's this tool's own gate entry
            // to release); turning on defers to whatever else - the utility
            // menu, the tool wheel - might already have the same X/Y button
            // held down for its own screen this frame.
            if (!turningOn || !PlayerUIGate.IsBlocked)
            {
                SetAiming(turningOn);
            }
        }

        if (!isAiming)
        {
            return;
        }

        UpdateAimTarget();
        PhotoScale?.Refresh();
        if (PhotoScale != null && PhotoScale.HandleInput()) return;

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            TakePhoto();
        }
    }

    // Runs every frame while aiming so a viewfinder indicator / target outline
    // can react in real time instead of only learning the result when the
    // shutter is pressed.
    //
    // Loosened per the documentation-flow redesign: a photo's validity now asks
    // "is the real evidence object visibly in frame" (on-screen from the player's
    // POV, within range, and not hidden behind something solid) rather than
    // requiring a raycast to land exactly on the item's own collider - a real
    // photograph is framed, not aimed like a laser pointer. The sequence gate is
    // untouched by this: which of the two photo statuses a shutter press would
    // apply still depends strictly on the item's current status (see
    // ResolveNextPhotoStatus), never on how loosely it was framed.
    //
    // AMBIGUOUS CALLS MADE HERE, FLAGGED RATHER THAN ASSUMED SETTLED:
    //   - "In frame" is tested as the item's pivot position falling inside the
    //     camera's view frustum (Camera.WorldToViewportPoint in 0-1 on x/y, in
    //     front of the camera on z) - not that any particular fraction of its mesh
    //     is on-screen. A large item clipped at the frame's edge with only its
    //     pivot inside would count; a large item with its pivot just outside the
    //     frame but bulk still visible would not.
    //   - "Not hidden behind something solid" is one raycast per candidate toward
    //     its pivot, requiring the FIRST thing hit to be that same item - i.e. any
    //     occluder (a wall, another prop, even a second piece of evidence in front
    //     of it) disqualifies it outright, with no partial-occlusion tolerance.
    //   - When multiple registered items are simultaneously in frame and
    //     unoccluded, the one CLOSEST TO VIEWPORT CENTER is chosen as
    //     CurrentAimTarget - i.e. whichever the player is actually looking at, not
    //     whichever happens to sit nearer to the camera in 3D space. Confirmed live
    //     as a real bug under the old "closest to camera wins" rule: aiming dead-
    //     center at the blood spatter (EVD-015, ~1.59m away) with the body (BODY-A,
    //     ~1.27m away, 0.34m from the spatter) sitting off to the side of frame
    //     still resolved CurrentAimTarget to the body - the nearer item stole the
    //     shot even though the player was visibly centered on the other one. Two
    //     evidence items sitting close together (the norm at a real scene, not an
    //     edge case) made this the common case, not a rare one. maxDistance is
    //     still a hard cutoff (see the range check below), so nothing outside real
    //     camera range is ever eligible regardless of framing.
    private void UpdateAimTarget()
    {
        EnsurePovCamera();

        EvidenceProp target = null;

        if (povCameraReady && povCamera != null)
        {
            float bestCenterDistanceSqr = float.MaxValue;
            Vector3 camPos = povCamera.transform.position;

            foreach (var prop in EvidenceProp.All)
            {
                if (prop == null || string.IsNullOrEmpty(prop.evidenceId))
                {
                    continue;
                }

                // A not-yet-discovered item is never a valid photo target, regardless
                // of distance/occlusion/framing - you can't document something the
                // player hasn't found yet. Without this, an undiscovered item sitting
                // physically close to (or on) an already-Found one - e.g. EVD-016,
                // the latent print, now sitting on the knife's own handle - could win
                // the centeredness tie-break and silently steal the aim target away
                // from the item the player actually meant to photograph. Confirmed
                // live as the real cause of "can't photograph the knife" under the
                // old rule; the same risk applies to any tie-break, not just distance.
                if (EvidenceStateManager.Instance == null
                    || !EvidenceStateManager.Instance.IsAtOrPastStatus(prop.evidenceId, EvidenceStatus.Found))
                {
                    continue;
                }

                Vector3 toProp = prop.transform.position - camPos;
                float distanceSqr = toProp.sqrMagnitude;
                if (distanceSqr > maxDistance * maxDistance)
                {
                    continue;
                }

                Vector3 viewportPoint = povCamera.WorldToViewportPoint(prop.transform.position);
                bool inFrustum = viewportPoint.z > 0f
                    && viewportPoint.x >= 0f && viewportPoint.x <= 1f
                    && viewportPoint.y >= 0f && viewportPoint.y <= 1f;
                if (!inFrustum)
                {
                    continue;
                }

                if (Physics.Raycast(camPos, toProp.normalized, out var occlusionHit, Mathf.Sqrt(distanceSqr), ~0, QueryTriggerInteraction.Ignore))
                {
                    // A hit within this same prop's own hierarchy (e.g. a nested
                    // fingerprint/swab marker with its own separate EvidenceProp, like
                    // EVD-019's latent-print marker sitting on the gloves' own surface)
                    // is the item itself, not something blocking it - only a hit
                    // belonging to a genuinely different object counts as occlusion.
                    // Without this, whichever of two overlapping colliders happened to
                    // be marginally closer along the ray silently made the OTHER one
                    // unphotographable, confirmed live: aiming straight down at the
                    // gloves hit the print marker first and refused the gloves as the
                    // aim target from that angle.
                    if (!occlusionHit.collider.transform.IsChildOf(prop.transform))
                    {
                        continue;
                    }
                }

                float dx = viewportPoint.x - 0.5f;
                float dy = viewportPoint.y - 0.5f;
                float centerDistanceSqr = dx * dx + dy * dy;
                if (centerDistanceSqr >= bestCenterDistanceSqr)
                {
                    continue;
                }

                bestCenterDistanceSqr = centerDistanceSqr;
                target = prop;
            }
        }
        else
        {
            // No POV camera resolved yet - fall back to the old single raycast from
            // the prop itself, purely so aiming isn't a hard no-op in that window.
            if (Physics.Raycast(aimOrigin.position, aimOrigin.forward, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                var evidence = hit.collider.GetComponentInParent<EvidenceProp>();
                if (evidence != null && !string.IsNullOrEmpty(evidence.evidenceId)
                    && EvidenceStateManager.Instance != null
                    && EvidenceStateManager.Instance.IsAtOrPastStatus(evidence.evidenceId, EvidenceStatus.Found))
                {
                    target = evidence;
                }
            }
        }

        SetAimTarget(target);
    }

    /// <summary>
    /// Which of the two photograph statuses a shutter press on this evidence would
    /// currently apply, or null if neither is genuinely next (already past
    /// PhotographedFinal, or between the two - ChalkMarked/Marked - where the next
    /// required step isn't a photograph at all). Centralizes the branch so the
    /// confirmation signal in SetAimTarget and the actual transition in TakePhoto can
    /// never disagree about which status is next.
    /// </summary>
    private static EvidenceStatus? ResolveNextPhotoStatus(string evidenceId)
    {
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record == null)
        {
            return null;
        }

        var next = EvidenceStateManager.GetNextRequiredStatus(record.status);
        bool isPhotoStatus = next == EvidenceStatus.PhotographedRaw || next == EvidenceStatus.PhotographedFinal;
        return isPhotoStatus ? next : null;
    }

    private void SetAimTarget(EvidenceProp target)
    {
        if (target != CurrentAimTarget)
        {
            CurrentAimTarget = target;
            OnAimTargetChanged?.Invoke(target);
        }

        bool canCapture = target != null;
        if (canCapture != CanCapture)
        {
            CanCapture = canCapture;
            OnAimValidityChanged?.Invoke(canCapture);
        }

        // Exactly Found or exactly Marked per ResolveNextPhotoStatus, not merely "a
        // real EvidenceProp is in frame" - see IsConfirmedForCapture's field comment.
        var nextPhoto = target != null ? ResolveNextPhotoStatus(target.evidenceId) : null;
        bool confirmed = nextPhoto.HasValue && (PhotoScale == null || PhotoScale.AllowsPhoto(target.evidenceId, nextPhoto.Value, out _));
        if (confirmed != IsConfirmedForCapture)
        {
            IsConfirmedForCapture = confirmed;
            OnConfirmedForCaptureChanged?.Invoke(confirmed);
        }
    }

    private void SetAiming(bool aiming)
    {
        isAiming = aiming;

        if (aiming)
        {
            PlayerUIGate.Enter(this);
        }
        else
        {
            PlayerUIGate.Exit(this);
            AimClosedFrame = Time.frameCount;
        }

        if (!aiming)
        {
            // No target while not aiming - clears any stale red/green dot or
            // outline left over from the moment aiming stopped.
            SetAimTarget(null);
        }

        if (viewfinderOverlay != null)
        {
            viewfinderOverlay.SetActive(aiming);
        }

        // Hide the camera body while aiming - like actually holding a camera up
        // to your eye, you see through the viewfinder, not the camera itself.
        if (cameraRenderers != null)
        {
            foreach (var r in cameraRenderers)
            {
                if (r != null)
                {
                    r.enabled = !aiming;
                }
            }
        }

        if (visualPivot != null)
        {
            if (aimRoutine != null)
            {
                StopCoroutine(aimRoutine);
            }
            aimRoutine = StartCoroutine(AnimateVisual(aiming));
        }

        EnsurePovCamera();
        if (povCameraReady)
        {
            if (fovRoutine != null)
            {
                StopCoroutine(fovRoutine);
            }
            fovRoutine = StartCoroutine(AnimateFieldOfView(aiming));
        }

        // Slow the player to a creep while focused on a shot, like actually
        // holding your breath and inching forward to line up a photo.
        if (locomotor != null)
        {
            var walkingSpeed = locomotor.GetComponent<PlayerWalkingSpeed>();
            if (walkingSpeed != null)
            {
                walkingSpeed.SetAimMultiplier(aiming ? aimMoveSpeedMultiplier : 1f);
                return;
            }
            if (aiming && !locomotorSpeedCached)
            {
                restSpeedFactor = locomotor.SpeedFactor;
                restRunningSpeedFactor = locomotor.RunningSpeedFactor;
                locomotorSpeedCached = true;
            }

            if (locomotorSpeedCached)
            {
                locomotor.SpeedFactor = aiming ? restSpeedFactor * aimMoveSpeedMultiplier : restSpeedFactor;
                locomotor.RunningSpeedFactor = aiming ? restRunningSpeedFactor * aimMoveSpeedMultiplier : restRunningSpeedFactor;
                if (!aiming) locomotorSpeedCached = false;
            }
        }
    }

    // Ease-in/out instead of linear, so both the raise-to-eye and the zoom read
    // as a deliberate, smooth motion rather than a snap.
    private static float SmoothStep01(float k)
    {
        return k * k * (3f - 2f * k);
    }

    private IEnumerator AnimateVisual(bool aiming)
    {
        Vector3 fromPos = visualPivot.localPosition;
        Quaternion fromRot = visualPivot.localRotation;
        Vector3 toPos = aiming ? restLocalPosition + aimLocalPositionOffset : restLocalPosition;
        Quaternion toRot = aiming ? restLocalRotation * Quaternion.Euler(aimLocalEulerOffset) : restLocalRotation;

        float t = 0f;
        while (t < aimTransitionSeconds)
        {
            t += Time.deltaTime;
            float k = SmoothStep01(Mathf.Clamp01(t / aimTransitionSeconds));
            visualPivot.localPosition = Vector3.Lerp(fromPos, toPos, k);
            visualPivot.localRotation = Quaternion.Slerp(fromRot, toRot, k);
            yield return null;
        }

        visualPivot.localPosition = toPos;
        visualPivot.localRotation = toRot;
        aimRoutine = null;
    }

    private IEnumerator AnimateFieldOfView(bool aiming)
    {
        float fromFov = povCamera.fieldOfView;
        float toFov = aiming ? zoomedFieldOfView : restFieldOfView;

        float t = 0f;
        while (t < aimTransitionSeconds)
        {
            t += Time.deltaTime;
            float k = SmoothStep01(Mathf.Clamp01(t / aimTransitionSeconds));
            povCamera.fieldOfView = Mathf.Lerp(fromFov, toFov, k);
            yield return null;
        }

        povCamera.fieldOfView = toFov;
        fovRoutine = null;
    }

    private void TakePhoto()
    {
        LastCaptureUsedScale = false;
        // Resolve ownership before either workflow changes state. In particular the
        // last route photo must not become an evidence photo on the same press.
        LastProcedureWarning = null;
        var scenePhotos = ScenePhotographyManager.Instance;
        bool sceneShot = Camera.main != null && scenePhotos != null
            && scenePhotos.OwnsShutter(Camera.main.transform.position);
        bool evidenceCaptured = !sceneShot && TryCaptureEvidence();
        OnShutterReleased?.Invoke(this, evidenceCaptured);
    }

    private bool TryCaptureEvidence()
    {
        LastProcedureWarning = null;
        if (!CanCapture || CurrentAimTarget == null)
        {
            // Shutter is fully blocked while nothing valid is in frame - no
            // flash, no sound, no evidence marked. The red viewfinder dot is
            // the player-facing reason why.
            Debug.Log("[PhotographTool] Shutter pressed, but no evidence was in frame.");
            return false;
        }

        string evidenceId = CurrentAimTarget.evidenceId;
        var targetStatus = ResolveNextPhotoStatus(evidenceId);

        if (targetStatus == null || ProceduralGateValidator.Instance == null || !ProceduralGateValidator.Instance.CanTransition(evidenceId, targetStatus.Value))
        {
            // GetBlockReason's fallback path reports the item's REAL next required
            // status regardless of which target it's asked about when that target
            // isn't the valid next step - so the placeholder PhotographedRaw target
            // below still yields a correct, generic "Not yet X." message whether the
            // item is actually waiting on ChalkMarked, Marked, or anything else.
            string reason = ProceduralGateValidator.Instance != null
                ? ProceduralGateValidator.Instance.GetBlockReason(evidenceId, targetStatus ?? EvidenceStatus.PhotographedRaw)
                : GameDialogue.Text("sys-evidence-system-unavailable");
            Debug.Log($"[PhotographTool] Can't photograph {evidenceId}: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            // A refusal by the procedural gate. Distinguishable from a confirmed action
            // on purpose - the game declined, which is not the same as the player
            // having picked the wrong item.
            InteractionFeedback.Blocked();
            return false;
        }

        if (PhotoScale != null && !PhotoScale.AllowsPhoto(evidenceId, targetStatus.Value, out string scaleReason))
        {
            NotificationManager.Notify(scaleReason, DialogueKind.Error);
            InteractionFeedback.Blocked();
            return false;
        }
        LastCaptureUsedScale = targetStatus == EvidenceStatus.PhotographedFinal && PhotoScale != null && PhotoScale.RequiresScale(evidenceId);
        LastCaptureSubjectId = evidenceId;
        LastCaptureSubjectName = EvidenceStateManager.Instance.GetDisplayName(evidenceId);
        LastCaptureKind = targetStatus == EvidenceStatus.PhotographedRaw ? PhotoCaptureKind.Original
            : LastCaptureUsedScale ? PhotoCaptureKind.ScaledFinal : PhotoCaptureKind.Final;
        PlayShutterFeedback();
        OnPhotoCaptured?.Invoke();

        // Same STCS suffix ("photographed") for both - preserves the existing
        // "evidence_014_photographed" trigger wiring in STCSManager rather than
        // orphaning it under a new string. STCSTrigger fires once ever, so this
        // still means the same thing it always did: fires on the FIRST photograph
        // of the item (now PhotographedRaw, same as old Photographed's timing),
        // and no-ops harmlessly (a logged warning, not an error) on the second.
        // Warn-mode procedure rules on either photo (raw: the overall scene first; final:
        // measure the position first). Read BEFORE the transition - afterwards the rule is
        // moot - and shown here at the action site, only once the photo actually applied.
        // The photo is never refused for this; EvidenceStateManager logs ProcedureOutOfOrder
        // once per rule per item.
        EvidenceStatus target = targetStatus.Value;
        var warnings = EvidenceStateManager.Instance.GetUnmetProcedureWarnings(evidenceId, target);
        var statusBefore = EvidenceStateManager.Instance.GetRecord(evidenceId)?.status;
        if (target == EvidenceStatus.PhotographedRaw)
        {
            ReportEvidence(evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkPhotographedRaw(id, tool), "photographed");
        }
        else
        {
            ReportEvidence(evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkPhotographedFinal(id, tool), "photographed");
            if (LastCaptureUsedScale && EvidenceStateManager.Instance.GetRecord(evidenceId)?.status == EvidenceStatus.PhotographedFinal)
                PhotoScale.RecordCapture(evidenceId);
        }
        if (warnings.Count > 0 && statusBefore != target
            && EvidenceStateManager.Instance.GetRecord(evidenceId)?.status == target
            && !string.IsNullOrEmpty(warnings[0].PlayerMessage))
        {
            LastProcedureWarning = warnings[0].PlayerMessage;
            NotificationManager.Notify(LastProcedureWarning, DialogueKind.Warning);
        }
        return true;
    }

    /// <summary>Flash + shutter sound only - no capture, no state change. Public so a shot that isn't an evidence photo (a scene photo) can still look and sound like one.</summary>
    public void PlayShutterFeedback()
    {
        if (flashLight != null)
        {
            StartCoroutine(FlashRoutine());
        }

        if (shutterAudioSource != null && shutterClip != null)
        {
            shutterAudioSource.PlayOneShot(shutterClip);
        }
    }

    private IEnumerator FlashRoutine()
    {
        flashLight.enabled = true;
        yield return new WaitForSeconds(flashDuration);
        flashLight.enabled = false;
    }

}
