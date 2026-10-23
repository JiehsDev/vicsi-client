// Assets/_Project/Scripts/Measurement/MeasurementReferenceZone.cs
using UnityEngine;

/// <summary>
/// The selectable area for one SceneReferencePoint: a trigger box (e.g. a strip of wall
/// beside the evidence) the tape end must be held in to hook that reference. Gives the
/// Quest controller a forgiving target instead of a tiny exact point. Selection only -
/// it never changes the measured distance, which always comes from the reference's own
/// anchor/surface (MeasurementGeometry.DistanceToReference), so hooking anywhere inside
/// the zone measures the same.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class MeasurementReferenceZone : MonoBehaviour
{
    private BoxCollider box;

    public BoxCollider Box => box != null ? box : (box = GetComponent<BoxCollider>());

    private void Reset()
    {
        Box.isTrigger = true;
    }

    private void Awake()
    {
        Box.isTrigger = true;
    }

    /// <summary>Whether a world point lies inside the zone's box.</summary>
    public bool Contains(Vector3 worldPoint)
    {
        var b = Box;
        Vector3 local = b.transform.InverseTransformPoint(worldPoint) - b.center;
        Vector3 half = b.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var b = Box;
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.35f);
        Gizmos.matrix = b.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(b.center, b.size);
    }
#endif
}
