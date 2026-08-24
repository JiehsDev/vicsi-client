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

        transform.SetPositionAndRotation(
            targetCamera.transform.position + targetCamera.transform.forward * distance,
            targetCamera.transform.rotation);
    }
}
