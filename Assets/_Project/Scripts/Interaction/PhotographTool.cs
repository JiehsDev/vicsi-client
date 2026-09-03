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
/// aiming, a raycast from the prop's forward direction runs every frame to
/// track whether an EvidenceProp is currently in frame (see CanCapture /
/// CurrentAimTarget below) - other components (a viewfinder red/green dot,
/// a white outline on the target) read that state via events instead of
/// duplicating the raycast. Pressing the assigned Activate input only takes
/// a photo - marking that evidence Photographed and playing a flash/shutter
/// cue - while CanCapture is true; otherwise the shutter is a no-op.
///
/// The Photographer capability itself - PlayerTool.ToolRole, registration,
/// held-state - lives in the PlayerTool base so every other tool (sketchpad,
/// evidence bag, recorder, ...) shares the exact same contract.
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

    /// <summary>True while aiming and the forward raycast is currently hitting a valid EvidenceProp within maxDistance. False (and the shutter is a no-op) at every other time, including while not aiming.</summary>
    public bool CanCapture { get; private set; }

    /// <summary>The EvidenceProp currently in frame, or null. Only ever non-null while aiming and CanCapture is true.</summary>
    public EvidenceProp CurrentAimTarget { get; private set; }

    /// <summary>Fired whenever CanCapture changes, including transitions caused by starting/stopping aiming.</summary>
    public event Action<bool> OnAimValidityChanged;

    /// <summary>Fired whenever CurrentAimTarget changes (including to/from null).</summary>
    public event Action<EvidenceProp> OnAimTargetChanged;

    /// <summary>Fired right after a photo is successfully taken (evidence was in frame) - drives shutter VFX/SFX that don't want to duplicate the flash/audio already in PlayShutterFeedback.</summary>
    public event Action OnPhotoCaptured;

    private InputAction leftAimAction;
    private bool isAiming;
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
        // primary-button control. Left hand (X) only: the right hand's A button is
        // reserved for other tools now, so the camera no longer responds to it.
        leftAimAction = new InputAction("PhotographTool_LeftAim", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");

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
    }

    private void OnDestroy()
    {
        leftAimAction?.Dispose();
    }

    private void Update()
    {
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
    private void UpdateAimTarget()
    {
        EvidenceProp target = null;

        // Raycasts from the player's head/eye (the HMD), not the hand holding the
        // camera prop - "what's in the viewfinder" should track where the player
        // is actually looking, not wherever the controller happens to be pointed.
        // Falls back to aimOrigin (the prop itself) if no POV camera is found.
        EnsurePovCamera();
        Transform rayOrigin = (povCameraReady && povCamera != null) ? povCamera.transform : aimOrigin;

        if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            var evidence = hit.collider.GetComponentInParent<EvidenceProp>();
            if (evidence != null && !string.IsNullOrEmpty(evidence.evidenceId))
            {
                target = evidence;
            }
        }

        SetAimTarget(target);
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
        if (!CanCapture || CurrentAimTarget == null)
        {
            // Shutter is fully blocked while nothing valid is in frame - no
            // flash, no sound, no evidence marked. The red viewfinder dot is
            // the player-facing reason why.
            Debug.Log("[PhotographTool] Shutter pressed, but no evidence was in frame.");
            return;
        }

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanTransition(CurrentAimTarget.evidenceId, EvidenceStatus.Photographed))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(CurrentAimTarget.evidenceId, EvidenceStatus.Photographed);
            Debug.Log($"[PhotographTool] Can't mark {CurrentAimTarget.evidenceId} Photographed: {reason}");
            NotificationManager.Notify(reason);
            // A refusal by the procedural gate. Distinguishable from a confirmed action
            // on purpose - the game declined, which is not the same as the player
            // having picked the wrong item.
            InteractionFeedback.Blocked();
            return;
        }

        PlayShutterFeedback();
        OnPhotoCaptured?.Invoke();
        ReportEvidence(CurrentAimTarget.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkPhotographed(id, tool), "photographed");
    }

    private void PlayShutterFeedback()
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
