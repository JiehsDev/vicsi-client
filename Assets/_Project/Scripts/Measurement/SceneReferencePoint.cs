// Assets/_Project/Scripts/Measurement/SceneReferencePoint.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fixed, permanent point of the scene's architecture (a wall corner, a door-frame
/// edge, a newel post) that evidence positions are measured from. Two of these per
/// measured item let the item's original position be reconstructed after it has been
/// collected - see MeasurementRequirement. Never put one on a movable prop.
///
/// Identified everywhere by its stable referenceId; nothing outside the scene (session
/// log, requirements, uploads) ever stores a Transform.
///
/// Shape:
///   Point - distance is measured to the anchor point itself (the original model).
///   Plane - the reference is a flat fixed surface (a wall face, a built-in counter
///           face); distance is the PERPENDICULAR distance to that surface, so it does
///           not matter where along the wall the tape is hooked. The anchor sits on the
///           surface and its forward (blue) axis points out of the surface, into the
///           room. Two perpendicular plane references give rectangular coordinates.
/// The optional MeasurementReferenceZone is only the selectable area the tape must be
/// held in - it never changes the measured value.
/// </summary>
public class SceneReferencePoint : MonoBehaviour
{
    public enum ReferenceShape
    {
        Point = 0,
        Plane = 1
    }
    private static readonly List<SceneReferencePoint> Registered = new();

    [Tooltip("Stable id used by MeasurementRequirement and logged on every measurement event, e.g. \"REF-A\". Never derived from name or position.")]
    public string referenceId;

    [Tooltip("Student-facing name, e.g. \"West Wall\" or \"Counter End\". Never a transform name or id.")]
    public string displayName;

    [Tooltip("Point: measure to the anchor point. Plane: measure perpendicular to the surface the anchor lies on (anchor forward = out of the surface).")]
    public ReferenceShape shape = ReferenceShape.Point;

    [Tooltip("Optional larger selectable area for this reference (a trigger box). Empty = searched in children.")]
    public MeasurementReferenceZone zone;

    [TextArea]
    [Tooltip("What physical feature this is and how to find it, for authors/evaluators.")]
    public string description;

    [Tooltip("The exact point measured from. Empty = this object's own transform.")]
    public Transform anchor;

    [Tooltip("Optional in-world marker for debugging. Hidden in player builds unless showMarkerInBuilds is set.")]
    public GameObject debugMarker;

    public bool showMarkerInBuilds;

    public Vector3 AnchorPosition => (anchor != null ? anchor : transform).position;

    /// <summary>The surface normal for a Plane reference (anchor forward), flattened when measuring on the floor plan.</summary>
    public Vector3 SurfaceNormal(bool onFloorPlan)
    {
        Vector3 n = (anchor != null ? anchor : transform).forward;
        if (onFloorPlan)
        {
            n.y = 0f;
        }
        return n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.forward;
    }

    /// <summary>The reference's selectable zone (assigned, or the first one in children), or null.</summary>
    public MeasurementReferenceZone Zone
    {
        get
        {
            if (zone == null)
            {
                zone = GetComponentInChildren<MeasurementReferenceZone>(true);
            }
            return zone;
        }
    }

    /// <summary>
    /// Whether a tape end at tip counts as "held at this reference": inside its zone when
    /// it has one, otherwise within radius (floor plan) of the anchor point / surface.
    /// Selection only - the measured value always comes from MeasurementGeometry.
    /// </summary>
    public bool ContainsTip(Vector3 tip, float radius)
    {
        var z = Zone;
        if (z != null && z.isActiveAndEnabled)
        {
            return z.Contains(tip);
        }
        return MeasurementGeometry.DistanceToReference(this, tip, true) <= radius;
    }

    public string LogLabel => string.IsNullOrEmpty(referenceId) ? name : referenceId;

    /// <summary>Every active reference point (runtime registry).</summary>
    public static IReadOnlyList<SceneReferencePoint> All => Registered;

    private void Awake()
    {
        if (debugMarker != null && !Application.isEditor && !showMarkerInBuilds)
        {
            debugMarker.SetActive(false);
        }
    }

    private void OnEnable() => Registered.Add(this);

    private void OnDisable() => Registered.Remove(this);

    /// <summary>
    /// The active reference point with this id, or null. Falls back to a scene search
    /// outside Play mode (OnEnable doesn't run in edit mode), so editor tools resolve
    /// ids the same way runtime code does.
    /// </summary>
    public static SceneReferencePoint Find(string referenceId)
    {
        if (string.IsNullOrEmpty(referenceId))
        {
            return null;
        }

        foreach (var point in Registered)
        {
            if (point != null && point.referenceId == referenceId)
            {
                return point;
            }
        }

        if (!Application.isPlaying)
        {
            foreach (var point in FindObjectsByType<SceneReferencePoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (point.referenceId == referenceId)
                {
                    return point;
                }
            }
        }
        return null;
    }

#if UNITY_EDITOR
    // Editor-only: gizmos are never drawn in player builds.
    private void OnDrawGizmos()
    {
        Vector3 p = AnchorPosition;
        Gizmos.color = new Color(1f, 0.55f, 0f);
        Gizmos.DrawWireSphere(p, 0.08f);
        if (shape == ReferenceShape.Plane)
        {
            // A short stub along the surface normal and a small square on the surface.
            Vector3 n = SurfaceNormal(false);
            Gizmos.DrawLine(p, p + n * 0.3f);
            Vector3 right = Vector3.Cross(Vector3.up, n).normalized * 0.25f;
            Vector3 up = Vector3.up * 0.25f;
            Gizmos.DrawLine(p - right - up, p + right - up);
            Gizmos.DrawLine(p + right - up, p + right + up);
            Gizmos.DrawLine(p + right + up, p - right + up);
            Gizmos.DrawLine(p - right + up, p - right - up);
        }
        else
        {
            Gizmos.DrawLine(p, p + Vector3.up * 2.4f);
        }
        UnityEditor.Handles.Label(p + Vector3.up * 0.35f, $"{LogLabel} ({displayName})");
    }
#endif
}
