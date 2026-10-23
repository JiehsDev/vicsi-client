using UnityEngine;

/// <summary>Scene-specific visual route only. Does not move the player or own navigation input.</summary>
[CreateAssetMenu(menuName = "ViCSI/Guidance/Vendor path route")]
public sealed class VendorPathRoute : ScriptableObject
{
    public string sceneName = "CSI_Environment";
    public Vector3[] waypoints;
    [Min(.4f)] public float arrowSpacing = .8f;
    [Min(.1f)] public float arrowWidth = .28f;
    [Min(.1f)] public float arrowLength = .36f;
}
