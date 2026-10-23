// Assets/_Project/Scripts/Measurement/MeasurementGeometry.cs
using UnityEngine;

/// <summary>
/// The one definition of "the distance a measurement means", shared by the runtime
/// (MeasurementStateManager.TryGetSceneDistance) and the editor bake, so baked expected
/// values and future measured values can never disagree on method.
/// </summary>
public static class MeasurementGeometry
{
    public static float Distance(Vector3 from, Vector3 to, bool onFloorPlan)
    {
        if (onFloorPlan)
        {
            from.y = 0f;
            to.y = 0f;
        }
        return Vector3.Distance(from, to);
    }

    /// <summary>
    /// Distance from a reference to a point by the reference's own shape: to the anchor
    /// point (Point), or perpendicular to its surface (Plane). The single definition used
    /// by the bake, the runtime scene distance and the tape measure.
    /// </summary>
    public static float DistanceToReference(SceneReferencePoint reference, Vector3 point, bool onFloorPlan)
    {
        if (reference.shape == SceneReferencePoint.ReferenceShape.Plane)
        {
            Vector3 n = reference.SurfaceNormal(onFloorPlan);
            Vector3 d = point - reference.AnchorPosition;
            if (onFloorPlan)
            {
                d.y = 0f;
            }
            return Mathf.Abs(Vector3.Dot(d, n));
        }
        return Distance(reference.AnchorPosition, point, onFloorPlan);
    }

    /// <summary>
    /// Where the measurement line meets the reference: the anchor point (Point), or the
    /// foot of the perpendicular from point onto the surface (Plane), at point's height.
    /// </summary>
    public static Vector3 ReferenceFoot(SceneReferencePoint reference, Vector3 point)
    {
        if (reference.shape == SceneReferencePoint.ReferenceShape.Plane)
        {
            Vector3 n = reference.SurfaceNormal(true);
            Vector3 d = point - reference.AnchorPosition;
            d.y = 0f;
            return point - n * Vector3.Dot(d, n);
        }
        return reference.AnchorPosition;
    }

    /// <summary>Preferred angle (degrees) between an item's two reference directions - rectangular coordinates.</summary>
    public const float PreferredReferenceAngle = 90f;

    /// <summary>Default authoring tolerance around PreferredReferenceAngle before a warning is raised.</summary>
    public const float DefaultReferenceAngleTolerance = 15f;

    /// <summary>
    /// Angle (degrees, floor plan) at the evidence between its two reference directions -
    /// toward each reference's foot point, so two perpendicular walls read 90°.
    /// </summary>
    public static float ReferenceAngle(Vector3 target, SceneReferencePoint a, SceneReferencePoint b)
    {
        return AngleAtTarget(target, ReferenceFoot(a, target), ReferenceFoot(b, target));
    }

    /// <summary>
    /// Authoring check only (never used at runtime): null when the angle is within
    /// tolerance of 90°, otherwise the warning text for the Inspector/console.
    /// </summary>
    public static string ReferenceAngleWarning(string evidenceId, float angle, float tolerance = DefaultReferenceAngleTolerance)
    {
        return Mathf.Abs(angle - PreferredReferenceAngle) <= tolerance
            ? null
            : $"Measurement reference directions for {evidenceId} are {angle:0}°. Review the reference placement.";
    }

    /// <summary>
    /// The measured point of an evidence item: its MeasurementTargetAnchor if one is
    /// authored, otherwise its EvidenceProp's transform. Searches the scene outside Play
    /// mode.
    /// </summary>
    public static bool TryGetEvidenceAnchor(string evidenceId, out Vector3 position)
    {
        var anchor = MeasurementTargetAnchor.Find(evidenceId);
        if (anchor != null)
        {
            position = anchor.transform.position;
            return true;
        }

        var prop = EvidenceProp.FindById(evidenceId);
        if (prop == null && !Application.isPlaying)
        {
            foreach (var p in Object.FindObjectsByType<EvidenceProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (p.evidenceId == evidenceId)
                {
                    prop = p;
                    break;
                }
            }
        }

        position = prop != null ? prop.transform.position : Vector3.zero;
        return prop != null;
    }

    /// <summary>
    /// Angle (degrees, floor plan) at the evidence between the directions to A and B.
    /// Near 0 or 180 the two distances barely constrain the position - see the bake's
    /// geometry warning.
    /// </summary>
    public static float AngleAtTarget(Vector3 target, Vector3 a, Vector3 b)
    {
        var da = new Vector2(a.x - target.x, a.z - target.z);
        var db = new Vector2(b.x - target.x, b.z - target.z);
        return Vector2.Angle(da, db);
    }
}
