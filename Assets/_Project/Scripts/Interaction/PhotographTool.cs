// Assets/_Project/Scripts/Interaction/PhotographTool.cs
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction;
using Oculus.Interaction.Locomotion;

/// <summary>
/// Sits on a grabbable camera prop (built on GrabbableEvidenceBase). Holding
/// it isn't enough to shoot: the player must press the controller's primary
/// (A/X) button to raise the camera to eye level first - this animates the
/// visual mesh into an aiming pose and shows the viewfinder HUD. Only while
/// aiming does the assigned Activate input raycast from the prop's forward
/// direction; if it hits an EvidenceProp, that evidence is marked
/// Photographed and a flash/shutter cue plays for feedback.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class PhotographTool : MonoBehaviour
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    [Header("Aiming (raise-to-eye)")]
    [Tooltip("The shutter only works while aiming. Press the controller's primary (A/X) button to toggle raising the camera to eye level, like lifting it to look through the viewfinder.")]
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

    private Grabbable grabbable;
    private InputAction leftAimAction;
    private InputAction rightAimAction;
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

    private void Awake()
    {
        grabbable = GetComponent<Grabbable>();
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
        // primary-button control so either hand can trigger it.
        leftAimAction = new InputAction("PhotographTool_LeftAim", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        rightAimAction = new InputAction("PhotographTool_RightAim", InputActionType.Button, "<XRController>{RightHand}/primaryButton");

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

    private void OnEnable()
    {
        leftActivateAction?.action.Enable();
        rightActivateAction?.action.Enable();
        leftAimAction.Enable();
        rightAimAction.Enable();
    }

    private void OnDisable()
    {
        leftActivateAction?.action.Disable();
        rightActivateAction?.action.Disable();
        leftAimAction.Disable();
        rightAimAction.Disable();
    }

    private void OnDestroy()
    {
        leftAimAction?.Dispose();
        rightAimAction?.Dispose();
    }

    private void Update()
    {
        bool held = grabbable != null && grabbable.SelectingPointsCount > 0;

        if (!held)
        {
            if (isAiming)
            {
                SetAiming(false);
            }
            return;
        }

        if (leftAimAction.WasPressedThisFrame() || rightAimAction.WasPressedThisFrame())
        {
            SetAiming(!isAiming);
        }

        if (!isAiming)
        {
            return;
        }

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            TakePhoto();
        }
    }

    private void SetAiming(bool aiming)
    {
        isAiming = aiming;

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
        PlayShutterFeedback();

        if (Physics.Raycast(aimOrigin.position, aimOrigin.forward, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            var evidence = hit.collider.GetComponentInParent<EvidenceProp>();
            if (evidence != null && !string.IsNullOrEmpty(evidence.evidenceId))
            {
                if (EvidenceStateManager.Instance != null)
                {
                    EvidenceStateManager.Instance.MarkPhotographed(evidence.evidenceId, RoleId.Photographer);
                    FireSTCSPhotographedTrigger(evidence.evidenceId);
                }
                else
                {
                    Debug.LogWarning("[PhotographTool] No EvidenceStateManager.Instance in scene.");
                }
                return;
            }
        }

        Debug.Log("[PhotographTool] Shutter pressed, but no evidence was in frame.");
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

    // "evidence_014_photographed" for evidenceId "EVD-014" - the digits of the
    // id form the STCS trigger id so any EVD-NNN item wires up automatically.
    private static void FireSTCSPhotographedTrigger(string evidenceId)
    {
        if (STCSManager.Instance == null || string.IsNullOrEmpty(evidenceId))
        {
            return;
        }

        var digits = System.Array.FindAll(evidenceId.ToCharArray(), char.IsDigit);
        if (digits.Length == 0)
        {
            return;
        }

        STCSManager.Instance.Fire($"evidence_{new string(digits)}_photographed");
    }
}
