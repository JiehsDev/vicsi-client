// Assets/_Project/Scripts/Interaction/HudFollowCamera.cs
using UnityEngine;

/// <summary>
/// Keeps a World Space canvas positioned directly in front of the main
/// camera every frame, like a HUD. Screen Space - Overlay canvases don't
/// composite correctly under stereo VR rendering (only part of the HUD -
/// usually just center-anchored elements - ends up visible), so the
/// viewfinder overlay uses this instead: a real World Space canvas that
/// tracks the HMD.
/// </summary>
public class HudFollowCamera : MonoBehaviour
{
    [SerializeField] private float distance = 0.4f;
    [SerializeField] private Camera targetCamera;

    [Tooltip("Extra offset applied in the camera's own local space (x = right, y = up, z = further away) after the base distance - e.g. a positive y moves this HUD higher in view, staying put as the player's head turns.")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    /// <summary>How far in front of the camera this HUD sits. Settable for HUDs built/sized at runtime.</summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <summary>Extra camera-local offset (right/up/forward) applied after the base distance. Settable for HUDs built/positioned at runtime.</summary>
    public Vector3 LocalOffset
    {
        get => localOffset;
        set => localOffset = value;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        Vector3 basePosition = targetCamera.transform.position + targetCamera.transform.forward * distance;
        Vector3 offset = targetCamera.transform.rotation * localOffset;

        transform.SetPositionAndRotation(basePosition + offset, targetCamera.transform.rotation);
    }
}
