using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>Face the lobby at startup and place its world-space menu after tracking updates.</summary>
[DefaultExecutionOrder(1000)]
public sealed class LobbyCanvasPlacement : MonoBehaviour
{
    public const float DefaultCenterHeight = 1.65f;
    [SerializeField] private float centerHeight = DefaultCenterHeight;
    private float startupUntil;
    private int enabledFrame;
    private Camera placedCamera;
    private Vector3 placedEye;
    private Vector3 lobbyForward;
    private bool startupFacingApplied;
    private bool firstTrackedFacingApplied;
#if UNITY_EDITOR
    private OVRCameraRig previewRig;
    private Vector3 originalTrackingPosition;
    private bool previewHeightApplied;
#endif

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        startupUntil = Time.unscaledTime + 2f;
        lobbyForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        placedCamera = null;
        startupFacingApplied = false;
        firstTrackedFacingApplied = false;
    }

    private void LateUpdate()
    {
        bool headTracked = OVRManager.isHmdPresent
            && OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter);
#if UNITY_EDITOR
        if (previewHeightApplied && headTracked) RestorePreviewHeight();
        if (!headTracked && !previewHeightApplied)
        {
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            if (rig != null && rig.centerEyeAnchor != null && rig.trackingSpace != null
                && Mathf.Abs(rig.centerEyeAnchor.localPosition.y) < .01f)
            {
                previewRig = rig;
                originalTrackingPosition = rig.trackingSpace.localPosition;
                rig.trackingSpace.localPosition = originalTrackingPosition + Vector3.up * 1.6f;
                previewHeightApplied = true;
                placedCamera = null;
            }
        }
#endif
        var camera = Camera.main;
        if (camera == null || Time.frameCount <= enabledFrame + 2) return;

        // Use the active locomotor so its capsule and player origin agree. Rotating
        // a camera anchor or only the rig transform gets overwritten by locomotion.
        // This runs in builds too, and once more when the first tracked pose arrives.
        if (!startupFacingApplied || (headTracked && !firstTrackedFacingApplied))
        {
            var locomotor = Object.FindAnyObjectByType<FirstPersonLocomotor>();
            if (locomotor == null || !locomotor.isActiveAndEnabled) return;
            locomotor.HandleLocomotionEvent(new LocomotionEvent(0,
                new Pose(Vector3.zero, Quaternion.LookRotation(lobbyForward, Vector3.up)),
                LocomotionEvent.TranslationType.None, LocomotionEvent.RotationType.Absolute));
            startupFacingApplied = true;
            firstTrackedFacingApplied |= headTracked;
            placedCamera = null;
            // The locomotor applies its accumulated pose at the end of this frame.
            return;
        }

        bool startupPoseChanged = Time.unscaledTime < startupUntil
            && Vector3.Distance(camera.transform.position, placedEye) > .1f;
        if (camera == placedCamera && !startupPoseChanged) return;
        Vector3 eye = camera.transform.position;
        Vector3 position = eye + lobbyForward * 1.5f + Vector3.down * .05f;
        // The lobby is furniture-mounted UI: keep it upright and above the desk,
        // independent of startup tracking height or the player crouching.
        position.y = centerHeight;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(lobbyForward, Vector3.up));
        placedCamera = camera;
        placedEye = eye;
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        RestorePreviewHeight();
#endif
    }

#if UNITY_EDITOR
    private void RestorePreviewHeight()
    {
        if (previewHeightApplied && previewRig != null && previewRig.trackingSpace != null)
            previewRig.trackingSpace.localPosition = originalTrackingPosition;
        previewHeightApplied = false;
        previewRig = null;
        placedCamera = null;
    }
#endif
}


