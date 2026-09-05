// Assets/_Project/Scripts/RoleSystem/PlayerHeightOffset.cs
using UnityEngine;

/// <summary>
/// Shifts the OVRCameraRig's TrackingSpace vertically so the in-VR player
/// height differs from the wearer's real, floor-calibrated headset height
/// (all project scenes use Floor Level tracking, so without this the camera
/// height is simply whatever the headset measures). A negative offset raises
/// the virtual floor under the player, making them appear shorter; positive
/// makes them appear taller. Grab distances, evidence heights, etc. are
/// unaffected since everything else in the scene stays where it is - only
/// the player's own eye height relative to the world moves.
/// </summary>
public class PlayerHeightOffset : MonoBehaviour
{
    [SerializeField] private float heightOffset;
    [SerializeField] private OVRCameraRig cameraRig;

    public float HeightOffset
    {
        get => heightOffset;
        set
        {
            heightOffset = value;
            Apply();
        }
    }

    private void Awake()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
        Apply();
    }
#endif

    private void Apply()
    {
        if (cameraRig == null || cameraRig.trackingSpace == null)
        {
            return;
        }

        Vector3 pos = cameraRig.trackingSpace.localPosition;
        pos.y = heightOffset;
        cameraRig.trackingSpace.localPosition = pos;
    }
}
